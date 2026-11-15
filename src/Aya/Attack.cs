using System.Text;
using System.Text.Json.Serialization;

namespace Aya;

/// <summary>A test a model wrote to break the change that failed on the new code, and what it did on the old code.</summary>
public sealed record AttackFailure(string Test, string OnOld, string Message);

/// <summary>
/// An attempt to make changed code fail. A model's suspicion counts for nothing by itself: a test that fails on
/// the new code and passes on the old one is a demonstrated regression, and anything short of that is labelled
/// as what it is.
/// </summary>
public sealed class AttackResult
{
    public required string Request { get; init; }

    public required string Symbol { get; init; }

    public required string SymbolId { get; init; }

    public required string File { get; init; }

    public required List<int> Lines { get; init; }

    public string? TestProject { get; init; }

    /// <summary>
    /// resisted: every test written to break it passes. regression-demonstrated: a test fails on the new code and
    /// passes on the old. fails-on-both: it fails on the old code too, so the change did not cause it or the test
    /// is wrong. possible-defect: it fails on the new code and the old code cannot judge. Otherwise
    /// rejected-compile | rejected-artificial | writer-failed | not-written | inconclusive | not-attempted.
    /// </summary>
    public string Status { get; set; } = "not-attempted";

    public string Detail { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public string? TestFile { get; set; }

    /// <summary>How many tests the accepted attack file contains.</summary>
    public int Tests { get; set; }

    public List<AttackFailure> Failing { get; set; } = new();

    /// <summary>For a test kept from an earlier run because it showed a regression: the day it was found.</summary>
    public string? Kept { get; set; }

    [JsonIgnore]
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>
/// Gets tests written to break the changed units, and lets the old code arbitrate. The split is not left to a
/// model: the dependency graph gives one unit per session. Every unit is attacked at the same time by a session
/// that knows only its own module, the text of its request. Whatever a session says about its tests is ignored;
/// every file is built and run here, on clean copies of the new and the old code.
/// </summary>
public sealed partial class Attacker(ModelSession session)
{
    private const int Rounds = 3;
    private const int RunLimitMs = 8 * 60 * 1000;

    public string Name => session.Name;

    /// <summary>
    /// One session per unit, all at once. The files that come back are built and run together. A file that does
    /// not compile goes back to its session with the compiler's errors, in a second round that is also parallel.
    /// </summary>
    public List<AttackResult> Run(Workbench w, CodeGraph graph, ReviewResult result, List<ChangedSymbol> targets, string outDir, int max)
    {
        var attacks = Prepare(w, graph, result, targets, outDir);
        var pending = attacks.Where(a => a.TestProject is not null).Take(max).ToList();
        w.Trace.Info($"{pending.Count} unit(s) attacked at the same time, one session of {this.Name} each");
        var prompts = pending.ToDictionary(a => a.Request, a => a.Prompt);
        var latest = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var round = 1; round <= Rounds && pending.Count > 0; round++)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var answers = session.AskAll(pending.Select(a => prompts[a.Request]).ToList());
            var wrote = clock.Elapsed.TotalSeconds;
            var items = new List<(AttackResult Attack, string Code)>();
            for (var i = 0; i < pending.Count; i++)
            {
                var a = pending[i];
                var (code, error) = answers[i];
                a.Attempts = round;
                if (code is null)
                {
                    (a.Status, a.Detail) = ("writer-failed", error);
                    continue;
                }

                latest[a.Request] = code;
                if (ModelSession.Trick(code) is { } trick)
                {
                    (a.Status, a.Detail) = ("rejected-artificial", ModelSession.TrickMessage(trick));
                }
                else
                {
                    items.Add((a, code));
                }
            }

            clock.Restart();
            Judge(w, items, $"round{round}");
            w.Trace.Info($"round {round}: {pending.Count} session(s) wrote in {wrote:0}s; built and run on new and old code in {clock.Elapsed.TotalSeconds:0}s");
            foreach (var a in pending)
            {
                w.Trace.Info($"      {a.Request}  {a.Symbol}: {a.Status} — {a.Detail.Split('\n')[0]}");
            }

            // Only what could not be judged at all goes round again. A test that failed is a result, not a retry.
            pending = pending.Where(a => a.Status is "rejected-compile" or "rejected-artificial").ToList();
            foreach (var a in pending)
            {
                prompts[a.Request] = a.Prompt +
                    "\n\n## Your previous attempt\n```csharp\n" + latest[a.Request].Trim() + "\n```\n\n" +
                    "## What happened when it was built\n" + a.Detail + "\n\n" +
                    "Fix that and nothing else: keep the expectations as they were. Reply with the whole file in a single csharp code block.";
            }
        }

        foreach (var a in attacks.Where(a => latest.ContainsKey(a.Request)))
        {
            Save(a, latest[a.Request], Path.Combine(outDir, "attacks"));
        }

        return attacks;
    }

    /// <summary>Takes test files written elsewhere, named A1.cs, A2.cs and so on, and arbitrates them all in one go.</summary>
    public List<AttackResult> RunFrom(Workbench w, CodeGraph graph, ReviewResult result, List<ChangedSymbol> targets, string outDir, int max, string folder)
    {
        // Read before anything else touches a working copy: the folder may be inside one that is cleaned on use.
        var found = !Directory.Exists(folder)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : Directory.EnumerateFiles(folder, "A*.cs", SearchOption.AllDirectories)
                .Where(f => Path.GetFileNameWithoutExtension(f) is { Length: > 1 } name && name[1..].All(char.IsDigit))
                .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj"))
                .GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => File.ReadAllText(g.First()), StringComparer.OrdinalIgnoreCase);
        w.Trace.Info($"{found.Count} test file(s) found in {folder}");

        var attacks = Prepare(w, graph, result, targets, outDir);
        var items = new List<(AttackResult Attack, string Code)>();
        foreach (var a in attacks.Where(a => a.TestProject is not null).Take(max))
        {
            if (!found.TryGetValue(a.Request, out var code))
            {
                (a.Status, a.Detail) = ("not-written", "no file with this name was found");
                continue;
            }

            a.Attempts = 1;
            if (ModelSession.Trick(code) is { } trick)
            {
                (a.Status, a.Detail) = ("rejected-artificial", ModelSession.TrickMessage(trick));
            }
            else
            {
                items.Add((a, code));
            }
        }

        Judge(w, items.ToList(), "batch");
        foreach (var a in attacks.Where(a => a.TestProject is not null).Take(max))
        {
            w.Trace.Info($"{a.Request}  {a.Symbol}: {a.Status} — {a.Detail.Split('\n')[0]}");
            if (a.Attempts > 0)
            {
                Save(a, found[a.Request], Path.Combine(outDir, "attacks"));
            }
        }

        return attacks;
    }

    /// <summary>
    /// Builds and runs every file at once on the new code, and the ones with a failing test on the old code too.
    /// A file that does not compile is taken out and the rest run again, so one bad file does not sink the others.
    /// </summary>
    public static void Judge(Workbench w, List<(AttackResult Attack, string Code)> items, string label)
    {
        foreach (var group in items.GroupBy(i => i.Attack.TestProject!).ToList())
        {
            var live = group.ToList();
            var onNew = RunCompiling(w, w.Work, group.Key, live, $"{label}-new", (a, errors) =>
                (a.Status, a.Detail) = ("rejected-compile", "It does not compile:\n" + string.Join("\n", errors.Take(6))));
            if (onNew is null)
            {
                continue;
            }

            // One file that hangs or brings the test runner down would take the verdict of every other file with
            // it. When that happens to a batch, each file is judged on its own.
            if ((onNew.TimedOut || onNew.Results.Count == 0) && live.Count > 1)
            {
                foreach (var one in live)
                {
                    Judge(w, [one], $"{label}-{one.Attack.Request}");
                }

                continue;
            }

            var failing = new List<(AttackResult Attack, string Code)>();
            foreach (var (a, code) in live)
            {
                var mine = onNew.Results.Where(r => Owns(a, r.Name)).ToList();
                a.Tests = mine.Count;
                a.Failing.Clear();
                if (onNew.TimedOut)
                {
                    (a.Status, a.Detail) = ("inconclusive", "The run was stopped at the time limit.");
                }
                else if (mine.Count == 0)
                {
                    (a.Status, a.Detail) = ("rejected-compile", $"The test runner found no test in a class named {ClassOf(a)}.");
                }
                else if (!mine.Any(r => r.Failed) && !mine.Any(r => r.Passed))
                {
                    // Not one of its tests ran to a result. That is not having resisted anything.
                    (a.Status, a.Detail) = ("inconclusive", $"None of its {mine.Count} test(s) ran to a result (skipped or not executed).");
                }
                else if (!mine.Any(r => r.Failed))
                {
                    var idle = mine.Count(r => !r.Passed);
                    (a.Status, a.Detail) = ("resisted", $"{mine.Count(r => r.Passed)} test(s) written to break it pass on the new code" + (idle > 0 ? $"; {idle} did not run to a result" : string.Empty));
                }
                else
                {
                    failing.Add((a, code));
                }
            }

            if (failing.Count == 0)
            {
                continue;
            }

            // Something fails on the new code. Whether the change is to blame is for the old code to say.
            var arbitrable = failing.ToList();
            var onOld = File.Exists(Path.Combine(w.OldWork, group.Key))
                ? RunCompiling(w, w.OldWork, group.Key, arbitrable, $"{label}-old", (_, _) => { })
                : null;
            foreach (var (a, _) in failing)
            {
                var judged = onOld is not null && arbitrable.Any(x => x.Attack == a);
                foreach (var f in onNew.Results.Where(r => Owns(a, r.Name) && r.Failed))
                {
                    var before = !judged ? "cannot-compile"
                        : onOld!.Results.FirstOrDefault(x => x.Name == f.Name) is not { } old ? "not-run"
                        : old.Passed ? "Passed"
                        : old.Failed ? "Failed"
                        : old.Outcome;
                    a.Failing.Add(new AttackFailure(f.Name, before, f.Message ?? string.Empty));
                }

                var regressions = a.Failing.Count(f => f.OnOld == "Passed");
                var both = a.Failing.Count(f => f.OnOld == "Failed");
                var passing = a.Tests - a.Failing.Count;
                (a.Status, a.Detail) = regressions > 0
                    ? ("regression-demonstrated", $"{regressions} test(s) fail on the new code and pass on the old code: {string.Join(", ", a.Failing.Where(f => f.OnOld == "Passed").Select(f => f.Test))}")
                    : both == a.Failing.Count
                        ? ("fails-on-both", $"{both} test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; {passing} other(s) pass")
                        : ("possible-defect", $"{a.Failing.Count} test(s) fail on the new code and the old code cannot judge them (" +
                            (a.Failing.Any(f => f.OnOld == "cannot-compile") ? "they need code the change added" : "they did not run to a result there") +
                            $"): {string.Join(", ", a.Failing.Select(f => f.Test))}");
            }
        }
    }

    /// <summary>The name of the class a request's tests must be in.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"Aya[AK]\d+Attack")]
    private static partial System.Text.RegularExpressions.Regex AttackClass();

    public static string ClassOf(AttackResult a) => $"Aya{a.Request}Attack";

    /// <summary>Whether a test result belongs to a request, by the class its name carries.</summary>
    public static bool Owns(AttackResult a, string testName) =>
        testName.StartsWith(ClassOf(a) + ".", StringComparison.Ordinal) || testName.Contains("." + ClassOf(a) + ".", StringComparison.Ordinal);

    /// <summary>Whether a compiler error is about a request's file, and not one whose name merely starts the same.</summary>
    public static bool Mentions(string error, string request) =>
        error.Contains($"\\{request}.cs(", StringComparison.OrdinalIgnoreCase) || error.Contains($"/{request}.cs(", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Runs the files together, dropping the ones the compiler rejects until the rest build. Returns null when
    /// none is left. The list is trimmed in place to the files that did compile.
    /// </summary>
    private static TestRun? RunCompiling(Workbench w, string worktree, string project, List<(AttackResult Attack, string Code)> live, string label, Action<AttackResult, List<string>> reject)
    {
        for (var round = 1; live.Count > 0; round++)
        {
            var files = live.Select(i => (i.Attack.Request + ".cs", i.Code)).ToList();
            var classes = live.Select(i => ClassOf(i.Attack)).ToList();
            var run = Candidates.With(worktree, project, files, () =>
                TestRunner.Run(worktree, project, w.RunsDir, Path.Combine("attacks", $"{label}-{round}"), classes, coverage: false, w.SdkFolder, RunLimitMs));
            if (run.Built)
            {
                return run;
            }

            // When the compiler fails without naming any of the files, none of them can be told apart.
            var broken = live.Where(i => run.BuildErrors.Any(e => Mentions(e, i.Attack.Request))).ToList();
            foreach (var i in broken.Count > 0 ? broken : live.ToList())
            {
                reject(i.Attack, run.BuildErrors.Where(e => Mentions(e, i.Attack.Request)).DefaultIfEmpty(run.BuildErrors.FirstOrDefault() ?? "the build failed").ToList());
                live.Remove(i);
            }
        }

        return null;
    }

    /// <summary>
    /// Keeps every test that showed a regression, and runs again the ones kept by earlier runs. What a model
    /// happened to write once is not left to whether it writes it again: a kept test is arbitrated like a new one,
    /// by the old and the new code, and says where it came from.
    /// </summary>
    public static List<AttackResult> Replay(Workbench w, List<AttackResult> attacks, string outDir, string store)
    {
        const string Mark = "// aya-kept ";
        Directory.CreateDirectory(store);
        var known = Directory.EnumerateFiles(store, "*.cs").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        var kept = new List<(AttackResult Attack, string Code)>();
        foreach (var file in known)
        {
            var text = File.ReadAllText(file).Replace("\r", string.Empty);
            var end = text.IndexOf('\n');
            if (end < 0 || !text.StartsWith(Mark, StringComparison.Ordinal))
            {
                continue;
            }

            // The first line says what the test was written against: symbol id, class name and when, separated by tabs.
            var parts = text[Mark.Length..end].Split('\t');
            if (parts.Length < 3 || attacks.FirstOrDefault(a => a.SymbolId == parts[0] && a.TestProject is not null) is not { } like)
            {
                continue;
            }

            var request = "K" + (kept.Count + 1);
            var attack = new AttackResult
            {
                Request = request,
                Symbol = like.Symbol,
                SymbolId = like.SymbolId,
                File = like.File,
                Lines = like.Lines,
                TestProject = like.TestProject,
                Attempts = 1,
                Kept = parts[2],
            };
            kept.Add((attack, text[(end + 1)..].Replace(parts[1], ClassOf(attack), StringComparison.Ordinal)));
        }

        if (kept.Count > 0)
        {
            w.Trace.Info($"{kept.Count} test(s) kept from earlier runs because they showed a regression are run again");
            Judge(w, kept.ToList(), "kept");
            foreach (var (a, code) in kept)
            {
                Save(a, code, Path.Combine(outDir, "attacks"));
                w.Trace.Info($"      {a.Request}  {a.Symbol}: {a.Status} — {a.Detail.Split('\n')[0]}");
            }
        }

        // What this run found goes into the store, once: the same text is not kept twice.
        var have = known.Select(f => File.ReadAllText(f).Replace("\r", string.Empty)).Select(t => t[(t.IndexOf('\n') + 1)..]).Select(Bare).ToHashSet(StringComparer.Ordinal);
        foreach (var a in attacks.Where(a => a.Status == "regression-demonstrated" && a.TestFile is not null))
        {
            var path = Path.Combine(outDir, a.TestFile!);
            if (!File.Exists(path))
            {
                continue;
            }

            var code = File.ReadAllText(path).Replace("\r", string.Empty);
            if (have.Add(Bare(code.Replace(ClassOf(a), "X", StringComparison.Ordinal))))
            {
                var name = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{a.Request}.cs";
                File.WriteAllText(Path.Combine(store, name), $"{Mark}{a.SymbolId}\t{ClassOf(a)}\t{DateTime.UtcNow:yyyy-MM-dd}\n{code}");
            }
        }

        return kept.Select(k => k.Attack).ToList();

        static string Bare(string code) => string.Join(' ', AttackClass().Replace(code, "X").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// A test that uses something the change introduced cannot be judged by the old code: it passes there because
    /// there is nothing to test, not because the old code is right. Such a pass is taken back. A file left with
    /// no test the old code could really judge is no longer a demonstrated regression but a failing test with no
    /// arbiter.
    /// </summary>
    public static void WithoutEmptyPasses(List<AttackResult> attacks, string outDir, IReadOnlyCollection<string> introduced)
    {
        if (introduced.Count == 0)
        {
            return;
        }

        foreach (var a in attacks.Where(a => a.Status == "regression-demonstrated" && a.TestFile is not null))
        {
            var path = Path.Combine(outDir, a.TestFile!);
            if (!File.Exists(path))
            {
                continue;
            }

            var code = File.ReadAllText(path);
            var empty = 0;
            string? because = null;
            for (var i = 0; i < a.Failing.Count; i++)
            {
                var f = a.Failing[i];
                if (f.OnOld != "Passed" || introduced.FirstOrDefault(name => BodyOf(code, f.Test).Contains(name, StringComparison.Ordinal)) is not { } name)
                {
                    continue;
                }

                a.Failing[i] = f with { OnOld = "not-arbitrated" };
                because ??= name;
                empty++;
            }

            if (empty == 0)
            {
                continue;
            }

            var judged = a.Failing.Count(f => f.OnOld == "Passed");
            var note = $"{empty} test(s) that fail on the new code pass on the old only because they use {because}, which the change introduced: the old code has nothing there to be tested, so its passing says nothing";
            (a.Status, a.Detail) = judged > 0
                ? (a.Status, a.Detail + "\n" + note + ".")
                : ("possible-defect", note + ". No test of this file is one the old code can judge.");
        }
    }

    /// <summary>The text of one test method, without its comments: from its name to the next test of the file.</summary>
    private static string BodyOf(string code, string testName)
    {
        var method = ModelSession.MethodOf(testName).Split('.').Last();
        var start = code.IndexOf(" " + method + "(", StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var end = new[] { "[Fact", "[Theory" }.Select(mark => code.IndexOf(mark, start, StringComparison.Ordinal)).Where(i => i > 0).DefaultIfEmpty(code.Length).Min();
        return string.Join('\n', code[start..end].Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    private static void Save(AttackResult a, string code, string attackDir)
    {
        var usable = a.Status is "resisted" or "regression-demonstrated" or "fails-on-both" or "possible-defect";
        var name = usable ? a.Request + ".cs" : a.Request + ".rejected.cs";
        File.WriteAllText(Path.Combine(attackDir, name), code);
        a.TestFile = "attacks/" + name;
    }

    private static List<AttackResult> Prepare(Workbench w, CodeGraph graph, ReviewResult result, List<ChangedSymbol> targets, string outDir)
    {
        var requestDir = Path.Combine(outDir, "attack-requests");
        var attackDir = Path.Combine(outDir, "attacks");
        Directory.CreateDirectory(requestDir);
        Directory.CreateDirectory(attackDir);
        foreach (var stale in Directory.EnumerateFiles(requestDir, "*.md").Concat(Directory.EnumerateFiles(attackDir, "*")).ToList())
        {
            File.Delete(stale);
        }

        var attacks = new List<AttackResult>();
        foreach (var c in targets)
        {
            var pattern = ModelSession.FindPattern(w, graph, result, c.Id, c.File);
            var attack = new AttackResult
            {
                Request = "A" + (attacks.Count + 1),
                Symbol = c.Display,
                SymbolId = c.Id,
                File = c.File,
                Lines = c.ChangedLines.OrderBy(l => l).ToList(),
                TestProject = pattern is null ? null : w.ProjectPath(pattern.Project),
            };
            if (pattern is null)
            {
                attack.Detail = "no existing test reaches this code, so there is neither a pattern to follow nor a project to put a test in";
            }
            else
            {
                attack.Prompt = BuildPrompt(w, graph, result, c, attack, pattern, targets.Count);
                File.WriteAllText(Path.Combine(requestDir, attack.Request + ".md"), attack.Prompt);
            }

            attacks.Add(attack);
        }

        return attacks;
    }

    /// <summary>
    /// The whole module one session works in: what the job is, what the change did to its unit, what has already
    /// been measured about it, the code, an existing test to imitate, and what will be done with its answer.
    /// Enough to aim well, and nothing that would send it looking elsewhere.
    /// </summary>
    private static string BuildPrompt(Workbench w, CodeGraph graph, ReviewResult result, ChangedSymbol c, AttackResult attack, Node pattern, int units)
    {
        var source = File.ReadAllLines(Path.Combine(w.HeadWorktree, c.File));
        var span = graph.Nodes[c.Id].Spans.FirstOrDefault(s => string.Equals(s.File, c.File, StringComparison.OrdinalIgnoreCase)) ?? new Span(c.File, attack.Lines.Min(), attack.Lines.Max());

        var sb = new StringBuilder();
        sb.AppendLine($"# Your module: {c.Display}");
        sb.AppendLine();
        sb.AppendLine("Write one xUnit test file. Reply with a single ```csharp code block containing the whole file, and nothing else.");
        sb.AppendLine();
        sb.AppendLine("## What this is");
        sb.AppendLine("A change to an existing C# repository was written by an AI" + (result.Requirement is null ? "." : $", to implement {result.Requirement}.") +
            (result.Subject.Length > 0 ? $" Its commit message is: \"{result.Subject}\"." : string.Empty));
        sb.AppendLine("A person has to sign that change off, and a review tool is finding out for them where it could break behaviour that");
        sb.AppendLine("existed before without anyone noticing. Part of that review is an attack: for each unit the change touched, tests are");
        sb.AppendLine($"written that the changed code may fail. {units} unit(s) are being attacked at the same time, each by a separate session.");
        sb.AppendLine("Yours is the one named above. This message is your whole module: the other units are not yours, and nothing outside");
        sb.AppendLine("this message is needed or available.");
        sb.AppendLine();
        sb.AppendLine("## What the change did to your unit");
        sb.AppendLine($"- It {(c.ChangeKind == "modified" ? "modified code that already existed" : "is new code")}: line(s) {ReviewCommand.Ranges(attack.Lines)} of `{c.File}`.");
        sb.AppendLine(c.Dependents == 0
            ? "- Nothing else in the analysed code depends on it."
            : $"- {c.Dependents} other members depend on it" + (c.PublicDependents.Count > 0 ? $", among them public API such as {string.Join(", ", c.PublicDependents.Take(2))}" : string.Empty) + ". A mistake here travels.");
        if (Comparer.Hunks(w, c.File, span.Start, span.End) is { Length: > 0 } hunks)
        {
            sb.AppendLine();
            sb.AppendLine("Old lines are marked `-` and new lines `+`:");
            sb.AppendLine("```diff");
            sb.AppendLine(hunks);
            sb.AppendLine("```");
        }

        // What the review has already measured about this unit: the best pointer to where the existing tests are blind.
        var known = new List<string>();
        if (c.CoveredLines.Count + c.UncoveredLines.Count > 0)
        {
            known.Add($"{c.CoveredLines.Count} of its {c.CoveredLines.Count + c.UncoveredLines.Count} changed executable lines run under the existing tests" +
                (c.UncoveredLines.Count > 0 ? $"; line(s) {ReviewCommand.Ranges(c.UncoveredLines)} are never executed by any test" : string.Empty) + ".");
        }

        foreach (var m in c.Mutants.Where(m => m.Status == "survived").Take(4))
        {
            known.Add($"Line {m.Line} can be altered ({m.Description}, in `{m.Original}`) and no existing test fails. Nothing checks what that line does.");
        }

        if (c.Mutants.Count(m => m.Status == "killed") is var caught and > 0)
        {
            known.Add($"{caught} other deliberate alteration(s) of its lines did make an existing test fail, so those are already guarded.");
        }

        foreach (var p in result.Comparisons.Where(p => p.SymbolId == c.Id && p.Status == "compared" && p.Differences.Count > 0))
        {
            known.Add($"Run on the same {p.Cases} inputs, the old and the new code gave a different result on {p.Differences.Count}: " +
                string.Join("; ", p.Differences.Take(3).Select(d => $"\"{d.Case}\" ({d.Kind})")) + ". Whether each difference is intended is exactly what is in doubt.");
        }

        if (known.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## What has already been measured about it");
            foreach (var fact in known)
            {
                sb.AppendLine("- " + fact);
            }

            sb.AppendLine("These are measurements, not opinions. Aim where they say the existing tests are blind.");
        }

        sb.AppendLine();
        sb.AppendLine("## Goal");
        sb.AppendLine("Look for inputs on which the changed code does the wrong thing, and write tests that assert the RIGHT behaviour for them.");
        sb.AppendLine("- A test that fails on the code as it is now is a finding, and is what you are looking for.");
        sb.AppendLine("- Never weaken an expectation to make a test pass, and never assert something you believe is wrong.");
        sb.AppendLine("- For behaviour that existed before the change, right means what it did before, unless changing it is the point of the change.");
        sb.AppendLine("- Write 3 to 6 sharp tests, each aimed at a different way the lines marked `>>` could be wrong.");
        sb.AppendLine();
        sb.AppendLine("## What happens to your file");
        sb.AppendLine("Nothing you say about your tests is used. The review tool builds your file and runs it itself, on a clean copy of the new");
        sb.AppendLine("code and on a clean copy of the old code, and reads the outcome like this:");
        sb.AppendLine("- every test passes on the new code: the unit resisted this attack;");
        sb.AppendLine("- a test fails on the new code and passes on the old code: the change broke something, demonstrated;");
        sb.AppendLine("- a test fails on both: the change is not the cause, or the test is wrong;");
        sb.AppendLine("- a test fails on the new code and cannot be compiled against the old: an open question for the person who signs.");
        sb.AppendLine("So the most valuable test describes behaviour the old code had, uses only what existed before the change, and is one the");
        sb.AppendLine("new code gets wrong. Be quick, but never at the cost of what you assert: a wrong expectation only produces a false alarm");
        sb.AppendLine("that the old code will expose. A file that does not compile is worth nothing.");
        sb.AppendLine();
        sb.AppendLine($"## Code under attack: {c.Display}");
        sb.AppendLine($"File: {c.File}   (lines marked `>>` are the ones the change wrote)");
        sb.AppendLine("```csharp");
        sb.Append(ModelSession.Excerpt(source, span.Start, span.End, attack.Lines.ToHashSet()));
        sb.AppendLine("```");
        sb.AppendLine();
        ModelSession.AppendPattern(sb, w, pattern);
        sb.AppendLine();
        sb.AppendLine("## Rules");
        sb.AppendLine("- Your file is added to the same test project, next to the file above. It can use everything that file uses.");
        sb.AppendLine($"- Declare exactly one public class, named {ClassOf(attack)}, in the same namespace as the test class above (none if it declares none).");
        sb.AppendLine("- It must compile. You cannot change any other file.");
        sb.AppendLine("- Assert on observable behaviour (output, diagnostics, return values), not merely that nothing throws.");
        sb.AppendLine("- Reach the code the way a caller can. Do not use reflection on private members, uninitialised objects or emitted types.");
        sb.AppendLine("- Do not depend on files that do not already exist in the repository.");
        return sb.ToString();
    }
}
