using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Aya;

/// <summary>One input on which the old code and the new code did not do the same thing.</summary>
public sealed class CaseDifference
{
    public required string Case { get; init; }

    /// <summary>
    /// now-fails: gave a result, now throws. changed: gave a result, now gives another. fails-differently: threw,
    /// now throws something else. now-works: threw, now gives a result. missing-on-new: the case no longer records.
    /// </summary>
    public required string Kind { get; set; }

    public required string Old { get; init; }

    public required string New { get; init; }

    /// <summary>Differences with the same group are the same alteration seen on different inputs.</summary>
    public required string Group { get; init; }

    /// <summary>
    /// explained: every line that differs is text the change itself added or removed, so the difference is the
    /// change showing through. unexplained: something differs that is not in the change's text. Null when the
    /// check does not apply. Explained is not the same as correct.
    /// </summary>
    public string? ByChange { get; init; }

    /// <summary>What a model believes about whether the difference was meant, with the text it bases that on. An opinion.</summary>
    public Judgement? Judgement { get; set; }

    /// <summary>The lines that left and the lines that arrived, taken from the whole values before they were cut for storage.</summary>
    public string Delta { get; init; } = string.Empty;

    /// <summary>For a sweep: where the input came from, an existing test or a mechanical alteration of one.</summary>
    public string? Input { get; set; }

    /// <summary>For a sweep: the difference shows only once the input is altered; the input it was made from does not have it.</summary>
    public bool OnlyWhenAltered { get; set; }
}

/// <summary>
/// A model's reading of whether a difference is what the change set out to do. It is never a fact: it is shown
/// as an opinion, with the words of the change it quotes, and it can only send a difference up the list.
/// </summary>
public sealed record Judgement(string Verdict, string Quote, string Reason);

/// <summary>
/// The lines a change added to and removed from the code, kept to recognise them when they show up in a result.
/// Code that writes text (a generator, a template, an error message) puts the lines of the change into its
/// output, and a difference made only of those lines is the change showing through, not a surprise.
/// </summary>
public sealed class ChangeText
{
    /// <summary>
    /// A line shorter than this (`true`, `0`, a name) is found inside almost any change, so finding it inside a
    /// longer line explains nothing. A short line counts only when the change has that very line (`#endif`).
    /// </summary>
    private const int Shortest = 12;
    private readonly List<string> added;
    private readonly List<string> removed;
    private readonly HashSet<string> addedWhole;
    private readonly HashSet<string> removedWhole;

    public ChangeText(IEnumerable<string> addedLines, IEnumerable<string> removedLines)
    {
        this.added = addedLines.Select(Normal).Where(l => l.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        this.removed = removedLines.Select(Normal).Where(l => l.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        this.addedWhole = this.added.ToHashSet(StringComparer.Ordinal);
        this.removedWhole = this.removed.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The lines the change added, with their spacing made uniform.</summary>
    public IReadOnlyList<string> AddedLines => this.added;

    public static ChangeText From(IEnumerable<FileChange> files)
    {
        var code = files.Where(f => f.Category is "source" or "template" or "generated").ToList();
        return new ChangeText(code.SelectMany(f => f.Hunks.SelectMany(h => h.Added)), code.SelectMany(f => f.Hunks.SelectMany(h => h.Removed)))
        {
            TemplateLines = code.Where(f => f.Category == "template").SelectMany(f => f.Hunks.SelectMany(h => h.Added)).Select(Normal).ToHashSet(StringComparer.Ordinal),
        };
    }

    /// <summary>The added lines that sit in a template: every one of them is text the code writes out.</summary>
    public IReadOnlySet<string> TemplateLines { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Whether everything that differs between two results is text of the change: every line that arrived is in
    /// a line the change added, and every line that went is in a line the change removed. Lines too short to
    /// tell anything on their own (a brace) are left out. A long line is recognised inside a line of the change,
    /// because code that writes text holds its lines inside string literals; a short one only as a whole line.
    /// </summary>
    public bool Explains(string was, string now)
    {
        var before = was.Split('\n').Select(Normal).ToHashSet(StringComparer.Ordinal);
        var after = now.Split('\n').Select(Normal).ToHashSet(StringComparer.Ordinal);
        var arrived = after.Where(l => !before.Contains(l) && l.Any(char.IsLetterOrDigit)).ToList();
        var gone = before.Where(l => !after.Contains(l) && l.Any(char.IsLetterOrDigit)).ToList();
        return arrived.Count + gone.Count > 0
            && arrived.All(l => Has(l, this.added, this.addedWhole))
            && gone.All(l => Has(l, this.removed, this.removedWhole));
    }

    private static bool Has(string line, List<string> lines, HashSet<string> whole) =>
        line.Length >= Shortest ? lines.Any(a => a.Contains(line, StringComparison.Ordinal)) : whole.Contains(line);

    private static string Normal(string line) => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>
/// A probe a model wrote for one unit, run on the old code and on the new code, and what the two runs recorded.
/// The model chooses the inputs; which results differ is measured.
/// </summary>
public sealed class Comparison
{
    public required string Request { get; init; }

    /// <summary>
    /// modified-unit: code the change edited. untested-dependent: older code that depends on it and that no test
    /// executes. sweep: the whole change, over inputs AYA built instead of a model.
    /// </summary>
    public required string Purpose { get; init; }

    public required string Symbol { get; init; }

    public required string SymbolId { get; init; }

    public required string File { get; init; }

    /// <summary>Lines the probe had to execute to count.</summary>
    public required List<int> Lines { get; init; }

    /// <summary>For an untested dependent: the changed unit it depends on.</summary>
    public string? DependsOn { get; init; }

    public string? TestProject { get; init; }

    /// <summary>
    /// compared | not-attempted | writer-failed | rejected-artificial | rejected-compile | fails-on-new |
    /// rejected-no-effect | cannot-run-on-old | no-old-version.
    /// </summary>
    public string Status { get; set; } = "not-attempted";

    public string Detail { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public string? ProbeFile { get; set; }

    /// <summary>Cases recorded identically by two runs on the old code: the only ones that can be compared.</summary>
    public int Cases { get; set; }

    /// <summary>Cases whose value changed between two runs of the same old code, and were therefore discarded.</summary>
    public int Unstable { get; set; }

    public int Same { get; set; }

    public int OnlyOnNew { get; set; }

    public List<CaseDifference> Differences { get; set; } = new();

    [JsonIgnore]
    public string Prompt { get; set; } = string.Empty;

    [JsonIgnore]
    public string? Code { get; set; }

    /// <summary>Names the change introduced. A probe must not use them: its inputs have to make sense on the old code.</summary>
    [JsonIgnore]
    public List<string> Introduced { get; set; } = new();

    [JsonIgnore]
    public Dictionary<string, string> HeadRecords { get; set; } = new();

    [JsonIgnore]
    public HashSet<string> StableKeys { get; set; } = new();

    /// <summary>For a sweep: the file of inputs AYA built, which the harness reads instead of choosing its own.</summary>
    [JsonIgnore]
    public string? InputsFile { get; set; }

    /// <summary>For a sweep: every changed executable line, by file. Reaching any of them counts.</summary>
    [JsonIgnore]
    public Dictionary<string, List<int>>? Wider { get; set; }

    [JsonIgnore]
    public int WiderHit { get; set; }

    public int Count(string kind) => this.Differences.Count(d => d.Kind == kind);
}

/// <summary>What the exhaustive mode did in the zone the original tests never execute.</summary>
public sealed class ExhaustiveSummary
{
    /// <summary>Members that depend on the change and that no original test executes.</summary>
    public int Candidates { get; set; }

    public int Attempted { get; set; }

    public int Compared { get; set; }

    public int Sessions { get; set; }

    public int SessionBudget { get; set; }

    public List<BlindImpact> Impacts { get; set; } = new();
}

/// <summary>How many untested dependents change their output when one changed unit is broken on purpose.</summary>
public sealed record BlindImpact(string Symbol, string Break, int ProbedDependents, int ChangedDependents, List<string> Changed);

/// <summary>
/// Runs the same inputs through the old code and the new code and reports where the results differ. This does not
/// depend on how good the existing tests are: a difference shows up whether or not anything asserts on it.
/// </summary>
public sealed partial class Comparer(ModelSession session)
{
    private const int MaxAttempts = 3;
    private const int MinCases = 3;
    private const int RunLimitMs = 8 * 60 * 1000;

    /// <summary>The recording helper placed next to every probe. Its output path is filled in for each run.</summary>
    private const string Helper = """
        // <auto-generated/>
        #pragma warning disable
        using System;
        using System.Collections;
        using System.Globalization;
        using System.IO;
        using System.Text;
        using System.Threading.Tasks;

        internal static class AyaProbe
        {
            private const string OutputPath = @"__OUT__";
            private const string InputsPath = @"__IN__";
            private static readonly object Gate = new object();

            public static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> Inputs()
            {
                var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
                if (InputsPath.Length == 0 || !File.Exists(InputsPath)) { return list; }
                foreach (var line in File.ReadAllLines(InputsPath, Encoding.UTF8))
                {
                    var tab = line.IndexOf('\t');
                    if (tab <= 0) { continue; }
                    list.Add(new System.Collections.Generic.KeyValuePair<string, string>(Unescape(line.Substring(0, tab)), Unescape(line.Substring(tab + 1))));
                }

                return list;
            }

            private static string Unescape(string text)
            {
                var sb = new StringBuilder(text.Length);
                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\\' && i + 1 < text.Length)
                    {
                        i++;
                        sb.Append(text[i] == 'n' ? '\n' : text[i] == 't' ? '\t' : text[i]);
                    }
                    else
                    {
                        sb.Append(text[i]);
                    }
                }

                return sb.ToString();
            }

            public static void Record<T>(string name, Func<T> produce)
            {
                string value;
                try { value = Describe(produce(), 0); }
                catch (Exception ex) { value = Failure(ex); }
                Write(name, value);
            }

            public static void Record(string name, Action act)
            {
                string value;
                try { act(); value = "completed"; }
                catch (Exception ex) { value = Failure(ex); }
                Write(name, value);
            }

            public static async Task RecordAsync<T>(string name, Func<Task<T>> produce)
            {
                string value;
                try { value = Describe(await produce().ConfigureAwait(false), 0); }
                catch (Exception ex) { value = Failure(ex); }
                Write(name, value);
            }

            public static async Task RecordAsync(string name, Func<Task> act)
            {
                string value;
                try { await act().ConfigureAwait(false); value = "completed"; }
                catch (Exception ex) { value = Failure(ex); }
                Write(name, value);
            }

            private static string Failure(Exception ex)
            {
                while (ex is AggregateException && ex.InnerException != null) { ex = ex.InnerException; }
                return "EXCEPTION " + ex.GetType().FullName + ": " + ex.Message;
            }

            private static string Describe(object value, int depth)
            {
                if (value == null) { return "null"; }
                if (value is string) { return (string)value; }
                if (value is IFormattable) { return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture); }
                if (value is IEnumerable && depth < 4)
                {
                    var sb = new StringBuilder("[");
                    var count = 0;
                    foreach (var item in (IEnumerable)value)
                    {
                        if (count > 0) { sb.Append("\n "); }
                        if (count == 400) { sb.Append("…"); break; }
                        sb.Append(Describe(item, depth + 1));
                        count++;
                    }

                    return sb.Append("]").ToString();
                }

                return value.ToString() ?? "null";
            }

            private static string Escape(string text)
            {
                return text.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "").Replace("\n", "\\n");
            }

            private static void Write(string name, string value)
            {
                var line = Escape(AppContext.TargetFrameworkName ?? "") + "\t" + Escape(name ?? "") + "\t" + Escape(value) + "\n";
                lock (Gate)
                {
                    for (var attempt = 0; ; attempt++)
                    {
                        try { File.AppendAllText(OutputPath, line, new UTF8Encoding(false)); return; }
                        catch (IOException) when (attempt < 20) { System.Threading.Thread.Sleep(50); }
                    }
                }
            }
        }
        """;

    private ChangeText? change;

    public string Name => session.Name;

    /// <summary>One probe per unit the change modified, most depended-on first.</summary>
    public List<Comparison> CompareModified(Workbench w, CodeGraph graph, ReviewResult result, string outDir, int max)
    {
        this.change = ChangeText.From(result.Files);
        var targets = result.ChangedSymbols
            .Where(c => !c.IsTest && c.ChangeKind == "modified" && c.CoveredLines.Count + c.UncoveredLines.Count > 0)
            .OrderByDescending(c => c.Dependents)
            .ToList();
        var requests = new List<Comparison>();
        foreach (var c in targets)
        {
            var lines = c.CoveredLines.Concat(c.UncoveredLines).OrderBy(l => l).ToList();
            requests.Add(this.Request(w, graph, result, "P" + (requests.Count + 1), "modified-unit", c.Id, c.Display, c.File, lines, lines.ToHashSet(), null));
        }

        var added = result.ChangedSymbols.Count(c => !c.IsTest && c.ChangeKind == "added");
        w.Trace.Info($"{targets.Count} unit(s) already existed and were modified; asking {this.Name} for a probe for {Math.Min(max, requests.Count(r => r.TestProject is not null))}" +
            (added > 0 ? $". {added} unit(s) are new: there is no old version to compare them with" : string.Empty));
        this.RunAll(w, requests, outDir, int.MaxValue, "P", max);
        return requests;
    }

    /// <summary>
    /// Probes for the older code that depends on the change and that no original test executes, nearest first,
    /// until the session budget runs out. Then each changed unit is broken once to count how many of those
    /// dependents change their output.
    /// </summary>
    public (List<Comparison> Probes, ExhaustiveSummary Summary) Exhaustive(
        Workbench w,
        CodeGraph graph,
        ReviewResult result,
        List<(Node Node, ChangedSymbol Owner, int Distance, List<int> Lines)> candidates,
        Dictionary<string, string> mutantTexts,
        string outDir,
        int sessionBudget)
    {
        this.change = ChangeText.From(result.Files);
        var summary = new ExhaustiveSummary { Candidates = candidates.Count, SessionBudget = sessionBudget };
        w.Trace.Info($"{candidates.Count} member(s) depend on the change and are executed by no original test");
        if (candidates.Count == 0)
        {
            return ([], summary);
        }

        w.Trace.Info($"each needs one to {MaxAttempts} model sessions; the budget is {sessionBudget} session(s), nearest to the change first");
        var requests = new List<Comparison>();
        foreach (var (node, owner, _, lines) in candidates.OrderBy(c => c.Distance).ThenBy(c => c.Node.Display, StringComparer.Ordinal))
        {
            var span = node.Spans[0];
            requests.Add(this.Request(w, graph, result, "X" + (requests.Count + 1), "untested-dependent", node.Id, node.Display, span.File, lines, new HashSet<int>(), owner.Display));
        }

        var before = session.Sessions;
        this.RunAll(w, requests, outDir, sessionBudget, "X", int.MaxValue);
        summary.Sessions = session.Sessions - before;
        summary.Attempted = requests.Count(r => r.Attempts > 0);
        summary.Compared = requests.Count(r => r.Status == "compared");

        // How much of the blind zone moves when a changed unit is wrong: break it once and re-run the probes.
        foreach (var c in result.ChangedSymbols.Where(c => !c.IsTest))
        {
            var probes = requests.Where(r => r.Code is not null && r.HeadRecords.Count > 0 && r.DependsOn == c.Display).ToList();
            var mutant = c.Mutants.FirstOrDefault(m => m.Status == "killed") ?? c.Mutants.FirstOrDefault(m => m.Status == "survived");
            if (probes.Count == 0 || mutant is null || !mutantTexts.TryGetValue(mutant.Id, out var text))
            {
                continue;
            }

            var moved = new List<string>();
            var unmeasured = 0;
            foreach (var p in probes)
            {
                var (brokenRun, broken) = w.WithFile(mutant.File, text, () => this.RunProbe(w, w.Work, p, p.Code!, $"{p.Request}-with-{mutant.Id}", coverage: false));

                // Only a run that completed says anything. One that did not build, hung or recorded nothing would
                // otherwise look like every output having changed.
                if (!brokenRun.Built || brokenRun.Problem is not null || broken.Count == 0)
                {
                    unmeasured++;
                    continue;
                }

                var keys = p.StableKeys.Count > 0 ? p.StableKeys : p.HeadRecords.Keys.ToHashSet(StringComparer.Ordinal);
                if (keys.Any(k => p.HeadRecords.TryGetValue(k, out var was) && (!broken.TryGetValue(k, out var now) || now != was)))
                {
                    moved.Add(p.Symbol);
                }
            }

            summary.Impacts.Add(new BlindImpact(c.Display, $"{Path.GetFileName(mutant.File)}:{mutant.Line} {mutant.Description}", probes.Count - unmeasured, moved.Count, moved));
            w.Trace.Info($"breaking {c.Display} ({mutant.Description}, line {mutant.Line}) changes the output of {moved.Count} of {probes.Count} probed dependent(s)");
        }

        return (requests, summary);
    }

    /// <summary>
    /// Sorts the cases two runs of the old code agree on into the same and the different, against the new code.
    /// A case the old code itself records differently twice is unstable and says nothing.
    /// </summary>
    public static (List<CaseDifference> Differences, HashSet<string> Stable, int Unstable, int Same, int OnlyOnNew) Sort(
        Dictionary<string, string> oldFirst,
        Dictionary<string, string> oldSecond,
        Dictionary<string, string> head,
        ChangeText? change = null,
        bool errorLines = false)
    {
        var stable = oldFirst.Where(kv => oldSecond.TryGetValue(kv.Key, out var again) && again == kv.Value).Select(kv => kv.Key).ToHashSet(StringComparer.Ordinal);
        var unstable = oldFirst.Keys.Union(oldSecond.Keys).Count(k => !stable.Contains(k));
        var differences = new List<CaseDifference>();
        var same = 0;
        foreach (var key in stable.OrderBy(k => k, StringComparer.Ordinal))
        {
            var was = oldFirst[key];
            if (!head.TryGetValue(key, out var now))
            {
                differences.Add(Difference(key, "missing-on-new", was, "(nothing recorded)"));
            }
            else if (now == was)
            {
                same++;
            }
            else
            {
                var kind = (Throws(was), Throws(now)) switch
                {
                    (false, true) => "now-fails",
                    (true, false) => "now-works",
                    (true, true) => "fails-differently",
                    _ => "changed",
                };

                // A sweep records one text per input, and the lines of it that begin with ERROR are the failures.
                // An input that had none and now has one is an input that worked and no longer does.
                if (errorLines && kind == "changed")
                {
                    var (failedBefore, failsNow) = (ErrorLines(was), ErrorLines(now));
                    kind = failedBefore.Count == 0 && failsNow.Count > 0 ? "now-fails"
                        : failedBefore.Count > 0 && failsNow.Count == 0 ? "now-works"
                        : failedBefore.Count > 0 && !failedBefore.SetEquals(failsNow) ? "fails-differently"
                        : kind;
                }

                differences.Add(Difference(key, kind, was, now, change));
            }
        }

        var onlyOnNew = head.Keys.Count(k => !oldFirst.ContainsKey(k) && !oldSecond.ContainsKey(k));
        return (differences, stable, unstable, same, onlyOnNew);
    }

    /// <summary>The lines that left and the lines that arrived between two recorded values.</summary>
    /// <remarks>
    /// A line that leaves or arrives many times is shown once with its count, and a line saying the input no
    /// longer works (ERROR) is shown first: on a long result it would otherwise be the last thing anyone reads.
    /// </remarks>
    public static string Delta(string was, string now)
    {
        var (gone, arrived) = Moved(was, now);
        return string.Join("\n", Once(gone, "- ").Concat(Once(arrived, "+ ")).OrderByDescending(l => l[2..].TrimStart().StartsWith("ERROR ", StringComparison.Ordinal)));

        static IEnumerable<string> Once(List<string> lines, string sign) =>
            lines.GroupBy(l => l.Trim(), StringComparer.Ordinal).Select(g => sign + g.First() + (g.Count() > 1 ? $"   (×{g.Count()})" : string.Empty));
    }

    /// <summary>
    /// What tells one alteration from another: which lines left and which arrived, whatever their number, order
    /// or indentation. The same edit seen on a result with five classes and on one with seven is one alteration.
    /// </summary>
    private static string Signature(string was, string now)
    {
        var (gone, arrived) = Moved(was, now);
        return string.Join("\n", gone.Select(l => "- " + l.Trim()).Concat(arrived.Select(l => "+ " + l.Trim())).Distinct(StringComparer.Ordinal).OrderBy(l => l, StringComparer.Ordinal));
    }

    private static (List<string> Gone, List<string> Arrived) Moved(string was, string now)
    {
        var before = was.Split('\n');
        var after = now.Split('\n');
        var kept = after.ToHashSet(StringComparer.Ordinal);
        var had = before.ToHashSet(StringComparer.Ordinal);
        return (before.Where(l => !kept.Contains(l)).ToList(), after.Where(l => !had.Contains(l)).ToList());
    }

    private static bool Throws(string value) => value.StartsWith("EXCEPTION ", StringComparison.Ordinal);

    private static HashSet<string> ErrorLines(string value) =>
        value.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("ERROR", StringComparison.Ordinal) || l.StartsWith("CRASH ", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);

    private static CaseDifference Difference(string key, string kind, string was, string now, ChangeText? change = null)
    {
        var delta = Delta(was, now);
        var group = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kind + "\n" + Signature(was, now))))[..6].ToLowerInvariant();

        // Only a result that became another result can be the change showing through. A failure cannot.
        var byChange = change is null || kind != "changed" ? null : change.Explains(was, now) ? "explained" : "unexplained";
        return new CaseDifference { Case = key, Kind = kind, Old = Clip(was), New = Clip(now), Group = group, ByChange = byChange, Delta = delta.Length > 1500 ? delta[..1500] + "…" : delta };
    }

    private static string Clip(string value) => value.Length > 500 ? value[..500] + "…" : value;

    private Comparison Request(Workbench w, CodeGraph graph, ReviewResult result, string id, string purpose, string nodeId, string display, string file, List<int> lines, HashSet<int> marked, string? dependsOn)
    {
        var pattern = ModelSession.FindPattern(w, graph, result, nodeId, file);
        var request = new Comparison
        {
            Request = id,
            Purpose = purpose,
            Symbol = display,
            SymbolId = nodeId,
            File = file,
            Lines = lines,
            DependsOn = dependsOn,
            TestProject = pattern is null ? null : w.ProjectPath(pattern.Project),
        };
        if (pattern is null)
        {
            request.Detail = "no existing test reaches this code, so there is neither a pattern to follow nor a project to put a probe in";
        }
        else
        {
            request.Introduced = Introduced(result);
            request.Prompt = BuildPrompt(w, graph, request, pattern, marked);
        }

        return request;
    }

    /// <summary>
    /// Works in rounds. In each one every open request gets a session of its own, all at the same time; the probes
    /// that come back are then run on both versions one by one, and what was rejected goes into the next round
    /// with what happened to it.
    /// </summary>
    private void RunAll(Workbench w, List<Comparison> requests, string outDir, int sessionBudget, string prefix, int maxRequests)
    {
        var requestDir = Path.Combine(outDir, "probe-requests");
        var probeDir = Path.Combine(outDir, "probes");
        Directory.CreateDirectory(requestDir);
        Directory.CreateDirectory(probeDir);
        foreach (var stale in Directory.EnumerateFiles(requestDir, prefix + "*.md").Concat(Directory.EnumerateFiles(probeDir, prefix + "*.cs")).ToList())
        {
            File.Delete(stale);
        }

        var pending = new List<Comparison>();
        foreach (var c in requests.Where(r => r.TestProject is not null))
        {
            File.WriteAllText(Path.Combine(requestDir, c.Request + ".md"), c.Prompt);
            if (pending.Count >= maxRequests)
            {
                continue;
            }

            if (!File.Exists(Path.Combine(w.OldWork, c.TestProject!)))
            {
                (c.Status, c.Detail) = ("no-old-version", "the test project does not exist in the old code, so nothing can be run there");
                w.Trace.Info($"{c.Request}  {c.Symbol}: {c.Status}");
                continue;
            }

            pending.Add(c);
        }

        var started = session.Sessions;
        var prompts = pending.ToDictionary(c => c.Request, c => c.Prompt);
        var latest = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var round = 1; round <= MaxAttempts && pending.Count > 0; round++)
        {
            // The budget is in sessions, so a round takes only as many requests as there are sessions left.
            var left = sessionBudget == int.MaxValue ? pending.Count : sessionBudget - (session.Sessions - started);
            var batch = pending.Take(Math.Max(0, left)).ToList();
            if (batch.Count == 0)
            {
                break;
            }

            var clock = System.Diagnostics.Stopwatch.StartNew();
            var answers = session.AskAll(batch.Select(c => prompts[c.Request]).ToList());
            w.Trace.Info($"round {round}: {batch.Count} session(s) at the same time, answered in {clock.Elapsed.TotalSeconds:0}s");
            for (var i = 0; i < batch.Count; i++)
            {
                var c = batch[i];
                var (code, error) = answers[i];
                c.Attempts = round;
                if (code is null)
                {
                    (c.Status, c.Detail) = ("writer-failed", error);
                    w.Trace.Info($"      {c.Request}  {c.Symbol}: the model gave no usable answer — {error}");
                    continue;
                }

                latest[c.Request] = code;
                this.Check(w, c, code, round);
                w.Trace.Info($"      {c.Request}  {c.Symbol}: {c.Status} — {c.Detail.Split('\n')[0]}");
                if (c.Status != "compared")
                {
                    prompts[c.Request] = c.Prompt +
                        "\n\n## Your previous attempt\n```csharp\n" + code.Trim() + "\n```\n\n" +
                        "## What happened when it was built and run\n" + c.Detail + "\n\n" +
                        "Fix it. Reply with the whole corrected file in a single csharp code block and nothing else.";
                }
            }

            pending = batch.Where(c => c.Status is not ("compared" or "writer-failed")).ToList();
        }

        foreach (var c in requests.Where(c => latest.ContainsKey(c.Request)))
        {
            var name = c.Status == "compared" ? c.Request + ".cs" : c.Request + ".rejected.cs";
            File.WriteAllText(Path.Combine(probeDir, name), latest[c.Request]);
            c.ProbeFile = "probes/" + name;
            c.Code = c.Status == "compared" ? latest[c.Request] : null;
        }
    }

    /// <summary>Accepts a probe only if it runs on both versions and is measured to execute the code it is about.</summary>
    private void Check(Workbench w, Comparison c, string code, int attempt)
    {
        // A probe that uses what the change introduced is exercising the new feature, not the behaviour that
        // existed before, and every such input would show up as a difference.
        if (c.Introduced.FirstOrDefault(name => code.Contains(name, StringComparison.Ordinal)) is { } introduced)
        {
            (c.Status, c.Detail) = ("rejected-new-feature", $"It mentions {introduced}, which the change introduced. Every input must be one the old version already handled: the point is to see whether behaviour that existed has changed. Do not use anything the change added.");
            return;
        }

        if (ModelSession.Trick(code) is { } trick)
        {
            (c.Status, c.Detail) = ("rejected-artificial", ModelSession.TrickMessage(trick));
            return;
        }

        var (run, head) = this.RunProbe(w, w.Work, c, code, $"{c.Request}-try{attempt}-new", coverage: true);
        if (Rejection(run, head, "new") is { } onNew)
        {
            (c.Status, c.Detail) = onNew;
            return;
        }

        var executed = run.CoverageFile is null ? null : TestRunner.ParseCoverage(run.CoverageFile, w.Work);
        var hit = c.Lines.Count(l => executed is not null && executed.TryGetValue(c.File, out var ls) && ls.TryGetValue(l, out var hits) && hits > 0);
        if (c.Wider is { } wider)
        {
            // A sweep is about the whole change: what counts is how many of all its lines the inputs drove.
            hit = wider.Sum(f => f.Value.Count(l => executed is not null && executed.TryGetValue(f.Key, out var ls) && ls.TryGetValue(l, out var hits) && hits > 0));
            c.WiderHit = hit;
        }

        if (hit == 0)
        {
            (c.Status, c.Detail) = ("rejected-no-effect", $"The probe runs, but it executed none of the lines it is about ({ReviewCommand.Ranges(c.Lines)} of {c.File}). Its cases must reach that code.");
            return;
        }

        var (firstRun, first) = this.RunProbe(w, w.OldWork, c, code, $"{c.Request}-try{attempt}-old1", coverage: false);
        if (Rejection(firstRun, first, "old") is { } onOld)
        {
            (c.Status, c.Detail) = (onOld.Status == "rejected-compile" ? "cannot-run-on-old" : onOld.Status, onOld.Detail);
            return;
        }

        var (secondRun, second) = this.RunProbe(w, w.OldWork, c, code, $"{c.Request}-try{attempt}-old2", coverage: false);
        if (Rejection(secondRun, second, "old") is { } onOldAgain)
        {
            // The same code and the same probe as a moment ago. If it does not complete now, that is the machine
            // and not the probe, and nothing can be said about what is stable.
            (c.Status, c.Detail) = ("cannot-run-on-old", "The second run on the old code did not complete, so stable and unstable cases could not be told apart. " + onOldAgain.Detail.Split('\n')[0]);
            return;
        }

        var sorted = Sort(first, second, head, this.change, errorLines: c.Purpose == "sweep");
        if (sorted.Stable.Count < MinCases)
        {
            (c.Status, c.Detail) = ("rejected-no-effect", $"Only {sorted.Stable.Count} case(s) gave the same value in two runs of the old code; {sorted.Unstable} changed from one run to the next. Record values that do not depend on time, order, random data or machine paths.");
            return;
        }

        c.HeadRecords = head;
        c.StableKeys = sorted.Stable;
        c.Cases = sorted.Stable.Count;
        c.Unstable = sorted.Unstable;
        c.Same = sorted.Same;
        c.OnlyOnNew = sorted.OnlyOnNew;
        c.Differences = sorted.Differences;
        c.Status = "compared";
        c.Detail = sorted.Differences.Count == 0
            ? $"{c.Cases} case(s) give the same result on the old and the new code"
            : $"{sorted.Differences.Count} of {c.Cases} case(s) differ: " + string.Join(", ", sorted.Differences.GroupBy(d => d.Kind).Select(g => $"{g.Count()} {g.Key}"));
    }

    /// <summary>Why a probe run cannot be used, with the text the model gets back, or null when it can.</summary>
    private static (string Status, string Detail)? Rejection(TestRun run, Dictionary<string, string> records, string version)
    {
        if (!run.Built)
        {
            var hint = version == "old" ? " It must compile against the old code too: use only types and members that exist in both versions." : string.Empty;
            return ("rejected-compile", $"It does not compile on the {version} code.{hint}\n" + string.Join("\n", run.BuildErrors.Take(6)));
        }

        if (run.TimedOut)
        {
            return ("rejected-no-effect", $"On the {version} code the run was stopped at the time limit.");
        }

        if (run.Results.Count == 0)
        {
            return ("rejected-compile", $"On the {version} code the test runner found no test in the probe class.");
        }

        if (run.Results.FirstOrDefault(r => !r.Passed) is { } bad)
        {
            return ("fails-on-" + version, $"{bad.Name} ended as {bad.Outcome} on the {version} code. A probe records and never asserts: every call must be inside AyaProbe.Record so that a failure is recorded, not thrown.\n{bad.Message}");
        }

        return records.Count < MinCases
            ? ("rejected-no-effect", $"It recorded {records.Count} case(s) on the {version} code; at least {MinCases} are needed.")
            : null;
    }

    private (TestRun Run, Dictionary<string, string> Records) RunProbe(Workbench w, string worktree, Comparison c, string code, string label, bool coverage)
    {
        var dir = Path.Combine(w.RunsDir, "probes");
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, label + ".tsv");
        File.Delete(output);
        var helper = Helper.Replace("__OUT__", output.Replace("\"", "\"\"")).Replace("__IN__", (c.InputsFile ?? string.Empty).Replace("\"", "\"\""));
        var files = new[] { ("AyaProbe.cs", helper), (c.Request + ".cs", code) };

        // A sweep runs every input in one test, so it gets the time a whole test project would.
        var limit = c.InputsFile is null ? RunLimitMs : 3 * RunLimitMs;
        var run = Candidates.With(worktree, c.TestProject!, files, () =>
            TestRunner.Run(worktree, c.TestProject!, w.RunsDir, Path.Combine("probes", label), [$"Aya{c.Request}Probe"], coverage, w.SdkFolder, limit));
        return (run, Read(output, worktree));
    }

    /// <summary>Reads what a probe recorded, with machine-specific paths taken out so two copies of the code compare equal.</summary>
    public static Dictionary<string, string> Read(string file, string worktree)
    {
        var rows = new List<(string Framework, string Name, string Value)>();
        if (File.Exists(file))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                var parts = line.Split('\t');
                if (parts.Length >= 3)
                {
                    rows.Add((Unescape(parts[0]), Unescape(parts[1]), Neutral(Unescape(parts[2]), worktree)));
                }
            }
        }

        var manyFrameworks = rows.Select(r => r.Framework).Distinct().Count() > 1;
        var records = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (framework, name, value) in rows)
        {
            var key = manyFrameworks ? $"{name} [{framework}]" : name;
            var unique = key;
            for (var n = 2; records.ContainsKey(unique); n++)
            {
                unique = $"{key} #{n}";
            }

            records[unique] = value;
        }

        return records;
    }

    private static string Neutral(string value, string worktree)
    {
        // The folder may have been given with either kind of slash, or both; the code under test prints it its own way.
        var full = Path.GetFullPath(worktree);
        foreach (var form in new[] { worktree, full, full.Replace('\\', '/'), full.Replace("\\", "\\\\"), worktree.Replace('\\', '/'), worktree.Replace("\\", "\\\\") }.Distinct().OrderByDescending(f => f.Length))
        {
            value = value.Replace(form, "<repo>", StringComparison.OrdinalIgnoreCase);
        }

        // A stack trace names the line an exception came from. The change moves lines, so the same failure
        // would read differently on the two versions without anything having changed.
        value = LineNumber().Replace(value, ":line N");

        // The frames of a stack trace say how a failure got where it did, not what a caller sees, and they change
        // with any edit: the compiler renumbers the lambdas and local functions of a method when one is added.
        // The kind of failure and its message stay; the frames go.
        value = StackFrame().Replace(value, string.Empty);

        // An assembly's version is stamped by the build from the commit it was built at, so the old and the new
        // code never agree on it. A message that names an assembly would differ without anything having changed.
        return AssemblyVersion().Replace(value, "Version=N");
    }

    private static string Unescape(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                i++;
                sb.Append(text[i] switch { 'n' => '\n', 't' => '\t', _ => text[i] });
            }
            else
            {
                sb.Append(text[i]);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// The names of the types and members the change added, long enough not to collide with ordinary words. An
    /// attribute is also known by its name without the suffix.
    /// </summary>
    public static List<string> Introduced(ReviewResult result)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var c in result.ChangedSymbols.Where(c => !c.IsTest && c.ChangeKind == "added"))
        {
            var paren = c.Display.IndexOf('(');
            var name = (paren > 0 ? c.Display[..paren] : c.Display).Split('.').Last();
            if (name.Length < 8)
            {
                continue;
            }

            names.Add(name);
            if (name.EndsWith("Attribute", StringComparison.Ordinal) && name.Length > 17)
            {
                names.Add(name[..^9]);
            }
        }

        return names.ToList();
    }

    private static string BuildPrompt(Workbench w, CodeGraph graph, Comparison request, Node pattern, HashSet<int> marked)
    {
        var source = File.ReadAllLines(Path.Combine(w.HeadWorktree, request.File));
        var span = graph.Nodes.TryGetValue(request.SymbolId, out var node)
            ? node.Spans.FirstOrDefault(s => string.Equals(s.File, request.File, StringComparison.OrdinalIgnoreCase))
            : null;
        span ??= new Span(request.File, request.Lines.Min(), request.Lines.Max());
        var className = $"Aya{request.Request}Probe";

        var sb = new StringBuilder();
        sb.AppendLine("You are writing one xUnit file for an existing C# repository. It is a probe, not a test:");
        sb.AppendLine("it does not judge the code, it records what the code does.");
        sb.AppendLine("Reply with a single ```csharp code block containing the whole file, and nothing else.");
        sb.AppendLine();
        sb.AppendLine("## Why");
        if (request.Purpose == "modified-unit")
        {
            sb.AppendLine("The code below already existed and was modified. Your probe will be run on the old version of the");
            sb.AppendLine("repository and on the new one, and what it records will be compared case by case. A case that records");
            sb.AppendLine("a different value on the two versions shows that behaviour which existed before has changed.");
        }
        else
        {
            sb.AppendLine($"The code below depends on {request.DependsOn}, which was just modified, and no existing test executes it.");
            sb.AppendLine("Your probe will be run on the old version of the repository and on the new one, and what it records");
            sb.AppendLine("will be compared case by case, to see whether the modification changed what this code does.");
        }

        sb.AppendLine();
        sb.AppendLine($"## Code to exercise: {request.Symbol}");
        sb.AppendLine($"File: {request.File}" + (marked.Count > 0 ? "   (lines marked `>>` are the ones the change wrote)" : string.Empty));
        sb.AppendLine("```csharp");
        sb.Append(ModelSession.Excerpt(source, span.Start, span.End, marked));
        sb.AppendLine("```");
        if (request.Purpose == "modified-unit" && Hunks(w, request.File, span.Start, span.End) is { Length: > 0 } hunks)
        {
            sb.AppendLine();
            sb.AppendLine("## What the change did to it (old lines `-`, new lines `+`)");
            sb.AppendLine("```diff");
            sb.AppendLine(hunks);
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("## How to record");
        sb.AppendLine("A helper named AyaProbe is already in the project. Do not declare it. Use it like this:");
        sb.AppendLine("```csharp");
        sb.AppendLine("AyaProbe.Record(\"case name\", () => value);                           // the value, or the exception if it throws");
        sb.AppendLine("await AyaProbe.RecordAsync(\"case name\", async () => await Something()); // the same for asynchronous code");
        sb.AppendLine("```");
        sb.AppendLine("- Give every case a distinct, descriptive name.");
        sb.AppendLine("- Record what a caller can observe: returned values, generated text, reported diagnostics with their id, message and position.");
        sb.AppendLine("- Strings and collections are recorded in full. Other objects are recorded through ToString(), so turn them into text that shows what matters.");
        sb.AppendLine("- Record at least 8 cases over different inputs: ordinary ones, edge cases, and inputs that drive execution through the code shown above.");
        sb.AppendLine("- Every input must be one the OLD version already handled. The comparison is about behaviour that existed before the change, so an");
        sb.AppendLine("  input that uses something the change added says nothing." + (request.Introduced.Count > 0 ? " The change introduced these names; your file must not contain any of them: " + string.Join(", ", request.Introduced.Take(30)) + "." : string.Empty));
        sb.AppendLine("- Never assert, and never let an exception escape a Record call: the probe must pass on both versions.");
        sb.AppendLine("- Recorded values must not depend on time, random data, ordering of unordered collections or machine paths.");
        sb.AppendLine();
        ModelSession.AppendPattern(sb, w, pattern);
        sb.AppendLine();
        sb.AppendLine("## Rules");
        sb.AppendLine("- Your file is added to the same test project, next to the file above. It can use everything that file uses.");
        sb.AppendLine($"- Declare exactly one public class, named {className}, in the same namespace as the test class above (none if it declares none), with [Fact] methods.");
        sb.AppendLine("- It must compile against BOTH the old and the new version of the repository. Use only types and members that existed before the change.");
        sb.AppendLine("- Reach the code the way a caller can. Do not use reflection on private members, uninitialised objects or emitted types.");
        sb.AppendLine("- Do not depend on files that do not already exist in the repository.");
        return sb.ToString();
    }

    /// <summary>The parts of the file's diff that touch the given lines of the new version.</summary>
    public static string Hunks(Workbench w, string file, int start, int end)
    {
        var diff = w.Git.Try("diff", "-U4", "--no-color", w.BaseSha, w.HeadSha, "--", file);
        if (!diff.Ok)
        {
            return string.Empty;
        }

        var kept = new List<string>();
        var keep = false;
        foreach (var line in diff.StdOut.Replace("\r", string.Empty).Split('\n'))
        {
            if (HunkHeader().Match(line) is { Success: true } m)
            {
                var from = int.Parse(m.Groups[1].Value);
                var to = from + (m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1);
                keep = from <= end && to >= start;
            }

            if (keep)
            {
                kept.Add(line);
            }
        }

        return string.Join("\n", kept.Take(160));
    }

    [GeneratedRegex(@"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@")]
    private static partial Regex HunkHeader();

    [GeneratedRegex(@":line \d+")]
    private static partial Regex LineNumber();

    [GeneratedRegex(@"^[ \t]+at [^\n(]+\([^\n]*\)(?: in [^\n]+)?[ \t]*\n?", RegexOptions.Multiline)]
    private static partial Regex StackFrame();

    [GeneratedRegex(@"Version=\d+(\.\d+){1,3}")]
    private static partial Regex AssemblyVersion();
}
