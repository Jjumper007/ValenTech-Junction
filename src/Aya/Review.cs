using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aya;

public sealed class ChangedSymbol
{
    public required string Id { get; init; }

    public required string Display { get; init; }

    public required string Kind { get; init; }

    public required string File { get; init; }

    public required string Project { get; init; }

    public required string Category { get; init; }

    public int Layer { get; init; }

    /// <summary>added | modified.</summary>
    public required string ChangeKind { get; init; }

    public bool PublicApi { get; init; }

    /// <summary>Test code: a test method, or a helper that lives in a test file.</summary>
    public bool IsTest { get; init; }

    public List<int> ChangedLines { get; init; } = new();

    public List<int> CoveredLines { get; } = new();

    public List<int> UncoveredLines { get; } = new();

    /// <summary>Tests with a static dependency path to this symbol. An upper bound, not proof they exercise it.</summary>
    public int ReachingTests { get; set; }

    public List<string> FailingTests { get; } = new();

    /// <summary>Members that depend on this symbol, directly or indirectly, excluding tests.</summary>
    public int Dependents { get; set; }

    public List<string> PublicDependents { get; } = new();

    /// <summary>verified | partially-verified | unverified | failing | not-measured.</summary>
    public string Status { get; set; } = "not-measured";

    /// <summary>Deliberate breaks tried on this symbol's changed lines. Empty unless mutation was requested.</summary>
    public List<Mutant> Mutants { get; } = new();
}

/// <summary>One test method's result on the new and the old code, for showing tests on the graph.</summary>
public sealed record TestOutcome(string Project, string Method, string OnNew, string? OnOld, string? Verdict);

public sealed class ProbedTest
{
    public required string Name { get; init; }

    public required string Project { get; init; }

    /// <summary>new | modified.</summary>
    public required string Origin { get; init; }

    public required string OnHead { get; init; }

    /// <summary>Outcome against the old code: Passed | Failed | build-failed | not-run.</summary>
    public string OnOldCode { get; set; } = "not-run";

    /// <summary>discriminating | non-discriminating | needs-new-code | unknown.</summary>
    public string Verdict { get; set; } = "unknown";
}

public sealed record Evidence(string Id, string Kind, string Status, string Claim, string Source, string? Detail = null);

public sealed record Finding(string Topic, string Text);

public sealed record ReadItem(string Depth, string What, string Why, int Lines);

public sealed record RunSummary(string Label, string Project, bool Built, int Passed, int Failed, int Total, double Seconds, List<string> BuildErrors);

public sealed class ReviewResult
{
    public string Tool { get; init; } = "aya 0.1.0";

    public string GeneratedAt { get; init; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    public required string Repo { get; init; }

    public string? Requirement { get; init; }

    public required string BaseSha { get; init; }

    public required string HeadRef { get; init; }

    public required string HeadSha { get; init; }

    public string Author { get; set; } = string.Empty;

    public string AuthoredAt { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    /// <summary>The .NET SDK that built and tested both versions of the code.</summary>
    public string Sdk { get; set; } = string.Empty;

    /// <summary>The SDK the repository selects, when the review used a different one; otherwise null.</summary>
    public string? SdkPinnedByRepo { get; set; }

    public List<FileChange> Files { get; set; } = new();

    public List<PatternGroup> Patterns { get; set; } = new();

    public List<string> Projects { get; set; } = new();

    public int GraphNodes { get; set; }

    public int GraphEdges { get; set; }

    public int GraphLayers { get; set; }

    public List<ChangedSymbol> ChangedSymbols { get; set; } = new();

    public List<RunSummary> Runs { get; set; } = new();

    public List<string> Regressions { get; set; } = new();

    public List<ProbedTest> ProbedTests { get; set; } = new();

    public List<Evidence> Evidence { get; set; } = new();

    public List<Finding> NotChecked { get; set; } = new();

    public List<Finding> RefusedToDecide { get; set; } = new();

    public List<ReadItem> ReadingPlan { get; set; } = new();

    public int ChangedLinesTotal { get; set; }

    public int LinesToRead { get; set; }

    public List<string> Limits { get; set; } = new();

    public List<Mutant> Mutants { get; set; } = new();

    public List<ProposedTest> ProposedTests { get; set; } = new();

    /// <summary>Who wrote the proposed tests, e.g. "claude-opus-5-5 (effort medium)". Null when none were asked for.</summary>
    public string? TestWriter { get; set; }

    public List<TestOutcome> Tests { get; set; } = new();
}

public static class ReviewCommand
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<int> RunAsync(Options o)
    {
        var trace = new Trace();
        void Log(string m) => trace.Info(m);

        var repo = ResolveRepo(o.Require("repo"), Log);
        var git = new Git(repo);
        var headRef = o.Require("head");
        var headSha = git.Resolve(headRef);
        var baseSha = o.Get("base") is { } b ? git.Resolve(b) : git.DefaultBase(headSha);
        if (baseSha == headSha)
        {
            throw new AyaException("base and head are the same commit; pass --base explicitly");
        }

        var skipTests = o.Has("skip-tests");
        var sdkMode = o.Get("sdk") ?? "auto";
        if (sdkMode is not ("auto" or "repo" or "latest"))
        {
            throw new AyaException("--sdk must be auto, repo or latest");
        }
        var outDir = Path.GetFullPath(o.Get("out") ?? Path.Combine("aya-out", Sanitize(headRef)));
        Directory.CreateDirectory(outDir);
        var runsDir = Path.Combine(outDir, "runs", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));

        var result = new ReviewResult { Repo = repo, Requirement = o.Get("req"), BaseSha = baseSha, HeadRef = headRef, HeadSha = headSha };
        var commit = git.Run("log", "-1", "--format=%an%x1f%aI%x1f%s", headSha).Trim().Split('\x1f');
        if (commit.Length == 3)
        {
            (result.Author, result.AuthoredAt, result.Subject) = (commit[0], commit[1], commit[2]);
        }

        // 1. What changed, by role.
        trace.Step("Read the change: which files, and what role each one plays");
        trace.Info($"old code {baseSha[..8]}  →  new code {headSha[..8]} ({headRef})");
        var files = DiffParser.Parse(git.Run("diff", "-U0", "--no-color", "-M", baseSha, headSha));
        var worktrees = Path.Combine(Paths.AyaHome, "wt");
        var headWt = git.EnsureWorktree(headSha, Path.Combine(worktrees, headSha[..8]));
        foreach (var f in files)
        {
            f.Category = DiffParser.Categorize(headWt, f.Path);
        }

        result.Files = files;
        result.Patterns = DiffParser.GroupPatterns(files);
        result.ChangedLinesTotal = files.Sum(f => f.Added + f.Deleted);
        trace.Info($"{files.Count} files, {result.ChangedLinesTotal} changed lines");
        foreach (var g in files.GroupBy(f => f.Category).OrderByDescending(g => g.Count()))
        {
            trace.Info($"{g.Key,-14} {g.Count(),4} file(s)  +{g.Sum(f => f.Added)} -{g.Sum(f => f.Deleted)}");
            trace.List(g.Select(f => "    " + f.Path), 4);
        }

        foreach (var p in result.Patterns.Where(p => p.Files.Count > 1))
        {
            trace.Info($"same edit in {p.Files.Count} {p.Category} files (pattern {p.Id}): {string.Join("  ", p.Edits.Take(3))}");
        }

        // 2. Which projects the change touches, and which test projects depend on them.
        trace.Step("Find the projects the change touches and the test projects that depend on them");
        var references = ProjectMap.References(headWt);
        var touched = files
            .Where(f => f.Status != "D" && Path.GetExtension(f.Path) is ".cs" or ".tt" or ".csproj")
            .Select(f => ProjectMap.NearestProject(headWt, f.Path))
            .Where(p => p is not null)
            .Select(p => p!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var projects = touched
            .Concat(ProjectMap.Dependents(references, touched).Where(Paths.IsTestPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var testProjects = projects.Where(p => Paths.IsTestPath(p) && HasTests(Path.GetDirectoryName(Path.Combine(headWt, p))!)).ToList();
        trace.Info("touched directly:");
        trace.List(touched.Select(p => "    " + p));
        trace.Info("test projects that depend on them:");
        trace.List(testProjects.Select(p => "    " + p));

        // Which .NET SDK builds the code. dotnet picks it from the global.json above the folder it is launched
        // from: the worktree honours the repo's pin, a neutral folder gives the newest SDK installed.
        var neutral = Path.Combine(Paths.AyaHome, "sdk-latest");
        Directory.CreateDirectory(neutral);
        var pinnedSdk = Proc.Run(Dotnet.Exe, ["--version"], headWt).StdOut.Trim();
        var latestSdk = Proc.Run(Dotnet.Exe, ["--version"], neutral).StdOut.Trim();
        string? sdkFolder = sdkMode == "latest" && latestSdk != pinnedSdk ? neutral : null;

        // 3. Run the suite on the new code, recording which lines execute.
        var headRuns = new List<TestRun>();
        if (skipTests)
        {
            trace.Step("Restore packages (tests skipped on request)");
            foreach (var p in projects)
            {
                Proc.Run(Dotnet.Exe, ["restore", Path.Combine(headWt, p), "--nologo", "-v", "q"], sdkFolder ?? headWt);
            }
        }
        else
        {
            trace.Step("Run the tests on the NEW code, recording which lines execute");
            trace.Info($".NET SDK {(sdkFolder is null ? pinnedSdk : latestSdk)}" + (sdkFolder is null ? " (the one the repo selects)" : $" (newest installed; the repo selects {pinnedSdk})"));
            foreach (var p in testProjects)
            {
                var run = TestRunner.Run(headWt, p, runsDir, "head", null, coverage: true, sdkFolder);
                headRuns.Add(run);
                trace.Info(Describe(run));
            }

            // A generator built against a newer compiler than the pinned SDK ships is silently skipped, and
            // every project that needs its output fails to compile. A newer SDK fixes that for old and new alike.
            if (sdkMode == "auto" && sdkFolder is null && latestSdk != pinnedSdk && headRuns.Any(r => !r.Built && r.NeedsNewerCompiler))
            {
                sdkFolder = neutral;
                trace.Info($"some projects did not compile: a source generator needs a newer compiler than SDK {pinnedSdk} has");
                trace.Info($"switching to SDK {latestSdk} for every run, old and new code alike, and repeating this step");
                headRuns.Clear();
                foreach (var p in testProjects)
                {
                    // A separate folder, so results and coverage of the discarded attempt are not read back.
                    var run = TestRunner.Run(headWt, p, runsDir + "-newer-sdk", "head", null, coverage: true, sdkFolder);
                    headRuns.Add(run);
                    trace.Info(Describe(run));
                }
            }
        }

        result.Sdk = sdkFolder is null ? pinnedSdk : latestSdk;
        result.SdkPinnedByRepo = sdkFolder is null ? null : pinnedSdk;

        // 4. Dependency graph of the new code.
        trace.Step("Break the new code into its smallest units and link them (dependency graph)");
        CodeGraph graph;
        try
        {
            graph = await GraphBuilder.BuildAsync(headWt, projects.Select(p => Path.Combine(headWt, p)).ToList(), Log);
        }
        catch (Exception ex)
        {
            graph = new CodeGraph();
            graph.Warnings.Add("graph construction failed: " + ex.Message);
        }

        result.Projects = graph.Projects;
        result.GraphNodes = graph.Nodes.Count;
        result.GraphEdges = graph.Edges.Count;
        result.GraphLayers = graph.LayerCount;
        trace.Info($"units (nodes): {graph.Nodes.Count}  =  " + string.Join(", ", graph.Nodes.Values.GroupBy(n => n.Kind).OrderByDescending(g => g.Count()).Select(g => $"{g.Count()} {g.Key}")));
        trace.Info($"links (edges): {graph.Edges.Count}  =  " + string.Join(", ", graph.Edges.GroupBy(e => e.Kind).OrderByDescending(g => g.Count()).Select(g => $"{g.Count()} {g.Key}")));
        var cycles = graph.Nodes.Values.GroupBy(n => n.Scc).Where(g => g.Count() > 1).ToList();
        trace.Info($"cycles collapsed: {cycles.Count} groups of units that depend on each other" + (cycles.Count > 0 ? $" (largest has {cycles.Max(g => g.Count())} units)" : string.Empty));
        trace.Info($"layers: {graph.LayerCount}  (layer 0 depends on nothing else; each layer depends only on lower ones)");
        var perLayer = graph.Nodes.Values.GroupBy(n => n.Layer).OrderBy(g => g.Key).ToList();
        trace.Info("units per layer: " + string.Join("  ", perLayer.Take(8).Select(g => $"L{g.Key}={g.Count()}")) + (perLayer.Count > 8 ? $"  …  L{perLayer[^1].Key}={perLayer[^1].Count()}" : string.Empty));
        trace.Info($"test methods found: {graph.Nodes.Values.Count(n => n.IsTest)}");
        if (graph.Warnings.Count > 0)
        {
            trace.Info($"warnings ({graph.Warnings.Count}): the graph may be incomplete");
            trace.List(graph.Warnings.Select(w => "    " + w), 3);
        }

        trace.Step("Locate the changed lines in the graph: which units own them");
        var changed = MapChanges(graph, files);
        result.ChangedSymbols = changed;
        trace.List(changed.Select(c => $"{c.File.Split('/').Last()}:{Ranges(c.ChangedLines)}  →  {(c.IsTest ? "test " : string.Empty)}{c.Kind} {c.Display}  [layer {c.Layer}, {c.ChangeKind}]"), 40);
        var unmapped = files.Where(f => Path.GetExtension(f.Path) == ".cs" && changed.All(c => c.File != f.Path)).ToList();
        if (unmapped.Count > 0)
        {
            trace.Info($"{unmapped.Count} changed .cs file(s) are not compiled code (expected-output files or excluded from the build), so they own no unit");
        }

        // 5. Run the suite on the old code, to tell regressions from pre-existing failures.
        var baseRuns = new List<TestRun>();
        var probeRuns = new List<TestRun>();
        if (!skipTests)
        {
            trace.Step("Run the same tests on the OLD code and compare, test by test");
            var baseWt = git.EnsureWorktree(baseSha, Path.Combine(worktrees, baseSha[..8]));
            foreach (var p in testProjects.Where(p => File.Exists(Path.Combine(baseWt, p))))
            {
                var run = TestRunner.Run(baseWt, p, runsDir, "base", null, coverage: false, sdkFolder);
                baseRuns.Add(run);
                trace.Info(Describe(run));
            }

            var early = CompareRuns(headRuns, baseRuns);
            trace.Info($"tests present in both: {early.Existing}  |  pass in both: {early.StillPassing}  |  broken by the change: {early.Regressions.Count}  |  only in the new code: {early.NewKeys.Count}");
            trace.List(early.Regressions.Select(r => "    REGRESSION " + r));

            // 6. Do the tests that came with the change fail without it? Run them against the old code.
            trace.Step("Run the change's own tests against the OLD code: do they fail without the change?");
            var newKeys = early.NewKeys;
            result.ProbedTests = SelectProbes(headRuns, baseRuns, newKeys, changed, graph);
            if (result.ProbedTests.Count == 0)
            {
                trace.Info("the change brought no new or modified test");
            }

            if (result.ProbedTests.Count > 0)
            {
                var probeWt = git.EnsureWorktree(baseSha, Path.Combine(worktrees, baseSha[..6] + headSha[..6]));
                var testFiles = files.Where(f => f.Status != "D" && Paths.IsTestPath(f.Path)).Select(f => f.Path).ToList();
                var spec = Path.Combine(runsDir, "probe-pathspec.txt");
                Directory.CreateDirectory(runsDir);
                File.WriteAllLines(spec, testFiles);
                new Git(probeWt).Run("checkout", headSha, "--pathspec-from-file=" + spec);
                trace.Info($"took the old code and copied in only the {testFiles.Count} test file(s) of the change");

                foreach (var group in result.ProbedTests.GroupBy(t => t.Project))
                {
                    var project = testProjects.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == group.Key);
                    if (project is null)
                    {
                        continue;
                    }

                    var methods = group.Select(t => MethodOf(t.Name)).Distinct().ToList();
                    var run = TestRunner.Run(probeWt, project, runsDir, "probe", methods, coverage: false, sdkFolder);
                    probeRuns.Add(run);
                    trace.Info(Describe(run));
                    foreach (var t in group)
                    {
                        var r = run.Results.FirstOrDefault(x => x.Name == t.Name);
                        (t.OnOldCode, t.Verdict) = (run.Built, r) switch
                        {
                            (false, _) => ("build-failed", "needs-new-code"),
                            (_, { Failed: true }) => ("Failed", "discriminating"),
                            (_, { Passed: true }) => ("Passed", "non-discriminating"),
                            _ => ("not-run", "unknown"),
                        };
                        trace.Info($"    {t.Name}: {t.OnOldCode} on old code  →  {t.Verdict}");
                    }
                }
            }
        }

        result.Runs = headRuns.Concat(baseRuns).Concat(probeRuns)
            .Select(r => new RunSummary(r.Label, r.Project, r.Built, r.PassedCount, r.FailedCount, r.Results.Count, r.Seconds, r.BuildErrors))
            .ToList();

        // 7. Attribute the results to each changed symbol, lowest layer first.
        trace.Step("Attach the results to each changed unit, from the lowest layer up");
        var coverage = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase);
        var coverageByProject = new Dictionary<string, Dictionary<string, Dictionary<int, int>>>(StringComparer.Ordinal);
        foreach (var run in headRuns.Where(r => r.CoverageFile is not null))
        {
            var ofThisProject = TestRunner.ParseCoverage(run.CoverageFile!, headWt);
            coverageByProject[run.Project] = ofThisProject;
            foreach (var (file, lines) in ofThisProject)
            {
                if (!coverage.TryGetValue(file, out var merged))
                {
                    coverage[file] = merged = new Dictionary<int, int>();
                }

                foreach (var (line, hits) in lines)
                {
                    merged[line] = merged.TryGetValue(line, out var e) ? Math.Max(e, hits) : hits;
                }
            }
        }

        var comparison = CompareRuns(headRuns, baseRuns);
        result.Regressions = comparison.Regressions;
        var failedMethods = headRuns.SelectMany(r => r.Results).Where(r => r.Failed).Select(r => r.Method).ToHashSet(StringComparer.Ordinal);

        foreach (var c in changed)
        {
            var dependents = graph.Reach([c.Id], reverse: true);
            dependents.Remove(c.Id);
            var reachingTests = dependents.Keys.Select(id => graph.Nodes[id]).Where(n => n.IsTest).ToList();
            c.ReachingTests = reachingTests.Count;
            c.FailingTests.AddRange(reachingTests.Where(t => t.TestName is not null && failedMethods.Contains(t.TestName)).Select(t => t.TestName!).OrderBy(n => n, StringComparer.Ordinal));
            var members = dependents.Where(kv => graph.Nodes[kv.Key] is { IsTest: false, Kind: not "type" }).ToList();
            c.Dependents = members.Count;
            c.PublicDependents.AddRange(members.Where(kv => graph.Nodes[kv.Key].IsPublicApi).OrderBy(kv => kv.Value).Take(5).Select(kv => graph.Nodes[kv.Key].Display));

            if (c.IsTest)
            {
                c.Status = failedMethods.Contains(graph.Nodes[c.Id].TestName ?? string.Empty) ? "failing" : "not-measured";
                continue;
            }

            // A constant has no code to run: the compiler copies its value into every use. The coverage tool
            // still lists its line as never executed, which would be a false gap.
            if (headRuns.Count == 0 || coverage.Count == 0 || graph.Nodes[c.Id].IsConstant)
            {
                continue;
            }

            if (coverage.TryGetValue(c.File, out var lines))
            {
                foreach (var line in c.ChangedLines)
                {
                    if (lines.TryGetValue(line, out var hits))
                    {
                        (hits > 0 ? c.CoveredLines : c.UncoveredLines).Add(line);
                    }
                }
            }
            else
            {
                // The file never appeared in the coverage report: no test loaded this code at all.
                c.UncoveredLines.AddRange(c.ChangedLines);
            }

            c.Status = c switch
            {
                { FailingTests.Count: > 0 } => "failing",
                { UncoveredLines.Count: 0, CoveredLines.Count: > 0 } => "verified",
                { UncoveredLines.Count: > 0, CoveredLines.Count: > 0 } => "partially-verified",
                { UncoveredLines.Count: > 0 } => "unverified",
                _ => "not-measured",
            };
        }

        trace.Info($"coverage data for {coverage.Count} source files");
        foreach (var c in changed.Where(c => !c.IsTest))
        {
            trace.Info($"layer {c.Layer,2}  {c.Display}");
            trace.Info($"          changed lines run {c.CoveredLines.Count}, not run {c.UncoveredLines.Count}{(c.UncoveredLines.Count > 0 ? " (" + Ranges(c.UncoveredLines) + ")" : string.Empty)}  |  {c.Dependents} units depend on it  |  {c.ReachingTests} tests can reach it  →  {c.Status}");
        }

        result.Tests = OutcomesByMethod(headRuns, baseRuns, result.ProbedTests);

        // Optional deeper stages. Both work on a second copy of the new code and put every file back.
        if (!skipTests && (o.Has("mutate") || o.Has("write-tests")))
        {
            var bench = new Workbench
            {
                Git = git,
                HeadSha = headSha,
                HeadWorktree = headWt,
                WorktreesRoot = worktrees,
                TestProjects = testProjects,
                RunsDir = runsDir,
                SdkFolder = sdkFolder,
                Trace = trace,
                CoverageByProject = coverageByProject,
                FailingOnHead = headRuns.SelectMany(r => r.Results).Where(r => r.Failed).Select(r => r.Project + "::" + r.Method).ToHashSet(StringComparer.Ordinal),
            };
            var mutantTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            if (o.Has("mutate"))
            {
                trace.Step("Break each changed line on purpose and see whether any test notices (mutation testing)");
                (result.Mutants, mutantTexts) = MutationRunner.Run(bench, changed, int.TryParse(o.Get("max-mutants"), out var mm) ? mm : 20);
            }

            if (o.Has("write-tests"))
            {
                trace.Step("Ask a model for tests that fill the gaps, and check every one before believing it");
                var writer = new TestWriter(o.Get("writer-model") ?? "claude-opus-5-5", o.Get("writer-effort") ?? "medium");
                result.TestWriter = writer.Name;
                result.ProposedTests = writer.Run(bench, graph, changed, result, mutantTexts, outDir, int.TryParse(o.Get("max-tests"), out var mt) ? mt : 4);
            }
        }

        trace.Step("Turn the results into evidence and a reading plan, and write the report");
        BuildEvidence(result, graph, headRuns, baseRuns, comparison, skipTests);
        BuildFindings(result, graph, testProjects, skipTests);
        BuildReadingPlan(result);
        trace.List(result.Evidence.Select(e => $"{e.Id,-4} {e.Status.ToUpperInvariant(),-5} {e.Claim}"), 30);
        trace.Info($"not checked: {result.NotChecked.Count} item(s)  |  refused to decide: {result.RefusedToDecide.Count} item(s)");
        trace.Info($"a person still has to read {result.LinesToRead} of {result.ChangedLinesTotal} changed lines");

        // 8. Write the artefacts. The report quotes the hash of the evidence file it was derived from.
        var evidenceJson = JsonSerializer.Serialize(result, Json);
        var evidencePath = Path.Combine(outDir, "evidence.json");
        File.WriteAllText(evidencePath, evidenceJson);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidenceJson))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(outDir, "graph.json"), JsonSerializer.Serialize(GraphExport(graph, changed), Json));
        File.WriteAllText(Path.Combine(outDir, "report.md"), Report.Render(result, hash));
        trace.Info($"wrote report.md, evidence.json, graph.json and pipeline.log to {outDir}");
        trace.Save(Path.Combine(outDir, "pipeline.log"));
        Console.Error.WriteLine();

        Console.WriteLine($"Change      {result.HeadRef} ({headSha[..8]}) — {result.Subject}");
        Console.WriteLine($"Files       {files.Count} changed, {result.ChangedLinesTotal} lines, {result.Patterns.Count} distinct edit patterns");
        Console.WriteLine($"Graph       {graph.Nodes.Count} nodes, {graph.Edges.Count} edges, {graph.LayerCount} layers");
        Console.WriteLine($"Symbols     {changed.Count} changed: " + string.Join(", ", changed.GroupBy(c => c.Status).Select(g => $"{g.Count()} {g.Key}")));
        foreach (var e in result.Evidence)
        {
            Console.WriteLine($"{e.Id,-4} {e.Status.ToUpperInvariant(),-5} {e.Claim}");
        }

        Console.WriteLine($"Human read  {result.LinesToRead} of {result.ChangedLinesTotal} changed lines");
        Console.WriteLine($"Report      {Path.Combine(outDir, "report.md")}");
        return result.Evidence.Any(e => e.Status == "fail") ? 1 : 0;
    }

    /// <summary>Accepts a local path or a clone URL; URLs are cloned (or refreshed) under the AYA home folder.</summary>
    private static string ResolveRepo(string arg, Action<string> log)
    {
        if (Directory.Exists(arg))
        {
            return Path.GetFullPath(arg);
        }

        if (!arg.Contains("://", StringComparison.Ordinal) && !arg.StartsWith("git@", StringComparison.Ordinal))
        {
            throw new AyaException($"'{arg}' is neither an existing folder nor a repository URL");
        }

        var name = Path.GetFileNameWithoutExtension(arg.TrimEnd('/').Split('/', ':').Last());
        var dest = Path.Combine(Paths.AyaHome, "repos", name);
        if (Directory.Exists(dest))
        {
            log($"fetching {name}");
            new Git(dest).Try("fetch", "--all", "--prune", "--quiet");
        }
        else
        {
            log($"cloning {arg}");
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            var clone = Proc.Run("git", ["clone", "-c", "core.longpaths=true", "--quiet", arg, dest]);
            if (!clone.Ok)
            {
                throw new AyaException("git clone failed: " + clone.StdErr.Trim());
            }
        }

        return dest;
    }

    private static bool HasTests(string projectDir) =>
        Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "Resources" + Path.DirectorySeparatorChar))
            .Any(f =>
            {
                var text = File.ReadAllText(f);
                return text.Contains("[Fact", StringComparison.Ordinal) || text.Contains("[Theory", StringComparison.Ordinal) || text.Contains("[Test", StringComparison.Ordinal);
            });

    /// <summary>Collapses data-driven rows into one outcome per test method: failed if any row failed.</summary>
    private static List<TestOutcome> OutcomesByMethod(List<TestRun> headRuns, List<TestRun> baseRuns, List<ProbedTest> probes)
    {
        static string Worst(IEnumerable<TestResult> rows) =>
            rows.Any(r => r.Failed) ? "Failed" : rows.Any(r => r.Passed) ? "Passed" : "Skipped";

        var before = baseRuns.SelectMany(r => r.Results).ToLookup(r => r.Project + "::" + r.Method);
        var verdicts = probes.ToLookup(p => p.Project + "::" + MethodOf(p.Name), p => p.Verdict);
        return headRuns.SelectMany(r => r.Results)
            .GroupBy(r => (r.Project, r.Method))
            .Select(g =>
            {
                var key = g.Key.Project + "::" + g.Key.Method;
                var v = verdicts[key].ToList();
                // The least reassuring verdict among a method's rows stands for the method.
                var verdict = new[] { "non-discriminating", "unknown", "needs-new-code", "discriminating" }.FirstOrDefault(v.Contains);
                return new TestOutcome(g.Key.Project, g.Key.Method, Worst(g), before.Contains(key) ? Worst(before[key]) : null, verdict);
            })
            .ToList();
    }

    private static string Describe(TestRun run) =>
        run.Built
            ? $"{run.Project,-46} {run.PassedCount,5} passed {run.FailedCount,3} failed {run.Results.Count - run.PassedCount - run.FailedCount,3} skipped   {run.Seconds}s"
            : $"{run.Project,-46} DID NOT COMPILE: {run.BuildErrors.FirstOrDefault() ?? "see dotnet-test.log"}";

    private static string MethodOf(string testName) => testName.IndexOf('(') is var i and > 0 ? testName[..i] : testName;

    private static string Sanitize(string s) => string.Concat(s.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_'));

    /// <summary>Maps changed line ranges onto the innermost declaration that contains them.</summary>
    private static List<ChangedSymbol> MapChanges(CodeGraph graph, List<FileChange> files)
    {
        var byFile = graph.Nodes.Values
            .SelectMany(n => n.Spans.Select(s => (Node: n, Span: s)))
            .GroupBy(x => x.Span.File, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var lines = new Dictionary<string, (Node Node, FileChange File, SortedSet<int> Lines, bool Deletion)>(StringComparer.Ordinal);
        foreach (var f in files)
        {
            if (!byFile.TryGetValue(f.Path, out var spans))
            {
                continue;
            }

            foreach (var hunk in f.Hunks)
            {
                // A pure deletion has no new lines; anchor it to the line where the text used to be.
                var touched = hunk.NewLen > 0 ? Enumerable.Range(hunk.NewStart, hunk.NewLen) : [Math.Max(1, hunk.NewStart)];
                foreach (var line in touched)
                {
                    // Blank lines separate declarations; they belong to none of them.
                    var offset = line - hunk.NewStart;
                    if (hunk.NewLen > 0 && offset < hunk.Added.Count && string.IsNullOrWhiteSpace(hunk.Added[offset]))
                    {
                        continue;
                    }

                    var owner = spans
                        .Where(x => x.Span.Start <= line && line <= x.Span.End)
                        .OrderBy(x => x.Node.Kind == "type" ? 1 : 0)
                        .ThenBy(x => x.Span.End - x.Span.Start)
                        .Select(x => x.Node)
                        .FirstOrDefault();
                    if (owner is null)
                    {
                        continue;
                    }

                    if (!lines.TryGetValue(owner.Id, out var entry))
                    {
                        entry = (owner, f, new SortedSet<int>(), false);
                    }

                    if (hunk.NewLen > 0)
                    {
                        entry.Lines.Add(line);
                    }
                    else
                    {
                        entry.Deletion = true;
                    }

                    lines[owner.Id] = entry;
                }
            }
        }

        return lines.Values
            .Select(e =>
            {
                // New when every line of the declaration, blank ones included, was added by this change.
                var added = e.File.AddedLines().ToHashSet();
                var wholly = !e.Deletion && e.Node.Spans
                    .Where(s => string.Equals(s.File, e.File.Path, StringComparison.OrdinalIgnoreCase))
                    .All(s => Enumerable.Range(s.Start, s.End - s.Start + 1).All(added.Contains));
                return new ChangedSymbol
                {
                    Id = e.Node.Id,
                    Display = e.Node.Display,
                    Kind = e.Node.Kind,
                    File = e.File.Path,
                    Project = e.Node.Project,
                    Category = e.File.Category,
                    Layer = e.Node.Layer,
                    ChangeKind = wholly || e.File.Status == "A" ? "added" : "modified",
                    PublicApi = e.Node.IsPublicApi,
                    IsTest = e.Node.IsTest || e.File.Category == "test",
                    ChangedLines = e.Lines.ToList(),
                };
            })
            .OrderBy(c => c.IsTest)
            .ThenBy(c => c.Layer)
            .ThenBy(c => c.Display, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Tests that arrived with the change (new) or were edited by it (modified).</summary>
    private static List<ProbedTest> SelectProbes(List<TestRun> headRuns, List<TestRun> baseRuns, HashSet<string> newKeys, List<ChangedSymbol> changed, CodeGraph graph)
    {
        var builtBefore = baseRuns.Where(r => r.Built).Select(r => r.Project).ToHashSet(StringComparer.Ordinal);
        var modified = changed
            .Where(c => c.IsTest && c.ChangeKind == "modified")
            .Select(c => graph.Nodes[c.Id].TestName)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal);

        var probes = new List<ProbedTest>();
        foreach (var r in headRuns.SelectMany(run => run.Results))
        {
            var isNew = newKeys.Contains(r.Key) && (builtBefore.Contains(r.Project) || baseRuns.All(b => b.Project != r.Project));
            if (isNew || modified.Contains(r.Method))
            {
                probes.Add(new ProbedTest { Name = r.Name, Project = r.Project, Origin = isNew ? "new" : "modified", OnHead = r.Outcome });
            }
        }

        return probes.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>The outcome of comparing the suite on old and new code.</summary>
    private sealed class RunComparison
    {
        public int Existing { get; set; }

        public int StillPassing { get; set; }

        /// <summary>Rows whose name differs between runs (random data), compared per test method instead.</summary>
        public int UnstableRows { get; set; }

        public List<string> Regressions { get; } = new();

        public List<string> Removed { get; } = new();

        public HashSet<string> NewKeys { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Matches tests between the two runs by exact name. Data-driven tests fed random values get a different
    /// name on every run, so rows left unmatched are compared per test method by pass and fail counts.
    /// </summary>
    private static RunComparison CompareRuns(List<TestRun> headRuns, List<TestRun> baseRuns)
    {
        static string MethodKey(TestResult r) => r.Project + "::" + r.Method;

        var head = headRuns.SelectMany(r => r.Results).ToList();
        var before = baseRuns.SelectMany(r => r.Results).ToList();
        var headByKey = head.GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.First());
        var baseByKey = before.GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.First());
        var headLoose = head.Where(r => !baseByKey.ContainsKey(r.Key)).ToLookup(MethodKey);
        var baseLoose = before.Where(r => !headByKey.ContainsKey(r.Key)).ToLookup(MethodKey);
        var baseMethods = before.Select(MethodKey).ToHashSet(StringComparer.Ordinal);
        var headMethods = head.Select(MethodKey).ToHashSet(StringComparer.Ordinal);

        var c = new RunComparison();
        foreach (var r in headByKey.Values.Where(r => baseByKey.ContainsKey(r.Key)))
        {
            c.Existing++;
            var old = baseByKey[r.Key];
            if (r.Passed && old.Passed)
            {
                c.StillPassing++;
            }
            else if (r.Failed && old.Passed)
            {
                c.Regressions.Add(r.Name);
            }
        }

        foreach (var group in headLoose)
        {
            if (!baseMethods.Contains(group.Key))
            {
                c.NewKeys.UnionWith(group.Select(r => r.Key));
                continue;
            }

            var rows = group.ToList();
            var oldRows = baseLoose[group.Key].ToList();
            c.Existing += rows.Count;
            c.UnstableRows += rows.Count;
            c.StillPassing += Math.Min(rows.Count(r => r.Passed), oldRows.Count(r => r.Passed));
            if (rows.Count(r => r.Failed) > oldRows.Count(r => r.Failed))
            {
                c.Regressions.Add(rows[0].Method + " (rows named after random data)");
            }
        }

        c.Removed.AddRange(before.Where(r => !headByKey.ContainsKey(r.Key) && !headMethods.Contains(MethodKey(r))).Select(r => r.Name));
        c.Regressions.Sort(StringComparer.Ordinal);
        return c;
    }

    private static void BuildEvidence(
        ReviewResult result,
        CodeGraph graph,
        List<TestRun> headRuns,
        List<TestRun> baseRuns,
        RunComparison comparison,
        bool skipTests)
    {
        var list = result.Evidence;
        string Next() => "E" + (list.Count + 1);

        var bigPatterns = result.Patterns.Where(p => p.Files.Count > 1).ToList();
        list.Add(new Evidence(
            Next(),
            "change-shape",
            "info",
            $"{result.Files.Count} changed files reduce to {result.Patterns.Count} distinct edit patterns" + (bigPatterns.Count > 0 ? $"; the largest covers {bigPatterns[0].Files.Count} files" : string.Empty),
            "git diff + exact line-edit grouping (deterministic)"));

        var source = result.ChangedSymbols.Where(c => !c.IsTest).ToList();
        list.Add(new Evidence(
            Next(),
            "impact",
            graph.Warnings.Count > 0 ? "warn" : "info",
            $"{source.Count} non-test symbols changed across layers {(source.Count > 0 ? $"{source.Min(c => c.Layer)}–{source.Max(c => c.Layer)}" : "n/a")}; {source.Sum(c => c.Dependents)} dependent members and {source.Count(c => c.PublicApi)} public API symbols touched",
            $"Roslyn semantic model over {graph.Projects.Count} projects ({graph.Nodes.Count} nodes, {graph.Edges.Count} edges)",
            graph.Warnings.Count > 0 ? $"{graph.Warnings.Count} loader warnings: the graph may be missing edges" : null));

        if (skipTests)
        {
            list.Add(new Evidence(Next(), "tests", "warn", "No test was run (--skip-tests)", "n/a"));
            return;
        }

        if (result.SdkPinnedByRepo is not null)
        {
            list.Add(new Evidence(
                Next(),
                "environment",
                "info",
                $"Old and new code were both built with .NET SDK {result.Sdk}, not the {result.SdkPinnedByRepo} the repository selects, because its source generator needs a newer compiler",
                "dotnet --version; compiler warning CS9057"));
        }

        var built = headRuns.Where(r => r.Built).ToList();
        if (built.Count > 0)
        {
            list.Add(new Evidence(Next(), "build", "pass", $"{built.Count} test project(s) and their dependencies compile with the change", "dotnet build (new code)"));
        }

        foreach (var run in headRuns.Where(r => !r.Built))
        {
            // A project that did not compile before the change either says nothing about the change.
            var brokenBefore = baseRuns.Any(b => b.Project == run.Project && !b.Built);
            list.Add(new Evidence(
                Next(),
                "build",
                brokenBefore ? "warn" : "fail",
                brokenBefore
                    ? $"{run.Project} compiles on neither old nor new code in this environment, so its tests gave no evidence"
                    : $"{run.Project} compiled before the change and does not compile with it",
                "dotnet build on old and new code",
                string.Join("\n", run.BuildErrors.Take(3))));
        }

        list.Add(new Evidence(
            Next(),
            "regression",
            comparison.Regressions.Count > 0 ? "fail" : comparison.Existing == 0 ? "warn" : "pass",
            comparison.Existing == 0
                ? "No pre-existing test was available to compare old and new behaviour"
                : $"{comparison.StillPassing} of {comparison.Existing} pre-existing tests pass on both old and new code; {comparison.Regressions.Count} regressed",
            "dotnet test on old and new code, compared test by test",
            comparison.Regressions.Count > 0 ? string.Join("\n", comparison.Regressions.Take(10)) : null));

        if (comparison.UnstableRows > 0)
        {
            list.Add(new Evidence(
                Next(),
                "regression",
                "warn",
                $"{comparison.UnstableRows} of those tests are fed random data, so their inputs differ on every run; they were compared per test method, not input by input",
                "dotnet test on old and new code"));
        }

        if (comparison.Removed.Count > 0)
        {
            list.Add(new Evidence(Next(), "regression", "warn", $"{comparison.Removed.Count} test(s) that ran on the old code no longer run on the new code", "dotnet test on old and new code", string.Join("\n", comparison.Removed.Take(10))));
        }

        var probes = result.ProbedTests;
        if (probes.Count > 0)
        {
            var failingNew = probes.Count(p => p.OnHead == "Failed");
            list.Add(new Evidence(
                Next(),
                "new-tests",
                failingNew > 0 ? "fail" : "pass",
                $"{probes.Count(p => p.Origin == "new")} new and {probes.Count(p => p.Origin == "modified")} modified tests; {probes.Count - failingNew} pass on the new code",
                "dotnet test (new code)"));

            var discriminating = probes.Count(p => p.Verdict == "discriminating");
            var weak = probes.Where(p => p.Verdict == "non-discriminating").ToList();
            var needNew = probes.Count(p => p.Verdict == "needs-new-code");
            list.Add(new Evidence(
                Next(),
                "fail-before",
                weak.Count > 0 ? "warn" : "pass",
                $"{discriminating} of {probes.Count} of those tests fail on the old code (they demonstrate the change); {weak.Count} pass without it; {needNew} cannot compile without it",
                "dotnet test on old source with the change's test files applied",
                weak.Count > 0 ? "Pass without the change:\n" + string.Join("\n", weak.Take(15).Select(p => p.Name)) : null));
        }
        else
        {
            list.Add(new Evidence(Next(), "new-tests", "warn", "The change adds or modifies no test", "dotnet test on old and new code"));
        }

        var covered = source.Sum(c => c.CoveredLines.Count);
        var uncovered = source.Sum(c => c.UncoveredLines.Count);
        list.Add(new Evidence(
            Next(),
            "coverage",
            covered + uncovered == 0 ? "warn" : uncovered > 0 ? "warn" : "pass",
            covered + uncovered == 0
                ? "No changed executable source line was measured"
                : $"{covered} of {covered + uncovered} changed executable source lines were executed by the test suite; {uncovered} were never executed",
            "Microsoft Code Coverage during dotnet test (new code)"));

        if (result.Mutants.Count > 0)
        {
            var caught = result.Mutants.Count(m => m.Status is "killed" or "timeout");
            var survived = result.Mutants.Count(m => m.Status == "survived");
            var blind = result.Mutants.Count(m => m.Status == "not-covered");
            var skipped = result.Mutants.Count(m => m.Status == "not-run");
            list.Add(new Evidence(
                Next(),
                "mutation",
                survived > 0 ? "warn" : caught > 0 ? "pass" : "warn",
                $"{caught} of {caught + survived} deliberate breaks of changed lines were caught by a test; {survived} went unnoticed" +
                (blind > 0 ? $"; {blind} more are on lines no test executes" : string.Empty) +
                (skipped > 0 ? $"; {skipped} were not tried (budget)" : string.Empty),
                "each changed line altered in turn, then dotnet test (mutation testing)",
                survived > 0 ? string.Join("\n", result.Mutants.Where(m => m.Status == "survived").Take(12).Select(m => $"{m.File}:{m.Line}  {m.Description}  in  {m.Original}")) : null));
        }

        if (result.ProposedTests.Count > 0)
        {
            var tried = result.ProposedTests.Where(t => t.Status != "not-attempted").ToList();
            var accepted = tried.Where(t => t.Status == "accepted").ToList();
            var closed = tried.Count(t => t.Reason == "unnoticed-break" && t.Status is "accepted" or "caught-by-other");
            var asked = tried.Count(t => t.Attempts > 0);
            list.Add(new Evidence(
                Next(),
                "written-tests",
                "info",
                $"{accepted.Count} of {asked} tests written by {result.TestWriter} were accepted: they compile, pass, and now execute {accepted.Sum(t => t.CoversLines.Count)} previously unexecuted line(s) and catch {closed} of {tried.Count(t => t.Reason == "unnoticed-break")} previously unnoticed break(s)",
                $"{result.TestWriter} wrote them; dotnet test and coverage checked each one, and each accepted test was run against every other unnoticed break",
                tried.Count > accepted.Count ? string.Join("\n", tried.Where(t => t.Status != "accepted").Select(t => $"{t.Request}: {t.Status} — {t.Detail}")) : null));
        }
    }

    private static void BuildFindings(ReviewResult result, CodeGraph graph, List<string> testProjects, bool skipTests)
    {
        var notChecked = result.NotChecked;
        foreach (var c in result.ChangedSymbols.Where(c => !c.IsTest && c.UncoveredLines.Count > 0))
        {
            notChecked.Add(new Finding(
                "Unexecuted code",
                $"{c.Display} — line(s) {Ranges(c.UncoveredLines)} of {c.File} were never executed by any test" +
                (c.Dependents > 0 ? $"; {c.Dependents} members depend on this symbol and inherit the doubt" : string.Empty)));
        }

        foreach (var m in result.Mutants.Where(m => m.Status == "survived"))
        {
            notChecked.Add(new Finding(
                "Unnoticed break",
                $"{m.File}:{m.Line} — altering `{m.Original}` ({m.Description}) makes no test fail. The line runs, but nothing checks what it does."));
        }

        foreach (var group in result.Files.Where(f => f.Category is "docs" or "build-config" or "other").GroupBy(f => f.Category))
        {
            notChecked.Add(new Finding(
                group.Key switch { "docs" => "Documentation", "build-config" => "Build configuration", _ => "Other files" },
                $"{group.Count()} file(s), {group.Sum(f => f.Added + f.Deleted)} lines: no tool checked that this text is accurate ({string.Join(", ", group.Take(4).Select(f => f.Path))}{(group.Count() > 4 ? ", …" : string.Empty)})"));
        }

        var snapshots = result.Files.Where(f => f.Category == "test-snapshot").ToList();
        if (snapshots.Count > 0)
        {
            notChecked.Add(new Finding(
                "Expected-output files",
                $"{snapshots.Count} expected-output files were rewritten by the same commit that changed the code producing them. Tests comparing against them pass by construction, so they show the output is stable, not that it is right."));
        }

        if (result.Files.Any(f => f.Category == "template") && result.Files.Any(f => f.Category == "generated"))
        {
            notChecked.Add(new Finding(
                "Templates vs generated code",
                "Both T4 templates (.tt) and their generated .cs files changed. The templates were not re-run to confirm the committed .cs files are what they produce."));
        }

        foreach (var run in result.Runs.Where(r => r.Label == "head" && !r.Built))
        {
            notChecked.Add(new Finding(
                "Tests that could not be built",
                $"{run.Project} did not compile, so none of its tests ran. First error: {run.BuildErrors.FirstOrDefault() ?? "see the run log"}"));
        }

        if (graph.Warnings.Count > 0)
        {
            notChecked.Add(new Finding("Dependency graph", $"{graph.Warnings.Count} warning(s) while loading the projects; dependencies may be missing. First: {graph.Warnings[0]}"));
        }

        if (skipTests)
        {
            notChecked.Add(new Finding("Behaviour", "No test was executed; every behavioural claim is unverified."));
        }

        var refused = result.RefusedToDecide;
        refused.Add(new Finding(
            "Requirement",
            $"Whether this change does what {(result.Requirement is null ? "its requirement" : "requirement " + result.Requirement)} asks. AYA measured what the code does under test; it has not interpreted the requirement."));
        foreach (var t in result.ProbedTests.Where(p => p.Verdict == "non-discriminating"))
        {
            refused.Add(new Finding("Test value", $"Whether {t.Name} is evidence for this change: it passes on the old code too, so it does not demonstrate the new behaviour."));
        }

        if (result.ProbedTests.Any(p => p.Origin == "modified"))
        {
            refused.Add(new Finding("Changed expectations", "Whether editing existing tests was legitimate. A modified test may have had its expectation changed to fit the new code."));
        }

        if (result.ChangedSymbols.Any(c => !c.IsTest && c.ChangeKind == "modified" && c.Dependents > 0))
        {
            refused.Add(new Finding("Side effects", "Whether behaviour changes in already-existing members are intended. They are reachable from code the requirement does not mention."));
        }

        var acceptedTests = result.ProposedTests.Count(t => t.Status == "accepted");
        if (acceptedTests > 0)
        {
            refused.Add(new Finding(
                "Proposed tests",
                $"Whether the {acceptedTests} accepted test(s) written by {result.TestWriter} assert the right behaviour. AYA checked that they compile, pass and reach their target; a model chose what to assert. They are proposals in the proposed-tests folder, not part of the change."));
        }

        foreach (var t in result.ProposedTests.Where(t => t.Status == "fails-on-current-code"))
        {
            refused.Add(new Finding(
                "Possible defect",
                $"Whether {t.Request} ({t.Symbol}) fails because the test is wrong or because the code is. {result.TestWriter} wrote a test for it that still fails after {t.Attempts} attempt(s): {t.Detail}"));
        }

        if (result.Regressions.Count > 0)
        {
            refused.Add(new Finding("Regressions", $"Whether {result.Regressions.Count} newly failing test(s) are acceptable. AYA reports them; it does not waive them."));
        }

        result.Limits.AddRange(
        [
            "Executed is not the same as correct: a covered line ran under a passing test, nothing more.",
            "Coverage does not separate pre-existing tests from tests written in the same change.",
            "Static reachability over-approximates: referring to a type is treated as possibly calling its constructors and framework entry points.",
            "Calls made through reflection, generated code or analyzers loaded by the compiler are not edges in the graph.",
            "Symbols deleted by the change are attributed to the declaration that enclosed them; a deleted member has no node.",
        ]);
        if (!skipTests && testProjects.Count == 0)
        {
            result.Limits.Add("No test project depends on the changed projects through a project reference; nothing was executed.");
        }
    }

    /// <summary>
    /// Turns the evidence into a reading plan: which lines a person still has to read, and why. The rule is fixed
    /// and stated in the report so the owner can disagree with it.
    /// </summary>
    private static void BuildReadingPlan(ReviewResult result)
    {
        var plan = result.ReadingPlan;
        var grouped = result.Patterns.Where(p => p.Files.Count > 1).ToDictionary(p => p.Id);

        foreach (var c in result.ChangedSymbols.Where(c => !c.IsTest && !grouped.ContainsKey(PatternOf(result, c.File))))
        {
            switch (c.Status)
            {
                case "failing":
                    plan.Add(new ReadItem("full", c.Display, $"{c.FailingTests.Count} failing test(s) reach it", c.ChangedLines.Count));
                    break;
                case "unverified":
                    plan.Add(new ReadItem("full", c.Display, "no changed line was executed by any test", c.ChangedLines.Count));
                    break;
                case "partially-verified":
                    plan.Add(new ReadItem("targeted", $"{c.Display} — lines {Ranges(c.UncoveredLines)}", "these lines were never executed", c.UncoveredLines.Count));
                    break;
                case "verified" when c.ChangeKind == "modified" && c.Dependents > 0:
                    // Existing behaviour that other code relies on changed. Tests passing is not enough to waive a look.
                    plan.Add(new ReadItem("targeted", $"{c.Display} — lines {Ranges(c.ChangedLines)}", $"alters an existing member that {c.Dependents} others depend on", c.ChangedLines.Count));
                    break;
                case "verified" when c.Mutants.Any(m => m.Status == "survived"):
                    var unnoticed = c.Mutants.Where(m => m.Status == "survived").Select(m => m.Line).Distinct().OrderBy(l => l).ToList();
                    plan.Add(new ReadItem("targeted", $"{c.Display} — lines {Ranges(unnoticed)}", "these lines run, but breaking them on purpose made no test fail", unnoticed.Count));
                    break;
                case "verified":
                    plan.Add(new ReadItem("light", c.Display, $"new code; all {c.CoveredLines.Count} changed executable lines ran under passing tests", 0));
                    break;
                default:
                    plan.Add(new ReadItem("light", c.Display, "no executable line changed, or nothing was measured", result.Runs.Count == 0 ? c.ChangedLines.Count : 0));
                    break;
            }
        }

        var testSymbols = result.ChangedSymbols.Where(c => c.IsTest).ToList();
        foreach (var t in result.ProbedTests.Where(p => p.Verdict != "discriminating").GroupBy(p => p.Name.IndexOf('(') is var i and > 0 ? p.Name[..i] : p.Name))
        {
            var symbol = testSymbols.FirstOrDefault(s => s.Display.Contains(t.Key.Split('.').Last(), StringComparison.Ordinal));
            var why = t.First().Verdict switch
            {
                "non-discriminating" => "passes on the old code too",
                "needs-new-code" => "cannot be compiled against the old code, so it could not be shown to fail before",
                _ => "could not be run against the old code",
            };
            plan.Add(new ReadItem("targeted", "test " + t.Key, why, symbol?.ChangedLines.Count ?? 0));
        }

        foreach (var p in grouped.Values)
        {
            plan.Add(new ReadItem(
                "targeted",
                $"one of {p.Files.Count} {p.Category} files sharing pattern {p.Id} (e.g. {p.Files[0]})",
                $"all {p.Files.Count} files received the same {p.Edits.Count} line edit(s)",
                p.LinesPerFile));
        }

        foreach (var f in result.Files.Where(f => !grouped.ContainsKey(f.Pattern) && f.Category is "docs" or "build-config" or "other" or "template" or "test-snapshot"))
        {
            plan.Add(new ReadItem("targeted", f.Path, $"{f.Category}: no automated check applies", f.Added + f.Deleted));
        }

        result.LinesToRead = plan.Sum(i => i.Lines);
    }

    private static string PatternOf(ReviewResult result, string file) =>
        result.Files.FirstOrDefault(f => string.Equals(f.Path, file, StringComparison.OrdinalIgnoreCase))?.Pattern ?? string.Empty;

    /// <summary>Formats sorted line numbers as compact ranges: 3, 7–9.</summary>
    public static string Ranges(IReadOnlyList<int> lines)
    {
        var parts = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            var start = lines[i];
            while (i + 1 < lines.Count && lines[i + 1] == lines[i] + 1)
            {
                i++;
            }

            parts.Add(start == lines[i] ? start.ToString() : $"{start}–{lines[i]}");
        }

        return string.Join(", ", parts);
    }

    private static object GraphExport(CodeGraph graph, List<ChangedSymbol> changed)
    {
        var changedById = changed.ToDictionary(c => c.Id);
        var impact = graph.Reach(changed.Where(c => !c.IsTest).Select(c => c.Id), reverse: true);
        return new
        {
            projects = graph.Projects,
            layers = graph.LayerCount,
            warnings = graph.Warnings,
            nodes = graph.Nodes.Values.Select(n => new
            {
                n.Id,
                n.Kind,
                n.Name,
                n.Display,
                n.ContainingType,
                n.Namespace,
                n.Project,
                n.Spans,
                n.Layer,
                n.Scc,
                n.IsTest,
                n.IsGenerated,
                n.IsPublicApi,
                n.IsEntryPoint,
                n.TestName,
                Change = changedById.TryGetValue(n.Id, out var c) ? c.ChangeKind : null,
                Status = c?.Status,
                ImpactDistance = impact.TryGetValue(n.Id, out var d) ? d : (int?)null,
            }),
            edges = graph.Edges,
        };
    }
}
