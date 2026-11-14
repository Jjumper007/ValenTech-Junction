using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Aya;

/// <summary>A gap in the evidence, the test a model wrote to fill it, and what checking that test showed.</summary>
public sealed class ProposedTest
{
    public required string Request { get; init; }

    /// <summary>never-executed: lines no test runs. unnoticed-break: a line whose deliberate break no test caught.</summary>
    public required string Reason { get; init; }

    public required string Symbol { get; init; }

    public required string File { get; init; }

    public required List<int> Lines { get; init; }

    /// <summary>For unnoticed-break: the alteration the test has to catch.</summary>
    public string? Break { get; init; }

    public string? TestProject { get; init; }

    /// <summary>accepted | rejected-compile | fails-on-current-code | rejected-no-effect | writer-failed | not-attempted.</summary>
    public string Status { get; set; } = "not-attempted";

    public string Detail { get; set; } = string.Empty;

    public int Attempts { get; set; }

    /// <summary>Where the last version of the test was saved, relative to the output folder.</summary>
    public string? TestFile { get; set; }

    /// <summary>Target lines the accepted test was measured to execute.</summary>
    public List<int> CoversLines { get; set; } = new();

    public List<string> Tests { get; set; } = new();

    [JsonIgnore]
    public string Prompt { get; set; } = string.Empty;

    [JsonIgnore]
    public string? MutantId { get; init; }
}

/// <summary>
/// Asks a model to write tests for the gaps the review found, and accepts none on its word: each test must
/// compile, pass on the code as it is, and be measured to do what it was asked for.
/// </summary>
public sealed partial class TestWriter(string model, string effort)
{
    private const int MaxAttempts = 3;

    public string Name => $"{model} (effort {effort})";

    public List<ProposedTest> Run(
        Workbench w,
        CodeGraph graph,
        List<ChangedSymbol> changed,
        ReviewResult result,
        Dictionary<string, string> mutantTexts,
        string outDir,
        int max)
    {
        var requests = this.BuildRequests(w, graph, changed, result);
        var requestDir = Path.Combine(outDir, "test-requests");
        var proposedDir = Path.Combine(outDir, "proposed-tests");
        Directory.CreateDirectory(requestDir);
        Directory.CreateDirectory(proposedDir);

        // Requests are renumbered on every run, so files left by an earlier run would be mistaken for this one's.
        foreach (var stale in Directory.EnumerateFiles(requestDir, "R*.md").Concat(Directory.EnumerateFiles(proposedDir, "R*.cs")).ToList())
        {
            File.Delete(stale);
        }

        foreach (var r in requests.Where(r => r.Prompt.Length > 0))
        {
            File.WriteAllText(Path.Combine(requestDir, r.Request + ".md"), r.Prompt);
        }

        var attemptable = requests.Where(r => r.TestProject is not null).Take(max).ToList();
        w.Trace.Info($"{requests.Count} gap(s): {requests.Count(r => r.Reason == "never-executed")} stretch(es) of unexecuted code, {requests.Count(r => r.Reason == "unnoticed-break")} unnoticed break(s); asking {this.Name} for {attemptable.Count}");

        foreach (var t in attemptable)
        {
            w.Trace.Info($"{t.Request}  {(t.Reason == "never-executed" ? "never executed" : "unnoticed break")}: {t.Symbol}, line(s) {ReviewCommand.Ranges(t.Lines)}" + (t.Break is null ? string.Empty : $"  [{t.Break}]"));
            var prompt = t.Prompt;
            string? code = null;
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                code = this.Ask(prompt, out var error);
                t.Attempts = attempt;
                if (code is null)
                {
                    (t.Status, t.Detail) = ("writer-failed", error);
                    w.Trace.Info($"      try {attempt}: the model gave no usable answer — {error}");
                    break;
                }

                (t.Status, t.Detail, t.CoversLines, t.Tests) = this.Check(w, t, code, attempt, mutantTexts);
                w.Trace.Info($"      try {attempt}: {t.Status} — {t.Detail.Split('\n')[0]}");
                if (t.Status == "accepted")
                {
                    break;
                }

                prompt = t.Prompt +
                    "\n\n## Your previous attempt\n```csharp\n" + code.Trim() + "\n```\n\n" +
                    "## What happened when it was built and run\n" + t.Detail + "\n\n" +
                    "Fix it. Reply with the whole corrected file in a single csharp code block and nothing else.";
            }

            if (code is not null)
            {
                var name = t.Status == "accepted" ? t.Request + ".cs" : t.Request + ".rejected.cs";
                File.WriteAllText(Path.Combine(proposedDir, name), code);
                t.TestFile = "proposed-tests/" + name;
            }
        }

        return requests;
    }

    /// <summary>One request per symbol with unexecuted lines, then one per break that no test noticed.</summary>
    private List<ProposedTest> BuildRequests(Workbench w, CodeGraph graph, List<ChangedSymbol> changed, ReviewResult result)
    {
        var requests = new List<ProposedTest>();
        var changedTests = changed.Where(c => c.IsTest).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var demonstrating = result.ProbedTests.Where(p => p.Verdict == "discriminating").Select(p => MethodOf(p.Name)).ToHashSet(StringComparer.Ordinal);

        Node? Pattern(ChangedSymbol c) =>
            graph.Reach([c.Id], reverse: true)
                .Select(kv => (Node: graph.Nodes[kv.Key], Distance: kv.Value))
                .Where(x => x.Node is { IsTest: true, TestName: not null } && w.ProjectPath(x.Node.Project) is not null)
                // Best pattern first: a test that came with this change and was shown to exercise it.
                .OrderByDescending(x => demonstrating.Contains(x.Node.TestName!))
                .ThenByDescending(x => changedTests.Contains(x.Node.Id))
                .ThenByDescending(x => w.CoverageByProject.TryGetValue(x.Node.Project, out var cov) && cov.ContainsKey(c.File))
                .ThenBy(x => x.Distance)
                .ThenBy(x => x.Node.TestName, StringComparer.Ordinal)
                .Select(x => x.Node)
                .FirstOrDefault();

        void Add(ChangedSymbol c, string reason, List<int> lines, Mutant? mutant)
        {
            var id = "R" + (requests.Count + 1);
            var pattern = Pattern(c);
            var request = new ProposedTest
            {
                Request = id,
                Reason = reason,
                Symbol = c.Display,
                File = c.File,
                Lines = lines,
                Break = mutant is null ? null : $"{mutant.Description} in `{mutant.Original}`",
                MutantId = mutant?.Id,
                TestProject = pattern is null ? null : w.ProjectPath(pattern.Project),
            };
            if (pattern is null)
            {
                request.Detail = "no existing test reaches this code, so there is neither a pattern to follow nor a project to put a test in";
            }
            else
            {
                request.Prompt = BuildPrompt(w, graph, c, request, pattern);
            }

            requests.Add(request);
        }

        foreach (var c in changed.Where(c => !c.IsTest && c.UncoveredLines.Count > 0).OrderBy(c => c.Layer))
        {
            Add(c, "never-executed", c.UncoveredLines.ToList(), null);
        }

        foreach (var c in changed.Where(c => !c.IsTest))
        {
            foreach (var m in c.Mutants.Where(m => m.Status == "survived"))
            {
                Add(c, "unnoticed-break", [m.Line], m);
            }
        }

        return requests;
    }

    private static string BuildPrompt(Workbench w, CodeGraph graph, ChangedSymbol c, ProposedTest request, Node pattern)
    {
        var source = File.ReadAllLines(Path.Combine(w.HeadWorktree, c.File));
        var span = graph.Nodes[c.Id].Spans.FirstOrDefault(s => string.Equals(s.File, c.File, StringComparison.OrdinalIgnoreCase)) ?? new Span(c.File, request.Lines.Min(), request.Lines.Max());
        var testSpan = pattern.Spans[0];
        var testLines = File.ReadAllLines(Path.Combine(w.HeadWorktree, testSpan.File));
        var className = $"Aya{request.Request}Tests";

        var sb = new StringBuilder();
        sb.AppendLine("You are writing one xUnit test file for an existing C# repository.");
        sb.AppendLine("Reply with a single ```csharp code block containing the whole file, and nothing else.");
        sb.AppendLine();
        sb.AppendLine("## Goal");
        if (request.Reason == "never-executed")
        {
            sb.AppendLine($"No existing test executes the lines marked `>>` below (line(s) {ReviewCommand.Ranges(request.Lines)}).");
            sb.AppendLine("Write tests that make execution reach them, and assert on what the code then does.");
        }
        else
        {
            sb.AppendLine($"Line {request.Lines[0]}, marked `>>` below, can be altered without any existing test failing.");
            sb.AppendLine($"The alteration: {request.Break}.");
            sb.AppendLine("Write a test that passes on the code as it is and would fail if that alteration were made.");
        }

        sb.AppendLine();
        sb.AppendLine($"## Code under test: {c.Display}");
        sb.AppendLine($"File: {c.File}");
        sb.AppendLine("```csharp");
        sb.Append(Excerpt(source, span.Start, span.End, request.Lines.ToHashSet()));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## An existing test that reaches this code. Follow its style, helpers and usings.");
        sb.AppendLine($"File: {testSpan.File}   Test: {pattern.TestName}");
        sb.AppendLine("```csharp");
        if (testLines.Length <= 260)
        {
            sb.AppendLine(string.Join("\n", testLines));
        }
        else
        {
            sb.AppendLine(string.Join("\n", testLines.Take(40)));
            sb.AppendLine("// …");
            sb.AppendLine(string.Join("\n", testLines.Skip(testSpan.Start - 1).Take(Math.Min(120, testSpan.End - testSpan.Start + 1))));
        }

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Rules");
        sb.AppendLine("- Your file is added to the same test project, next to the file above. It can use everything that file uses.");
        sb.AppendLine($"- Declare exactly one public class, named {className}, in the same namespace as the test class above (none if it declares none).");
        sb.AppendLine("- The tests must compile and pass against the code exactly as it is. You cannot change any other file.");
        sb.AppendLine("- Assert on observable behaviour (output, diagnostics, return values), not merely that nothing throws.");
        sb.AppendLine("- Do not depend on files that do not already exist in the repository.");
        return sb.ToString();
    }

    /// <summary>The declaration's first lines plus a window around each marked line, with gaps elided.</summary>
    private static string Excerpt(string[] lines, int start, int end, HashSet<int> marked)
    {
        var show = new SortedSet<int>(Enumerable.Range(start, Math.Min(3, end - start + 1)));
        foreach (var m in marked)
        {
            for (var l = Math.Max(start, m - 16); l <= Math.Min(end, m + 6); l++)
            {
                show.Add(l);
            }
        }

        var sb = new StringBuilder();
        var previous = start - 1;
        foreach (var l in show.Where(l => l >= 1 && l <= lines.Length))
        {
            if (l != previous + 1)
            {
                sb.AppendLine("         …");
            }

            sb.AppendLine($"{(marked.Contains(l) ? ">>" : "  ")}{l,5} | {lines[l - 1]}");
            previous = l;
        }

        return sb.ToString();
    }

    /// <summary>Starts a fresh, tool-less model session with the prompt on standard input and returns the code it wrote.</summary>
    private string? Ask(string prompt, out string error)
    {
        var cwd = Path.Combine(Paths.AyaHome, "model-cwd");
        Directory.CreateDirectory(cwd);
        ProcResult answer;
        try
        {
            answer = Proc.Run(
                "claude",
                ["-p", "--model", model, "--effort", effort, "--tools", string.Empty, "--strict-mcp-config", "--no-session-persistence", "--output-format", "text"],
                cwd,
                10 * 60 * 1000,
                prompt);
        }
        catch (AyaException ex)
        {
            error = ex.Message;
            return null;
        }

        if (!answer.Ok || answer.StdOut.Trim().Length == 0)
        {
            error = answer.ExitCode == -1 ? "timed out" : $"exit code {answer.ExitCode}: {answer.StdErr.Trim().Split('\n').FirstOrDefault()}";
            return null;
        }

        error = string.Empty;
        var blocks = CodeBlock().Matches(answer.StdOut).Select(m => m.Groups[1].Value).ToList();
        return blocks.Count > 0 ? blocks.MaxBy(b => b.Length) : answer.StdOut;
    }

    /// <summary>Builds and runs the candidate in the scratch copy of the new code, then removes it again.</summary>
    private (string Status, string Detail, List<int> Covers, List<string> Tests) Check(Workbench w, ProposedTest t, string code, int attempt, Dictionary<string, string> mutantTexts)
    {
        var className = $"Aya{t.Request}Tests";
        var dir = Path.Combine(w.Work, Path.GetDirectoryName(t.TestProject!)!, "AyaCandidates");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, t.Request + ".cs");
        File.WriteAllText(file, code);
        try
        {
            var label = Path.Combine("written-tests", $"{t.Request}-try{attempt}");
            var run = TestRunner.Run(w.Work, t.TestProject!, w.RunsDir, label, [className], coverage: t.Reason == "never-executed", w.SdkFolder, 8 * 60 * 1000);
            if (!run.Built)
            {
                return ("rejected-compile", "It does not compile:\n" + string.Join("\n", run.BuildErrors.Take(6)), [], []);
            }

            if (run.Results.Count == 0)
            {
                return ("rejected-compile", $"The test runner found no test in a class named {className}.", [], []);
            }

            var names = run.Results.Select(r => r.Name).ToList();
            if (run.Results.FirstOrDefault(r => r.Failed) is { } failed)
            {
                return ("fails-on-current-code", $"{failed.Name} fails on the code as it is:\n{failed.Message}", [], names);
            }

            if (t.Reason == "never-executed")
            {
                var coverage = run.CoverageFile is null ? null : TestRunner.ParseCoverage(run.CoverageFile, w.Work);
                var hit = t.Lines.Where(l => coverage is not null && coverage.TryGetValue(t.File, out var ls) && ls.TryGetValue(l, out var hits) && hits > 0).ToList();
                if (hit.Count == 0)
                {
                    return ("rejected-no-effect", $"The tests pass, but none of the target lines ({ReviewCommand.Ranges(t.Lines)}) was executed.", [], names);
                }

                var missing = t.Lines.Except(hit).ToList();
                return ("accepted", $"passes and executes line(s) {ReviewCommand.Ranges(hit)}" + (missing.Count > 0 ? $"; still not executed: {ReviewCommand.Ranges(missing)}" : string.Empty), hit, names);
            }

            var broken = w.WithFile(t.File, mutantTexts[t.MutantId!], () =>
                TestRunner.Run(w.Work, t.TestProject!, w.RunsDir, label + "-broken", [className], coverage: false, w.SdkFolder, 8 * 60 * 1000));
            if (!broken.Built)
            {
                return ("rejected-no-effect", "With the line altered the project did not compile, so the test could not be judged.", [], names);
            }

            return broken.Results.Any(r => r.Failed)
                ? ("accepted", "passes on the code as it is and fails when the line is altered", [], names)
                : ("rejected-no-effect", "The tests pass on the code as it is, but they still pass when the line is altered as described.", [], names);
        }
        finally
        {
            File.Delete(file);
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }

    private static string MethodOf(string testName) => testName.IndexOf('(') is var i and > 0 ? testName[..i] : testName;

    [GeneratedRegex(@"```(?:csharp|cs|c#)?[ \t]*\r?\n(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CodeBlock();
}
