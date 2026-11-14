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

    [JsonIgnore]
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>
/// Gets tests written to break the changed units that most deserve it, and lets the old code arbitrate. Who
/// writes them does not matter to the verdict: one model session at a time, a swarm of agents working in
/// parallel, or a person. Whatever the writer says about its own tests is ignored; every file is built and run
/// here, on clean copies of the new and the old code.
/// </summary>
public sealed class Attacker(ModelSession session)
{
    private const int MaxAttempts = 3;
    private const int RunLimitMs = 8 * 60 * 1000;

    public string Name => session.Name;

    /// <summary>One model session per unit, one after another, each with up to three tries at compiling.</summary>
    public List<AttackResult> Run(Workbench w, CodeGraph graph, ReviewResult result, List<ChangedSymbol> targets, string outDir, int max)
    {
        var attacks = Prepare(w, graph, result, targets, outDir);
        var attackDir = Path.Combine(outDir, "attacks");
        w.Trace.Info($"{targets.Count} unit(s) at the top of the attention list; asking {this.Name} to break {Math.Min(max, attacks.Count(a => a.TestProject is not null))}");
        foreach (var a in attacks.Where(a => a.TestProject is not null).Take(max))
        {
            w.Trace.Info($"{a.Request}  {a.Symbol}");
            var prompt = a.Prompt;
            string? code = null;
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                code = session.AskForCode(prompt, out var error);
                a.Attempts = attempt;
                if (code is null)
                {
                    (a.Status, a.Detail) = ("writer-failed", error);
                    w.Trace.Info($"      try {attempt}: the model gave no usable answer — {error}");
                    break;
                }

                if (ModelSession.Trick(code) is { } trick)
                {
                    (a.Status, a.Detail) = ("rejected-artificial", ModelSession.TrickMessage(trick));
                }
                else
                {
                    Judge(w, [(a, code)], $"{a.Request}-try{attempt}");
                }

                w.Trace.Info($"      try {attempt}: {a.Status} — {a.Detail.Split('\n')[0]}");
                if (a.Status is not ("rejected-compile" or "rejected-artificial"))
                {
                    break;
                }

                prompt = a.Prompt +
                    "\n\n## Your previous attempt\n```csharp\n" + code.Trim() + "\n```\n\n" +
                    "## What happened when it was built\n" + a.Detail + "\n\n" +
                    "Fix that and nothing else: keep the expectations as they were. Reply with the whole file in a single csharp code block.";
            }

            Save(a, code, attackDir);
        }

        return attacks;
    }

    /// <summary>
    /// Writes one request per unit and a brief for a swarm of agents, and asks nobody. The swarm works in a copy
    /// of the new code kept for it alone; only the test files it leaves there are ever looked at.
    /// </summary>
    public List<AttackResult> Brief(Workbench w, CodeGraph graph, ReviewResult result, List<ChangedSymbol> targets, string outDir, int max)
    {
        var attacks = Prepare(w, graph, result, targets, outDir);
        var chosen = attacks.Where(a => a.TestProject is not null).Take(max).ToList();
        var brief = Path.Combine(outDir, "attack-requests", "swarm-brief.md");
        File.WriteAllText(brief, SwarmBrief(w, result, chosen, outDir));
        w.Trace.Info($"brief for {chosen.Count} unit(s) written to {brief}");
        w.Trace.Info($"the swarm works in {w.SwarmWork}; collect its files with --attack-from");
        return attacks;
    }

    /// <summary>Takes test files written elsewhere, named A1.cs, A2.cs and so on, and arbitrates them all in one go.</summary>
    public List<AttackResult> RunFrom(Workbench w, CodeGraph graph, ReviewResult result, List<ChangedSymbol> targets, string outDir, int max, string folder)
    {
        // Read before anything else touches a working copy: the folder may be inside one that is cleaned on use.
        var found = !Directory.Exists(folder)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : Directory.EnumerateFiles(folder, "A*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj"))
                .GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => File.ReadAllText(g.First()), StringComparer.OrdinalIgnoreCase);
        w.Trace.Info($"{found.Count} test file(s) found in {folder}");

        var attacks = Prepare(w, graph, result, targets, outDir);
        var attackDir = Path.Combine(outDir, "attacks");
        var items = new List<(AttackResult Attack, string Code)>();
        foreach (var a in attacks.Where(a => a.TestProject is not null).Take(max))
        {
            if (!found.TryGetValue(a.Request, out var code))
            {
                (a.Status, a.Detail) = ("not-written", $"no file named {a.Request}.cs was found");
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
        foreach (var a in attacks.Where(a => a.Attempts > 0))
        {
            w.Trace.Info($"{a.Request}  {a.Symbol}: {a.Status} — {a.Detail.Split('\n')[0]}");
            Save(a, found[a.Request], attackDir);
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

            var failing = new List<(AttackResult Attack, string Code)>();
            foreach (var (a, code) in live)
            {
                var mine = onNew.Results.Where(r => Owns(a, r.Name)).ToList();
                a.Tests = mine.Count;
                if (onNew.TimedOut)
                {
                    (a.Status, a.Detail) = ("inconclusive", "The run was stopped at the time limit.");
                }
                else if (mine.Count == 0)
                {
                    (a.Status, a.Detail) = ("rejected-compile", $"The test runner found no test in a class named {ClassOf(a)}.");
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
                (a.Status, a.Detail) = regressions > 0
                    ? ("regression-demonstrated", $"{regressions} test(s) fail on the new code and pass on the old code: {string.Join(", ", a.Failing.Where(f => f.OnOld == "Passed").Select(f => f.Test))}")
                    : both == a.Failing.Count
                        ? ("fails-on-both", $"{both} test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong")
                        : ("possible-defect", $"{a.Failing.Count} test(s) fail on the new code and the old code cannot judge them (they need code the change added): {string.Join(", ", a.Failing.Select(f => f.Test))}");
            }
        }
    }

    /// <summary>The name of the class a request's tests must be in.</summary>
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

    private static void Save(AttackResult a, string? code, string attackDir)
    {
        if (code is null)
        {
            return;
        }

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
        foreach (var stale in Directory.EnumerateFiles(requestDir, "*.md").Concat(Directory.EnumerateFiles(attackDir, "A*.cs")).ToList())
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
                attack.Prompt = BuildPrompt(w, graph, result, c, attack, pattern);
                File.WriteAllText(Path.Combine(requestDir, attack.Request + ".md"), attack.Prompt);
            }

            attacks.Add(attack);
        }

        return attacks;
    }

    /// <summary>The task handed to a swarm: who does what, where each file goes, and what will be done with it.</summary>
    private static string SwarmBrief(Workbench w, ReviewResult result, List<AttackResult> chosen, string outDir)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Try to break a code change");
        sb.AppendLine();
        sb.AppendLine($"A change to a C# repository{(result.Requirement is null ? string.Empty : $", claiming to implement {result.Requirement},")} has to be attacked: for each unit below,");
        sb.AppendLine("write xUnit tests that assert the right behaviour on inputs where the changed code may be wrong.");
        sb.AppendLine();
        sb.AppendLine($"- Work in `{w.SwarmWork}`, a copy of the repository with the change applied.");
        sb.AppendLine($"- The repository as it was before the change is at `{w.OldWork}`. Read it for reference. Do not edit it.");
        sb.AppendLine();
        sb.AppendLine("## Split the work");
        sb.AppendLine();
        sb.AppendLine("One worker per row, all at the same time, plus one verifier. Each worker reads its request file, which has the");
        sb.AppendLine("code under attack, an existing test to imitate and the rules, and writes exactly one test file.");
        sb.AppendLine();
        sb.AppendLine("| Worker | Unit | Request file | Test file to write |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var a in chosen)
        {
            var target = Path.Combine(w.SwarmWork, Path.GetDirectoryName(a.TestProject!)!, "AyaCandidates", a.Request + ".cs");
            sb.AppendLine($"| @{a.Request.ToLowerInvariant()} | `{a.Symbol.Replace("|", "\\|")}` | `{Path.Combine(outDir, "attack-requests", a.Request + ".md")}` | `{target}` |");
        }

        sb.AppendLine();
        sb.AppendLine("## Rules for every worker");
        sb.AppendLine();
        sb.AppendLine("- Follow your request file exactly, above all the class name it gives.");
        sb.AppendLine("- Create your one test file and nothing else. Every other change in the working copy is thrown away.");
        sb.AppendLine("- You may build and run your own tests to get them to compile, with the command below. Run only your own class.");
        sb.AppendLine("- A test that fails on the changed code is a finding, and is what you are looking for. Never weaken an expectation to make");
        sb.AppendLine("  a test pass. A test that does not compile is worth nothing: make it compile.");
        sb.AppendLine("- Reach the code the way a caller can: no reflection on private members, no uninitialised objects, no emitted types.");
        sb.AppendLine("- What you report about your tests is not used. Another tool builds and runs each file on clean copies of the old and");
        sb.AppendLine("  the new code, and that run decides.");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine($"cd \"{w.SdkFolder ?? w.SwarmWork}\"");
        foreach (var project in chosen.Select(a => a.TestProject!).Distinct())
        {
            sb.AppendLine($"dotnet test \"{Path.Combine(w.SwarmWork, project)}\" --filter \"FullyQualifiedName~Aya<your id>Attack\"");
        }

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## The verifier");
        sb.AppendLine();
        sb.AppendLine("Checks that each worker's file exists at the path in the table, compiles, uses the class name its request gives and");
        sb.AppendLine("breaks none of the rules above, and sends a worker back when it does not. It does not judge whether a failing test is right.");
        sb.AppendLine();
        sb.AppendLine("Finish when every file is in place and compiles, or a worker has given up on its unit and said why.");
        return sb.ToString();
    }

    private static string BuildPrompt(Workbench w, CodeGraph graph, ReviewResult result, ChangedSymbol c, AttackResult attack, Node pattern)
    {
        var source = File.ReadAllLines(Path.Combine(w.HeadWorktree, c.File));
        var span = graph.Nodes[c.Id].Spans.FirstOrDefault(s => string.Equals(s.File, c.File, StringComparison.OrdinalIgnoreCase)) ?? new Span(c.File, attack.Lines.Min(), attack.Lines.Max());

        var sb = new StringBuilder();
        sb.AppendLine("You are reviewing a change to an existing C# repository by trying to break it.");
        sb.AppendLine("Write one xUnit test file. Reply with a single ```csharp code block containing the whole file, and nothing else.");
        sb.AppendLine();
        sb.AppendLine("## Goal");
        sb.AppendLine($"The code below was {(c.ChangeKind == "modified" ? "modified" : "added")} by a change" + (result.Requirement is null ? "." : $" that claims to implement {result.Requirement}."));
        sb.AppendLine("Look for inputs on which it does the wrong thing, and write tests that assert the RIGHT behaviour for those inputs.");
        sb.AppendLine("- A test that fails on the code as it is now is a finding, and is what you are looking for.");
        sb.AppendLine("- Never weaken an expectation to make a test pass, and never assert something you believe is wrong.");
        sb.AppendLine("- For behaviour that existed before the change, right means what it did before, unless changing it is the point of the change.");
        sb.AppendLine("- Write 3 to 6 sharp tests, each aimed at a different way the lines marked `>>` could be wrong.");
        sb.AppendLine("Each test that fails will also be run on the old version of the repository: one that fails only on the new");
        sb.AppendLine("version proves the change broke something. Where you can, use only types and members that existed before the change.");
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

