namespace Aya;

/// <summary>Turns the measurements of a review into the statements the report makes.</summary>
public static partial class ReviewCommand
{
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
            $"{result.Files.Count} changed files fall into {result.Patterns.Count} groups of similar edits" +
            (bigPatterns.Count > 0 ? $"; the largest has {bigPatterns[0].Files.Count} files in {bigPatterns[0].Variants.Count} exact variant(s)" : string.Empty),
            "git diff; files grouped by the distinct lines they edit, then split by exact edit (deterministic)"));

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

        foreach (var run in headRuns.Concat(baseRuns).Where(r => r.Built && r.Problem is not null))
        {
            list.Add(new Evidence(
                Next(),
                "run",
                "warn",
                $"{run.Project} on the {(run.Label == "head" ? "new" : "old")} code: {run.Problem}. Its tests gave no evidence either way",
                "dotnet test"));
        }

        if (comparison.Silenced.Count > 0)
        {
            list.Add(new Evidence(
                Next(),
                "regression",
                "warn",
                $"{comparison.Silenced.Count} test(s) that passed on the old code are skipped or do not run on the new code",
                "dotnet test on old and new code, compared test by test",
                string.Join("\n", comparison.Silenced.Take(10))));
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
            // Counted from each test's own outcome. A test that did not fail has not thereby passed.
            var failingNew = probes.Count(p => p.OnHead == "Failed");
            var passingNew = probes.Count(p => p.OnHead == "Passed");
            var idleNew = probes.Count - failingNew - passingNew;
            list.Add(new Evidence(
                Next(),
                "new-tests",
                failingNew > 0 ? "fail" : idleNew > 0 ? "warn" : "pass",
                $"{probes.Count(p => p.Origin == "new")} new and {probes.Count(p => p.Origin == "modified")} modified tests; {passingNew} pass on the new code, {failingNew} fail" +
                (idleNew > 0 ? $", {idleNew} were skipped or did not run and show nothing" : string.Empty),
                "dotnet test (new code)",
                idleNew > 0 ? "Skipped or not run:\n" + string.Join("\n", probes.Where(p => p.OnHead is not ("Failed" or "Passed")).Take(15).Select(p => $"{p.Name} ({p.OnHead})")) : null));

            var discriminating = probes.Count(p => p.Verdict == "discriminating");
            var weak = probes.Where(p => p.Verdict == "non-discriminating").ToList();
            var needNew = probes.Count(p => p.Verdict == "needs-new-code");
            var undetermined = probes.Count - discriminating - weak.Count - needNew;

            // "pass" only when every one of them was seen to fail without the change. Not having caught a weak
            // test is not the same as having shown the tests are strong.
            list.Add(new Evidence(
                Next(),
                "fail-before",
                discriminating == probes.Count ? "pass" : "warn",
                $"{discriminating} of {probes.Count} of those tests fail on the old code (they demonstrate the change); {weak.Count} pass without it; {needNew} cannot compile without it" +
                (undetermined > 0 ? $"; {undetermined} could not be determined" : string.Empty),
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
            // Only a named test failing counts as caught. A hung or crashed run is reported apart.
            var caught = result.Mutants.Count(m => m.Status == "killed");
            var survived = result.Mutants.Count(m => m.Status == "survived");
            var blind = result.Mutants.Count(m => m.Status == "not-covered");
            var skipped = result.Mutants.Count(m => m.Status == "not-run");
            var inconclusive = result.Mutants.Count(m => m.Status is "timeout" or "crashed");
            list.Add(new Evidence(
                Next(),
                "mutation",
                survived > 0 || inconclusive > 0 ? "warn" : caught > 0 ? "pass" : "warn",
                $"{caught} of {caught + survived} deliberate breaks of changed lines were caught by a test; {survived} went unnoticed" +
                (inconclusive > 0 ? $"; {inconclusive} hung or crashed the test run and are inconclusive" : string.Empty) +
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

        var probed = result.Comparisons.Where(p => p.Purpose == "modified-unit" && p.Status != "not-attempted").ToList();
        if (probed.Count > 0)
        {
            var compared = probed.Where(p => p.Status == "compared").ToList();
            var broke = compared.Sum(p => p.Count("now-fails"));
            var differ = compared.Sum(p => p.Differences.Count);
            list.Add(new Evidence(
                Next(),
                "old-vs-new",
                broke > 0 ? "fail" : differ > 0 || compared.Count < probed.Count ? "warn" : compared.Count > 0 ? "pass" : "warn",
                $"{compared.Count} of {probed.Count} modified unit(s) were run on the old and the new code with the same {compared.Sum(p => p.Cases)} input(s): " +
                $"{compared.Sum(p => p.Same)} give the same result, {differ} differ" +
                (differ > 0 ? " (" + string.Join(", ", compared.SelectMany(p => p.Differences).GroupBy(d => d.Kind).Select(g => $"{g.Count()} {g.Key}")) + ")" : string.Empty) +
                (compared.Sum(p => p.Unstable) is var shaky and > 0 ? $"; {shaky} input(s) were discarded as unstable" : string.Empty),
                $"probes written by {result.TestWriter}; the same probe run twice on the old code and once on the new",
                compared.Count < probed.Count ? string.Join("\n", probed.Where(p => p.Status != "compared").Select(p => $"{p.Request} {p.Symbol}: {p.Status} — {p.Detail.Split('\n')[0]}")) : null));
        }

        var attacked = result.Attacks.Where(a => a.Attempts > 0).ToList();
        if (attacked.Count > 0)
        {
            var regressions = attacked.Count(a => a.Status == "regression-demonstrated");
            var open = attacked.Count(a => a.Status == "possible-defect");
            var judged = attacked.Count(a => a.Status is "resisted" or "regression-demonstrated" or "possible-defect" or "fails-on-both");
            list.Add(new Evidence(
                Next(),
                "attack",
                regressions > 0 ? "fail" : open > 0 || judged < attacked.Count ? "warn" : "info",
                (judged < attacked.Count ? $"for {attacked.Count - judged} of {attacked.Count} unit(s) the model produced no test that could be run; " : string.Empty) +
                $"{judged} unit(s) were attacked with tests written to break them: {attacked.Count(a => a.Status == "resisted")} resisted " +
                $"({attacked.Where(a => a.Status == "resisted").Sum(a => a.Tests)} tests), {regressions} with a demonstrated regression, {open} with a failing test nothing can arbitrate, " +
                $"{attacked.Count(a => a.Status == "fails-on-both")} failing on the old code too",
                $"tests written by {result.AttackWriter}; each file is built and run here, and a failing test is run on the old code, which decides whether the change caused it",
                string.Join("\n", attacked.Where(a => a.Status != "resisted").Select(a => $"{a.Request} {a.Symbol}: {a.Status} — {a.Detail}")) is { Length: > 0 } detail ? detail : null));
        }

        if (result.Exhaustive is { } deep)
        {
            list.Add(new Evidence(
                Next(),
                "exhaustive",
                "info",
                $"{deep.Candidates} member(s) depend on the change and are run by no original test; {deep.Attempted} were probed within a budget of {deep.SessionBudget} session(s) and {deep.Compared} could be compared on old and new code",
                $"probes written by {result.TestWriter}; {deep.Sessions} session(s) used",
                deep.Impacts.Count > 0 ? string.Join("\n", deep.Impacts.Select(i => $"with {i.Symbol} broken ({i.Break}): {i.ChangedDependents} of {i.ProbedDependents} probed dependents changed their output")) : null));
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
            "Deleted code has no unit in a graph of the new code. It is listed as not analysed; what used it was not looked for.",
            "Old and new code are compared only on the inputs a probe chose: no difference found on them is not the same behaviour.",
            "The order of the list is a fixed rule over what was measured. It ranks where a mistake would matter and go unseen, not how probable a mistake is.",
        ]);
        if (!skipTests && testProjects.Count == 0)
        {
            result.Limits.Add("No test project depends on the changed projects through a project reference; nothing was executed.");
        }
    }
}
