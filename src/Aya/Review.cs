using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aya;

public static partial class ReviewCommand
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

        var requirementText = o.Get("req-file") is { } reqFile ? File.ReadAllText(Path.GetFullPath(reqFile)).Replace("\r", string.Empty).Trim() : null;
        var result = new ReviewResult { Repo = repo, Requirement = o.Get("req"), RequirementText = requirementText is { Length: > 8000 } ? requirementText[..8000] + " …" : requirementText, BaseSha = baseSha, HeadRef = headRef, HeadSha = headSha };
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
            .Where(f => Path.GetExtension(f.Path) is ".cs" or ".tt" or ".csproj")
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
        var changed = MapChanges(graph, files, git, baseSha);
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
                        (t.OnOldCode, t.Verdict) = (run.Built, run.Problem, r) switch
                        {
                            (false, _, _) => ("build-failed", "needs-new-code"),
                            (_, not null, _) => ("not-run", "unknown"),
                            (_, _, { Failed: true }) => ("Failed", "discriminating"),
                            (_, _, { Passed: true }) => ("Passed", "non-discriminating"),
                            _ => ("not-run", "unknown"),
                        };
                        trace.Info($"    {t.Name}: {t.OnOldCode} on old code  →  {t.Verdict}");
                    }
                }
            }
        }

        result.Runs = headRuns.Concat(baseRuns).Concat(probeRuns)
            .Select(r => r.Summary())
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
        var blindZone = new List<(Node Node, ChangedSymbol Owner, int Distance, List<int> Lines)>();

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

            // Of the code that depends on this unit, how much does any test execute? Where none does, a break
            // that reaches it would not be seen, and the count of dependents is all that is known about it.
            if (!c.IsTest && coverage.Count > 0)
            {
                foreach (var (id, distance) in members.OrderBy(kv => kv.Value))
                {
                    var member = graph.Nodes[id];
                    var measured = member.Spans
                        .SelectMany(s => coverage.TryGetValue(s.File, out var ls) ? ls.Where(l => l.Key >= s.Start && l.Key <= s.End) : Enumerable.Empty<KeyValuePair<int, int>>())
                        .ToList();
                    if (measured.Count == 0 || member.IsGenerated || changed.Any(x => x.Id == id))
                    {
                        continue;
                    }

                    c.MeasuredDependents++;
                    if (measured.All(l => l.Value == 0))
                    {
                        c.UnexecutedDependents++;
                        c.UnexecutedDependentNames.Add(member.Display);
                        if (blindZone.All(b => b.Node.Id != id))
                        {
                            blindZone.Add((member, c, distance, measured.Select(l => l.Key).OrderBy(l => l).ToList()));
                        }
                    }
                }
            }

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
            else if (headRuns.All(r => r.Problem is null))
            {
                // The file never appeared in the coverage report: no test loaded this code at all.
                c.UncoveredLines.AddRange(c.ChangedLines);
            }
            else
            {
                // A test project did not build or did not finish, and it may be the one that runs this file. "No
                // test executes it" would be a claim about tests that never ran, so nothing is concluded.
                continue;
            }

            c.Status = c switch
            {
                { FailingTests.Count: > 0 } => "failing",
                { UncoveredLines.Count: 0, CoveredLines.Count: > 0 } => "executed",
                { UncoveredLines.Count: > 0, CoveredLines.Count: > 0 } => "partly-executed",
                { UncoveredLines.Count: > 0 } => "not-executed",
                _ => "not-measured",
            };
        }

        trace.Info($"coverage data for {coverage.Count} source files");
        foreach (var c in changed.Where(c => !c.IsTest))
        {
            trace.Info($"layer {c.Layer,2}  {c.Display}");
            trace.Info($"          changed lines run {c.CoveredLines.Count}, not run {c.UncoveredLines.Count}{(c.UncoveredLines.Count > 0 ? " (" + Ranges(c.UncoveredLines) + ")" : string.Empty)}  |  {c.Dependents} units depend on it ({c.UnexecutedDependents} of {c.MeasuredDependents} measured ones run by no test)  |  {c.ReachingTests} tests can reach it  →  {c.Status}");
        }

        result.Tests = OutcomesByMethod(headRuns, baseRuns, result.ProbedTests);

        // Optional deeper stages. They work on scratch copies of the old and the new code and put every file back.
        var deeper = new[] { "mutate", "write-tests", "compare", "sweep", "attack", "exhaustive", "judge" }.Where(o.Has).ToList();
        var attackFrom = o.Get("attack-from") is { } from ? Path.GetFullPath(from) : null;
        if (attackFrom is not null)
        {
            deeper.Add("attack-from");
        }

        var writer = o.Get("writer") ?? "gemini";
        if (writer is not ("gemini" or "claude"))
        {
            throw new AyaException("--writer must be gemini or claude");
        }

        if (!skipTests && deeper.Count > 0)
        {
            var bench = new Workbench
            {
                Git = git,
                HeadSha = headSha,
                BaseSha = baseSha,
                HeadWorktree = headWt,
                WorktreesRoot = worktrees,
                TestProjects = testProjects,
                RunsDir = runsDir,
                SdkFolder = sdkFolder,
                Trace = trace,
                CoverageByProject = coverageByProject,
                FailingOnHead = headRuns.SelectMany(r => r.Results).Where(r => r.Failed).Select(r => r.Project + "::" + r.Method).ToHashSet(StringComparer.Ordinal),
                NewTests = result.ProbedTests.Select(p => ModelSession.MethodOf(p.Name)).ToHashSet(StringComparer.Ordinal),
            };
            int Limit(string name, int fallback) => int.TryParse(o.Get(name), out var n) ? n : fallback;
            var session = new ModelSession(writer, o.Get("writer-model") ?? (writer == "gemini" ? "gemini-4-hydrogen-medium" : "claude-opus-5-5"), o.Get("writer-effort") ?? "medium");
            if (deeper.Any(d => d is "write-tests" or "compare" or "sweep" or "attack" or "exhaustive" or "judge"))
            {
                result.TestWriter = session.Name;
            }

            var mutantTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            if (o.Has("mutate"))
            {
                trace.Step("Break each changed line on purpose and see whether any test notices (mutation testing)");
                (result.Mutants, mutantTexts) = MutationRunner.Run(bench, changed, Limit("max-mutants", 20));

                trace.Step("Undo the change one part at a time and see whether any test needs that part");
                result.Reverts = MutationRunner.Reverts(bench, changed, files, Limit("max-reverts", 8));
            }

            if (o.Has("compare"))
            {
                trace.Step("Run the same inputs through the OLD and the NEW code and see where the results differ");
                result.Comparisons = new Comparer(session).CompareModified(bench, graph, result, outDir, Limit("max-probes", 4));
            }

            if (o.Has("sweep"))
            {
                trace.Step("Sweep: run inputs nobody chose (the existing tests' own, and altered with what the change adds) through the OLD and the NEW code");
                var (run, sweep) = new Comparer(session).Sweep(bench, graph, result, outDir, Limit("max-inputs", 240));
                result.Sweep = sweep;
                if (run is not null)
                {
                    result.Comparisons.Add(run);
                }
            }

            if (o.Has("write-tests"))
            {
                trace.Step("Ask a model for tests that fill the gaps, and check every one before believing it");
                result.ProposedTests = new TestWriter(session).Run(bench, graph, changed, result, mutantTexts, outDir, Limit("max-tests", 4));
            }

            if (o.Has("attack") || attackFrom is not null)
            {
                // Aimed by what was measured so far: the units the list already puts first.
                trace.Step("Ask a model to break the units at the top of the list; the old code decides who is right");
                var byId = changed.Where(c => !c.IsTest).GroupBy(c => c.Display, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
                var targets = Attention.Build(result, headWt)
                    .Where(i => i.Kind == "unit" && byId.ContainsKey(i.What))
                    .Select(i => byId[i.What])
                    .Where(c => c.CoveredLines.Count + c.UncoveredLines.Count > 0)
                    .ToList();
                // Every changed unit gets a session of its own, and they all run at once, so none is left out by
                // default. --max-attacks is there to limit what is spent.
                var attacker = new Attacker(session);
                var most = Limit("max-attacks", int.MaxValue);
                (result.Attacks, result.AttackWriter) = attackFrom is not null
                    ? (attacker.RunFrom(bench, graph, result, targets, outDir, most, attackFrom), "whoever left the test files in " + attackFrom)
                    : (attacker.Run(bench, graph, result, targets, outDir, most), session.Name);

                // Tests that showed a regression on an earlier run of this branch are run again, and this run's are kept.
                var store = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outDir))!, "_kept", string.Concat(result.HeadRef.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_')));
                // A pass on the old code of a test about something the old code does not have is not a verdict.
                Attacker.WithoutEmptyPasses(result.Attacks, outDir, Comparer.Introduced(result));
                result.Attacks.AddRange(Attacker.Replay(bench, result.Attacks, outDir, store));
                Attacker.WithoutEmptyPasses(result.Attacks, outDir, Comparer.Introduced(result));
            }

            if (o.Has("exhaustive"))
            {
                trace.Step("Exhaustive: probe the older code that depends on the change and that no original test executes");
                var (probes, summary) = new Comparer(session).Exhaustive(bench, graph, result, blindZone, mutantTexts, outDir, Limit("max-sessions", 10));
                result.Comparisons.AddRange(probes);
                result.Exhaustive = summary;
            }

            if (o.Has("judge"))
            {
                trace.Step("Judge: ask a model whether each difference the change's own text does not explain was meant (an opinion, shown as one)");
                result.Judge = new Judge(session).Run(bench, result, outDir);
                result.Obligations = new Judge(session).Fit(bench, result, outDir);
            }

            // What Gemini did not answer was written by Claude. The reports name who wrote what they show, so they
            // must not name only the model that was asked first.
            if (session.Fallbacks > 0)
            {
                var also = $"; {session.Fallbacks} of this run's {session.Sessions} sessions got no answer from it and were answered by claude-opus-5-5 instead" +
                    (session.QuotaSpent ? ", because its quota ran out during the run" : string.Empty);
                result.TestWriter = result.TestWriter is null ? null : result.TestWriter + also;
                result.AttackWriter = result.AttackWriter == session.Name ? result.AttackWriter + also : result.AttackWriter;
                if (result.Judge is { } judged)
                {
                    judged.Writer += also;
                }
            }

            if (session.Sessions > 0)
            {
                trace.Info($"model sessions opened in this run: {session.Sessions}" + (session.Fallbacks > 0 ? $"; {session.Fallbacks} that Gemini did not answer were asked of Claude instead" : string.Empty));
            }
        }
        else if (!skipTests && blindZone.Count > 0)
        {
            trace.Info($"{blindZone.Count} member(s) depend on the change and are run by no test; --exhaustive would probe them");
        }

        if (git.Restored.Count > 0)
        {
            trace.Info($"working copies found altered by an earlier run and put back to their commit: {string.Join(", ", git.Restored)}");
        }

        trace.Step("Turn the results into evidence and an ordered list of where to look, and write the report");
        BuildEvidence(result, graph, headRuns, baseRuns, comparison, skipTests);
        BuildFindings(result, graph, testProjects, skipTests);
        result.Attention = Attention.Build(result, headWt);
        trace.List(result.Evidence.Select(e => $"{e.Id,-4} {e.Status.ToUpperInvariant(),-5} {e.Claim}"), 30);
        trace.Info($"not checked: {result.NotChecked.Count} item(s)  |  refused to decide: {result.RefusedToDecide.Count} item(s)");
        trace.Info("where to look, most deserving first:");
        trace.List(result.Attention.Select(a => $"{a.Rank,3}. tier {a.Tier}  {a.What}  —  {a.Why.FirstOrDefault()}"), 25);

        // 8. Write the artefacts. The report quotes the hash of the evidence file it was derived from.
        var evidenceJson = JsonSerializer.Serialize(result, Json);
        var evidencePath = Path.Combine(outDir, "evidence.json");
        File.WriteAllText(evidencePath, evidenceJson);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidenceJson))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(outDir, "graph.json"), JsonSerializer.Serialize(GraphExport(graph, changed), Json));
        File.WriteAllText(Path.Combine(outDir, "report.md"), Report.Render(result, hash));
        File.WriteAllText(Path.Combine(outDir, "summary.md"), Report.Summary(result));
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

        Console.WriteLine("Look first  " + string.Join("  |  ", result.Attention.GroupBy(a => a.Tier).OrderBy(g => g.Key).Select(g => $"tier {g.Key}: {g.Count()}")));
        foreach (var a in result.Attention.Take(5))
        {
            Console.WriteLine($"  {a.Rank}. [tier {a.Tier}] {a.What}");
        }

        Console.WriteLine($"Summary     {Path.Combine(outDir, "summary.md")}");
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
    private static List<ChangedSymbol> MapChanges(CodeGraph graph, List<FileChange> files, Git git, string baseSha)
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

                    // For a pure deletion the diff gives the line before the removed text. The deletion is inside a
                    // unit only if that unit goes on past that line; at its last line, what was removed came after it.
                    var owner = spans
                        .Where(x => x.Span.Start <= line && (hunk.NewLen > 0 ? line <= x.Span.End : line < x.Span.End))
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

        // What each changed file declared before the change, read from the old text. Whether a unit is new or
        // already existed is decided by that, not by which lines the diff happens to mark: a diff can leave the
        // closing brace of a new method as context, or show a method that only moved as added.
        var declaredBefore = new Dictionary<string, HashSet<string>?>(StringComparer.OrdinalIgnoreCase);
        HashSet<string>? Before(FileChange f)
        {
            if (!declaredBefore.TryGetValue(f.Path, out var keys))
            {
                var old = f.Status == "A" || !f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? null : git.Try("show", $"{baseSha}:{f.OldPath ?? f.Path}");
                declaredBefore[f.Path] = keys = old is { Ok: true } ? Declarations.In(old.StdOut) : null;
            }

            return keys;
        }

        return lines.Values
            .Select(e =>
            {
                // Without the old text to go by: new when every line of the declaration was added by this change.
                var added = e.File.AddedLines().ToHashSet();
                var wholly = !e.Deletion && e.Node.Spans
                    .Where(s => string.Equals(s.File, e.File.Path, StringComparison.OrdinalIgnoreCase))
                    .All(s => Enumerable.Range(s.Start, s.End - s.Start + 1).All(added.Contains));
                if (Before(e.File) is { } before && Declarations.KeyOf(e.Node.Display, e.Node.Kind) is { } key)
                {
                    wholly = !Declarations.Declares(before, key);
                }
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

        /// <summary>Tests that passed on the old code and neither pass nor fail on the new code: skipped or not run.</summary>
        public List<string> Silenced { get; } = new();

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
            else if (old.Passed)
            {
                // Switching a test off is the quietest way to make a failure disappear.
                c.Silenced.Add($"{r.Name} ({r.Outcome})");
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
