using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aya;

/// <summary>What the judge was asked and what it answered. Every number here counts opinions, not measurements.</summary>
public sealed class JudgeSummary
{
    public string Writer { get; set; } = string.Empty;

    /// <summary>What the judge was given as the stated purpose of the change.</summary>
    public List<string> Material { get; set; } = new();

    /// <summary>Groups of differences it was asked about: the ones the change's own text does not explain.</summary>
    public int Groups { get; set; }

    public int Intended { get; set; }

    public int NotIntended { get; set; }

    public int Unclear { get; set; }

    /// <summary>Differences it reads as made by the build or the run, not by what the code does. They move nothing.</summary>
    public int Artefact { get; set; }

    /// <summary>Readings of "intended" thrown away because the words quoted for them are not in what the judge was given.</summary>
    public int QuoteNotFound { get; set; }

    /// <summary>Groups left without a reading because the model gave no usable answer.</summary>
    public int Unanswered { get; set; }

    public int Sessions { get; set; }
}

/// <summary>
/// A model that says, for every difference between the old and the new code that the change's own text does not
/// explain, whether it believes the difference is what the change set out to do. It decides nothing: its reading
/// is shown next to the measurement as an opinion, a reading of "intended" must quote the words that ask for the
/// difference and is discarded if they are not there, and no reading moves anything down the list.
/// </summary>
public sealed partial class Judge(ModelSession session)
{
    private const int MostGroups = 12;

    public JudgeSummary Run(Workbench w, ReviewResult result, string outDir)
    {
        var summary = new JudgeSummary { Writer = session.Name };
        var purpose = Purpose(w, result, summary.Material);
        var requestDir = Path.Combine(outDir, "judge-requests");
        Directory.CreateDirectory(requestDir);
        foreach (var stale in Directory.EnumerateFiles(requestDir, "J*.*").ToList())
        {
            File.Delete(stale);
        }

        var asked = new List<(Comparison Run, List<IGrouping<string, CaseDifference>> Groups, string Prompt, string Diff)>();
        foreach (var c in result.Comparisons.Where(c => c.Status == "compared"))
        {
            var groups = c.Differences
                .Where(d => d.ByChange != "explained")
                .GroupBy(d => d.Group, StringComparer.Ordinal)
                .OrderBy(g => g.First().Kind switch { "now-fails" => 0, "missing-on-new" => 1, "changed" => 2, "fails-differently" => 3, _ => 4 })
                .Take(MostGroups)
                .ToList();
            if (groups.Count == 0)
            {
                continue;
            }

            var diff = Comparer.Hunks(w, c.File, c.Lines.Count > 0 ? c.Lines.Min() : 1, c.Lines.Count > 0 ? c.Lines.Max() : int.MaxValue);
            var prompt = BuildPrompt(c, groups, purpose, diff);
            File.WriteAllText(Path.Combine(requestDir, $"J{asked.Count + 1}-{c.Request}.md"), prompt);
            asked.Add((c, groups, prompt, diff));
        }

        summary.Groups = asked.Sum(a => a.Groups.Count);
        if (asked.Count == 0)
        {
            w.Trace.Info("no difference is left unexplained by the change's own text, so there is nothing to ask a judge");
            return summary;
        }

        w.Trace.Info($"{summary.Groups} group(s) of differences are not explained by the change's own text; asking {session.Name} whether each was meant, one session per comparison");
        var before = session.Sessions;
        var answers = session.AskAll(asked.Select(a => a.Prompt).ToList());
        summary.Sessions = session.Sessions - before;
        for (var i = 0; i < asked.Count; i++)
        {
            var (run, groups, _, diff) = asked[i];
            var (text, error) = answers[i];
            if (text is not null)
            {
                File.WriteAllText(Path.Combine(requestDir, $"J{i + 1}-{run.Request}.answer.txt"), text);
            }

            var readings = text is null ? new Dictionary<string, Judgement>() : Parse(text, purpose, diff, out _);
            foreach (var g in groups)
            {
                if (!readings.TryGetValue(g.Key, out var reading))
                {
                    summary.Unanswered++;
                    continue;
                }

                if (reading.Verdict == "quote-not-found")
                {
                    summary.QuoteNotFound++;
                    reading = reading with { Verdict = "unclear" };
                }

                foreach (var d in g)
                {
                    d.Judgement = reading;
                }

                _ = reading.Verdict switch { "intended" => summary.Intended++, "not-intended" => summary.NotIntended++, "artefact" => summary.Artefact++, _ => summary.Unclear++ };
            }

            w.Trace.Info($"      {run.Request}  {run.Symbol}: " + (text is null
                ? $"the model gave no usable answer — {error}"
                : string.Join(", ", groups.Select(g => g.First().Judgement?.Verdict ?? "no reading").GroupBy(v => v).Select(v => $"{v.Count()} {v.Key}"))));
        }

        return summary;
    }

    /// <summary>
    /// Reads the judge's answer. A reading of "intended" stands only if the words it quotes are, letter for letter,
    /// in what the change says it is for. The code of the change does not count: that a line does something is
    /// not a statement that it was meant to. Otherwise the reading comes back marked, to be counted and discarded.
    /// </summary>
    public static Dictionary<string, Judgement> Parse(string answer, string purpose, string diff, out string? problem)
    {
        var readings = new Dictionary<string, Judgement>(StringComparer.Ordinal);
        problem = null;
        var array = JsonArray().Match(answer);
        if (!array.Success)
        {
            problem = "no JSON array in the answer";
            return readings;
        }

        try
        {
            using var doc = JsonDocument.Parse(array.Value);
            var (stated, code) = (Flat(purpose), Flat(diff));
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                string Text(string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
                var (group, verdict, quote, reason) = (Text("group").Trim(), Text("verdict").Trim().ToLowerInvariant(), Text("quote").Trim(), Text("reason").Trim());
                verdict = verdict == "artifact" ? "artefact" : verdict;
                if (group.Length == 0 || verdict is not ("intended" or "not-intended" or "unclear" or "artefact"))
                {
                    continue;
                }

                var words = Flat(quote);
                var inPurpose = words.Length >= 12 && stated.Contains(words, StringComparison.Ordinal);
                var found = inPurpose || (words.Length >= 12 && code.Contains(words, StringComparison.Ordinal));
                if (verdict == "intended" && !inPurpose)
                {
                    readings[group] = new Judgement("quote-not-found", string.Empty, "The model read this as intended, but the words it quoted for it are not in what the change says it is for. The reading is not used. It said: " + Clip(reason));
                    continue;
                }

                readings[group] = new Judgement(verdict, found ? Clip(quote) : string.Empty, Clip(reason));
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            problem = "the answer is not valid JSON";
        }

        return readings;
    }

    /// <summary>
    /// What the change says it is for, in its own words: the commit messages, the requirement it names, and the
    /// documentation and comments it added. AYA does not fetch the text of the requirement.
    /// </summary>
    private static string Purpose(Workbench w, ReviewResult result, List<string> material)
    {
        var sb = new StringBuilder();
        if (result.RequirementText is { Length: > 0 } asked)
        {
            // Written by whoever asked for the change, not by whoever made it: the only text here that the
            // change did not write about itself.
            sb.AppendLine("### The requirement, as written by whoever asked for the change");
            sb.AppendLine(Cut(asked, 6000));
            sb.AppendLine();
            material.Add("the text of the requirement, supplied by the owner");
        }

        var log = w.Git.Try("log", "--format=%B", $"{w.BaseSha}..{w.HeadSha}");
        var messages = log.Ok ? log.StdOut.Replace("\r", string.Empty).Trim() : string.Empty;
        sb.AppendLine("### Commit message(s)");
        sb.AppendLine(messages.Length > 0 ? Cut(messages, 3000) : result.Subject);
        material.Add("the commit message(s) of the change");

        sb.AppendLine();
        sb.AppendLine("### Requirement it names");
        sb.AppendLine(result.Requirement is null ? "(none given)" : result.Requirement + (result.RequirementText is null ? "   (only this reference is known; its text is not available)" : string.Empty));
        if (result.Requirement is not null && result.RequirementText is null)
        {
            material.Add("the reference of the requirement, not its text");
        }

        var docs = result.Files.Where(f => f.Category == "docs").SelectMany(f => f.Hunks.SelectMany(h => h.Added)).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (docs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Documentation the change added");
            sb.AppendLine(Cut(string.Join("\n", docs), 3000));
            material.Add($"{docs.Count} line(s) of documentation the change added");
        }

        var comments = result.Files.Where(f => f.Category is "source" or "template" or "test")
            .SelectMany(f => f.Hunks.SelectMany(h => h.Added))
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("//", StringComparison.Ordinal) && l.TrimStart('/').Trim().Length > 12)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (comments.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Comments the change added to the code");
            sb.AppendLine(Cut(string.Join("\n", comments), 2500));
            material.Add($"{comments.Count} comment line(s) the change added to the code");
        }

        return sb.ToString();
    }

    private static string BuildPrompt(Comparison c, List<IGrouping<string, CaseDifference>> groups, string purpose, string diff)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are asked for an opinion, not for code. Do not write any code.");
        sb.AppendLine();
        sb.AppendLine("A change was made to a C# repository. The same inputs were run through the old code and the new code, and");
        sb.AppendLine("some of them give a different result. That part was measured and is not in question. What nobody measured");
        sb.AppendLine("is whether each difference is what the change set out to do. Say what you believe, for each one.");
        sb.AppendLine();
        sb.AppendLine("## What the change is for (all that is known)");
        sb.AppendLine(purpose);
        if (diff.Length > 0)
        {
            sb.AppendLine($"## What the change did to {c.File} (old lines `-`, new lines `+`)");
            sb.AppendLine("```diff");
            sb.AppendLine(diff);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine($"## The differences, measured on: {c.Symbol}");
        foreach (var g in groups)
        {
            var first = g.First();
            var meaning = first.Kind switch
            {
                "now-fails" => "gave a result on the old code, fails on the new",
                "changed" => "gave a result on the old code, gives another on the new",
                "fails-differently" => "failed on the old code, fails differently on the new",
                "now-works" => "failed on the old code, gives a result on the new",
                _ => "recorded on the old code, not on the new",
            };
            sb.AppendLine();
            sb.AppendLine($"### Group {g.Key}: {meaning}, in {g.Count()} input(s), for example \"{first.Case}\"");
            if (first.Input is not null)
            {
                sb.AppendLine($"The input was {first.Input}.");
            }

            sb.AppendLine("Lines that left the result (`-`) and lines that arrived (`+`):");
            sb.AppendLine("```");
            sb.AppendLine(first.Delta.Length > 0 ? first.Delta : Comparer.Delta(first.Old, first.New));
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("## How to answer");
        sb.AppendLine("Reply with a single code block holding a JSON array, one object per group, and nothing else:");
        sb.AppendLine("```");
        sb.AppendLine("[{\"group\": \"<id>\", \"verdict\": \"intended\" | \"not-intended\" | \"artefact\" | \"unclear\", \"quote\": \"...\", \"reason\": \"one sentence\"}]");
        sb.AppendLine("```");
        sb.AppendLine("- intended: the stated purpose asks for this difference. `quote` must be the words that ask for it, copied");
        sb.AppendLine("  character by character from the section \"What the change is for\". An answer of intended whose quote");
        sb.AppendLine("  is not found there is discarded: that a line of code does something is not a statement that it was meant to.");
        sb.AppendLine("- artefact: the difference is not in what the code does. It comes from the build or the run: a version or");
        sb.AppendLine("  a hash stamped at build time, a name the compiler generates, a path, an address, an ordering nobody controls.");
        sb.AppendLine("- not-intended: the code behaves differently, nothing in the stated purpose asks for it, and it looks like a side effect. `quote` may be");
        sb.AppendLine("  the line of the change that causes it, or empty.");
        sb.AppendLine("- unclear: it cannot be told from what is here. Prefer this to guessing.");
        sb.AppendLine("A difference being an improvement does not make it intended, and a difference being small does not either.");
        sb.AppendLine("Where the requirement is given, it weighs more than what the change says about itself in its commit message,");
        sb.AppendLine("documentation and comments: those were written by whoever made the change. A difference that only they ask for,");
        sb.AppendLine("and the requirement does not, is intended by the author and not shown to be asked for: say so in the reason.");
        sb.AppendLine("Your reading is shown to the person who approves the change as an opinion, next to the measurement. It");
        sb.AppendLine("removes nothing from their list.");
        return sb.ToString();
    }

    /// <summary>
    /// Reads the requirement and says, thing by thing, whether a test that came with the change shows it. A model's
    /// reading, bounded by two checks it cannot talk its way past: the words of each thing asked for must be in the
    /// requirement, and "demonstrated" stands only if the test it names is one that was run on the old code here
    /// and failed there.
    /// </summary>
    public List<Obligation> Fit(Workbench w, ReviewResult result, string outDir)
    {
        if (result.RequirementText is not { Length: > 0 } asked)
        {
            w.Trace.Info("the text of the requirement was not supplied (--req-file), so it cannot be read against the tests");
            return [];
        }

        var source = result.Files.Where(f => f.Category is "source" or "template").Select(f => f.Path).ToList();
        var diff = source.Count == 0 ? string.Empty : w.Git.Try(["diff", "-U3", "--no-color", w.BaseSha, w.HeadSha, "--", .. source]) is { Ok: true } d ? d.StdOut.Replace("\r", string.Empty) : string.Empty;
        var tests = string.Join("\n", result.Files.Where(f => f.Category == "test").SelectMany(f => f.Hunks.SelectMany(h => h.Added)));
        var sb = new StringBuilder();
        sb.AppendLine("You are asked for an opinion, not for code. Do not write any code.");
        sb.AppendLine();
        sb.AppendLine("A change was made to a C# repository to fulfil the requirement below. Break the requirement into the");
        sb.AppendLine("separate things it asks for, and for each one say whether the tests that came with the change show it.");
        sb.AppendLine();
        sb.AppendLine("## The requirement, as written by whoever asked for the change");
        sb.AppendLine(Cut(asked, 6000));
        sb.AppendLine();
        sb.AppendLine("## What the change did to the code (old lines `-`, new lines `+`)");
        sb.AppendLine("```diff");
        sb.AppendLine(Cut(diff, 14000));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## The tests that came with the change, and what running each on the OLD code showed (measured)");
        foreach (var t in result.ProbedTests)
        {
            sb.AppendLine($"- {t.Name}: " + t.Verdict switch
            {
                "discriminating" => "FAILS on the old code, so it shows something the change did",
                "non-discriminating" => "PASSES on the old code too, so it shows nothing about the change",
                "needs-new-code" => "cannot be compiled against the old code, so it was not shown to fail without the change",
                _ => "could not be run on the old code",
            });
        }

        if (tests.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## The text of those tests");
            sb.AppendLine("```csharp");
            sb.AppendLine(Cut(tests, 10000));
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("## How to answer");
        sb.AppendLine("Reply with a single code block holding a JSON array, one object per thing the requirement asks for, and nothing else:");
        sb.AppendLine("```");
        sb.AppendLine("[{\"asks\": \"<words copied from the requirement>\", \"status\": \"demonstrated\" | \"not-demonstrated\" | \"not-addressed\" | \"contradicted\", \"test\": \"<a test from the list, or empty>\", \"reason\": \"one sentence\"}]");
        sb.AppendLine("```");
        sb.AppendLine("- `asks` must be copied character by character from the requirement. An entry whose words are not there is discarded.");
        sb.AppendLine("- demonstrated: a listed test that FAILS on the old code checks exactly this. Name it in `test`. A test that");
        sb.AppendLine("  passes on the old code too demonstrates nothing, whatever it is called.");
        sb.AppendLine("- not-demonstrated: the change appears to do it, but no listed test that fails on the old code shows it.");
        sb.AppendLine("- not-addressed: nothing in the change does it.");
        sb.AppendLine("- contradicted: the change does less than is asked, limits it, or does something the requirement rules out. Say what.");
        sb.AppendLine("Also add an entry with status contradicted for anything the change does to behaviour that existed and that the");
        sb.AppendLine("requirement does not ask for; for that entry `asks` is the sentence of the requirement it goes beyond.");
        var prompt = sb.ToString();
        var dir = Path.Combine(outDir, "judge-requests");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "F1-requirement.md"), prompt);

        var (answer, error) = session.AskAll([prompt])[0];
        if (answer is null)
        {
            w.Trace.Info($"the model gave no usable answer about the requirement — {error}");
            return [];
        }

        File.WriteAllText(Path.Combine(dir, "F1-requirement.answer.txt"), answer);
        var fails = result.ProbedTests.Where(t => t.Verdict == "discriminating").Select(t => t.Name).ToList();
        var list = ParseFit(answer, asked, fails, out var dropped);
        w.Trace.Info($"the model reads {list.Count} thing(s) in the requirement: " + string.Join(", ", list.GroupBy(o => o.Status).Select(g => $"{g.Count()} {g.Key}")) +
            (dropped > 0 ? $"; {dropped} entr(ies) were discarded because their words are not in the requirement" : string.Empty));
        return list;
    }

    /// <summary>
    /// Reads the answer about the requirement. An entry whose words are not in the requirement is dropped, and
    /// "demonstrated" is kept only when the test named is one that failed on the old code.
    /// </summary>
    public static List<Obligation> ParseFit(string answer, string requirement, IReadOnlyCollection<string> failingOnOld, out int dropped)
    {
        var list = new List<Obligation>();
        dropped = 0;
        var array = JsonArray().Match(answer);
        if (!array.Success)
        {
            return list;
        }

        try
        {
            using var doc = JsonDocument.Parse(array.Value);
            var text = Flat(requirement);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                string Text(string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
                var (asks, status, test, reason) = (Flat(Text("asks")), Text("status").Trim().ToLowerInvariant(), Text("test").Trim(), Text("reason").Trim());
                if (status is not ("demonstrated" or "not-demonstrated" or "not-addressed" or "contradicted"))
                {
                    continue;
                }

                if (asks.Length < 8 || !text.Contains(asks, StringComparison.Ordinal))
                {
                    dropped++;
                    continue;
                }

                var named = failingOnOld.FirstOrDefault(t => test.Length > 3 && (t.Contains(test, StringComparison.Ordinal) || test.Contains(ModelSession.MethodOf(t).Split('.').Last(), StringComparison.Ordinal)));
                if (status == "demonstrated" && named is null)
                {
                    (status, reason) = ("not-demonstrated", "The model called this demonstrated, but the test it named is not one that fails on the old code. It said: " + reason);
                }

                list.Add(new Obligation(Clip(asks), status, status == "demonstrated" ? named! : string.Empty, Clip(reason)));
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // Not the list that was asked for: no reading.
        }

        return list;
    }

    private static string Flat(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Cut(string text, int most) => text.Length > most ? text[..most] + "\n…" : text;

    private static string Clip(string text) => text.Length > 400 ? text[..400] + "…" : text;

    [GeneratedRegex(@"\[\s*\{.*\}\s*\]", RegexOptions.Singleline)]
    private static partial Regex JsonArray();
}
