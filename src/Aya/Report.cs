using System.Text;

namespace Aya;

/// <summary>Renders the evidence as the page a named owner reads before signing off.</summary>
public static class Report
{
    public static string Render(ReviewResult r, string evidenceHash)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Review evidence — {r.HeadRef}");
        sb.AppendLine();
        sb.AppendLine($"| | |");
        sb.AppendLine($"|---|---|");
        sb.AppendLine($"| Requirement | {r.Requirement ?? "not supplied"} |");
        sb.AppendLine($"| Change | `{r.HeadSha[..8]}` {Esc(r.Subject)} |");
        sb.AppendLine($"| Written by | {Esc(r.Author)}, {r.AuthoredAt} |");
        sb.AppendLine($"| Compared with | `{r.BaseSha[..8]}` |");
        sb.AppendLine($"| Built with | .NET SDK {r.Sdk}{(r.SdkPinnedByRepo is null ? string.Empty : $" (the repository selects {r.SdkPinnedByRepo})")} |");
        sb.AppendLine($"| Produced by | {r.Tool}, {r.GeneratedAt} |");
        sb.AppendLine($"| Evidence file | `evidence.json`, sha256 `{evidenceHash}` |");
        sb.AppendLine();
        sb.AppendLine("Every statement below comes from a tool run, and its source is named. Where a model wrote a probe or a test, what is stated is what running it showed, never what the model said.");
        sb.AppendLine();

        sb.AppendLine("## 1. Where to look, most deserving first");
        sb.AppendLine();
        sb.AppendLine("Everything the change touches is in this list; nothing was left out of it. The order says where a mistake is most likely to matter and least likely to be noticed. The last tier means no signal was found, not that the code is right. How far down to go is the owner's decision.");
        sb.AppendLine();
        sb.AppendLine("| # | Tier | What | Why it is here |");
        sb.AppendLine("|---:|---|---|---|");
        foreach (var a in r.Attention)
        {
            sb.AppendLine($"| {a.Rank} | {a.Tier}. {a.TierName} | {Esc(a.What)} | {Esc(a.Why.FirstOrDefault() ?? string.Empty)} |");
        }

        sb.AppendLine();
        sb.AppendLine("Rule used. " + Attention.Rule);
        foreach (var a in r.Attention.Where(a => a.Tier < 6))
        {
            sb.AppendLine();
            sb.AppendLine($"### {a.Rank}. {Esc(a.What)}");
            sb.AppendLine();
            if (a.File is not null)
            {
                sb.AppendLine($"`{a.File}`" + (a.Lines is { Length: > 0 } ? $", {(a.Kind == "unit" ? "changed lines " : string.Empty)}{a.Lines}" : string.Empty));
                sb.AppendLine();
            }

            Section(sb, "Why it is here", a.Why);
            Section(sb, "How likely it is to be wrong (indications, not a measurement)", a.Likelihood);
            Section(sb, "How much breaks if it is wrong", a.Impact);
            Section(sb, "Whether anything would notice", a.Detection);
            Section(sb, "What the analysis cannot see", a.Blind);
            Section(sb, "For the owner to check", a.Check);
        }

        sb.AppendLine();
        sb.AppendLine("## 2. What changed");
        sb.AppendLine();
        sb.AppendLine("| Role | Files | Lines added | Lines removed |");
        sb.AppendLine("|---|---:|---:|---:|");
        foreach (var g in r.Files.GroupBy(f => f.Category).OrderByDescending(g => g.Count()))
        {
            sb.AppendLine($"| {g.Key} | {g.Count()} | {g.Sum(f => f.Added)} | {g.Sum(f => f.Deleted)} |");
        }

        var repeated = r.Patterns.Where(p => p.Files.Count > 1).ToList();
        if (repeated.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Groups of files that use the same edit. Similar is not identical: each group is split into the variants that are exactly the same edit.");
            sb.AppendLine();
            foreach (var p in repeated.Take(8))
            {
                sb.AppendLine($"- **{p.Files.Count} {p.Category} files**, group `{p.Id}`, e.g. `{p.Files[0]}`: " +
                    (p.Variants.Count == 1 ? "all exactly the same edit" : $"{p.Variants.Count} exact variants, of {string.Join(", ", p.Variants.Select(v => $"{v.Files.Count} file(s) with {v.EditedLines} edited line(s)"))}"));
                foreach (var e in p.Edits.Take(6))
                {
                    sb.AppendLine($"  - `{e.Replace("`", "'")}`");
                }

                if (p.Edits.Count > 6)
                {
                    sb.AppendLine($"  - … {p.Edits.Count - 6} more");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("## 3. Changed symbols, from the bottom of the dependency graph up");
        sb.AppendLine();
        sb.AppendLine($"Graph: {r.GraphNodes} nodes, {r.GraphEdges} edges, {r.GraphLayers} layers, over {r.Projects.Count} projects. Layer 0 depends on nothing else in the code; a higher layer depends on lower ones.");
        sb.AppendLine();
        sb.AppendLine("| Layer | Symbol | Change | Public API | Dependents | Tests that can reach it | Changed lines run / not run | Measured |");
        sb.AppendLine("|---:|---|---|---|---:|---:|---|---|");
        foreach (var c in r.ChangedSymbols.Where(c => !c.IsTest))
        {
            sb.AppendLine($"| {c.Layer} | `{Esc(c.Display)}` | {c.ChangeKind} | {(c.PublicApi ? "yes" : "no")} | {c.Dependents} | {c.ReachingTests} | {c.CoveredLines.Count} / {c.UncoveredLines.Count} | {c.Status} |");
        }

        var tests = r.ChangedSymbols.Where(c => c.IsTest).ToList();
        if (tests.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Plus {tests.Count} test methods and test helpers added or modified by the change (section 4 says what they prove).");
        }

        sb.AppendLine();
        sb.AppendLine("## 4. What was checked");
        sb.AppendLine();
        sb.AppendLine("| ID | Result | Finding | Checked by |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var e in r.Evidence)
        {
            sb.AppendLine($"| {e.Id} | {e.Status} | {Esc(e.Claim)} | {Esc(e.Source)} |");
        }

        foreach (var e in r.Evidence.Where(e => e.Detail is not null))
        {
            sb.AppendLine();
            sb.AppendLine($"{e.Id} detail:");
            sb.AppendLine("```");
            sb.AppendLine(e.Detail);
            sb.AppendLine("```");
        }

        if (r.ProbedTests.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Tests that came with the change, run against the old code:");
            sb.AppendLine();
            sb.AppendLine("| Test | Origin | On new code | On old code | Meaning |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var t in r.ProbedTests)
            {
                var meaning = t.Verdict switch
                {
                    "discriminating" => "fails without the change: it demonstrates it",
                    "non-discriminating" => "passes without the change: it proves nothing about it",
                    "needs-new-code" => "does not compile without the change",
                    _ => "could not be determined",
                };
                sb.AppendLine($"| `{Esc(t.Name)}` | {t.Origin} | {t.OnHead} | {t.OnOldCode} | {meaning} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Test runs:");
        sb.AppendLine();
        sb.AppendLine("| Code under test | Project | Compiled | Passed | Failed | Skipped or not run | Seconds | Problem |");
        sb.AppendLine("|---|---|---|---:|---:|---:|---:|---|");
        foreach (var run in r.Runs)
        {
            var label = run.Label switch { "head" => "new", "base" => "old", _ => "old + new tests" };
            sb.AppendLine($"| {label} | {run.Project} | {(run.Built ? "yes" : "no")} | {run.Passed} | {run.Failed} | {run.Skipped} | {run.Seconds} | {run.Problem ?? string.Empty} |");
        }

        if (r.Mutants.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Deliberate breaks of the changed lines. Each row alters one line and re-runs the tests that execute it:");
            sb.AppendLine();
            sb.AppendLine("| Break | Where | Alteration | Original code | Result |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var m in r.Mutants.OrderBy(m => m.Status switch { "survived" => 0, "timeout" or "crashed" => 1, "not-covered" => 2, "killed" => 3, _ => 4 }).ThenBy(m => m.Line))
            {
                var outcome = m.Status switch
                {
                    "killed" => $"caught by `{m.KilledBy.FirstOrDefault()}`" + (m.KilledByCount > 1 ? $" and {m.KilledByCount - 1} more" : string.Empty) + (m.CaughtByOldTests == 0 ? "; only by tests that came with the change" : string.Empty),
                    "survived" => "**not noticed by any test**",
                    "not-covered" => "no test executes this line",
                    "invalid" => "does not compile; discarded",
                    "timeout" => "the tests hung: inconclusive",
                    "crashed" => "the test run ended without results: inconclusive",
                    _ => "not tried (budget)",
                };
                sb.AppendLine($"| {m.Id} | {Path.GetFileName(m.File)}:{m.Line} | {Esc(m.Description)} | `{Esc(m.Original).Replace("`", "'")}` | {Esc(outcome)} |");
            }
        }

        if (r.ProposedTests.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Tests written by {r.TestWriter} to fill the gaps. None is trusted on the model's word; each was built and run:");
            sb.AppendLine();
            sb.AppendLine("| Request | Gap | Target | Tries | Result | Saved as |");
            sb.AppendLine("|---|---|---|---:|---|---|");
            foreach (var t in r.ProposedTests)
            {
                var gap = t.Reason == "never-executed" ? "never executed" : "unnoticed break: " + t.Break;
                var outcome = t.Status == "accepted" ? "**accepted**: " + t.Detail : t.Status + (t.Detail.Length > 0 ? ": " + t.Detail.Split('\n')[0] : string.Empty);
                sb.AppendLine($"| {t.Request} | {Esc(gap).Replace("`", "'")} | `{Esc(t.Symbol)}` line(s) {ReviewCommand.Ranges(t.Lines)} | {t.Attempts} | {Esc(outcome)} | {t.TestFile ?? "—"} |");
            }
        }

        var probes = r.Comparisons.Where(p => p.Status != "not-attempted").ToList();
        if (probes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Old code against new code. A probe written by {r.TestWriter} records what the code does on a set of inputs; it is run twice on the old code, to discard what is unstable, and once on the new:");
            sb.AppendLine();
            sb.AppendLine("| Probe | Unit | Inputs compared | Same | Differ | Unstable | Tries | Outcome | Saved as |");
            sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---|---|");
            foreach (var p in probes)
            {
                var unit = p.Purpose is "modified-unit" or "sweep" ? p.Symbol : $"{p.Symbol} (depends on {p.DependsOn}; run by no test)";
                sb.AppendLine($"| {p.Request} | `{Esc(unit)}` | {p.Cases} | {p.Same} | {p.Differences.Count} | {p.Unstable} | {p.Attempts} | {Esc(p.Status == "compared" ? p.Detail : p.Status + ": " + p.Detail.Split('\n')[0])} | {p.ProbeFile ?? "—"} |");
            }

            foreach (var p in probes.Where(p => p.Differences.Count > 0))
            {
                sb.AppendLine();
                sb.AppendLine($"{p.Request}: inputs whose result differs, the same alteration grouped together:");
                sb.AppendLine();
                foreach (var g in p.Differences.GroupBy(d => d.Group).OrderBy(g => g.First().Kind switch { "now-fails" => 0, "missing-on-new" => 1, "changed" => 2, "fails-differently" => 3, _ => 4 }).Take(12))
                {
                    var first = g.First();
                    var meaning = first.Kind switch
                    {
                        "now-fails" => "gave a result, now fails",
                        "changed" => "gave a result, now gives another",
                        "fails-differently" => "failed, now fails differently",
                        "now-works" => "failed, now gives a result",
                        _ => "recorded on the old code, not on the new",
                    };
                    var label = first.ByChange == "explained" ? " (only lines the change itself adds or removes)"
                        : first.ByChange == "unexplained" ? " (**not explained by the change's own text**)" : string.Empty;
                    var reading = first.Judgement is { } j ? $" Model's reading, not a measurement: {j.Verdict}. {j.Reason}" + (j.Quote.Length > 0 ? $" It quotes: \"{j.Quote}\"" : string.Empty) : string.Empty;
                    sb.AppendLine($"- **{meaning}**{label} in {g.Count()} input(s): {Esc(string.Join("; ", g.Take(4).Select(d => d.Case)))}{(g.Count() > 4 ? "; …" : string.Empty)}");
                    sb.AppendLine("  ```");
                    foreach (var line in (first.Delta.Length > 0 ? first.Delta : Comparer.Delta(first.Old, first.New)).Split('\n').Take(12))
                    {
                        sb.AppendLine("  " + (line.Length > 200 ? line[..200] + "…" : line));
                    }

                    sb.AppendLine("  ```");
                    if (first.Input is not null)
                    {
                        var altered = g.Count(d => d.Input!.StartsWith("the input of", StringComparison.Ordinal));
                        var only = g.Count(d => d.OnlyWhenAltered);
                        sb.AppendLine($"  Where the inputs came from: {g.Count() - altered} taken from an existing test as it is, {altered} altered mechanically" +
                            (only > 0 ? $"; in {only} the difference shows only once the input is altered" : string.Empty) + $". First one: {Esc(first.Input)}.");
                    }

                    if (reading.Length > 0)
                    {
                        sb.AppendLine($"  {Esc(reading.Trim())}");
                    }
                }
            }
        }

        if (r.Sweep is { } sweep)
        {
            sb.AppendLine();
            sb.AppendLine(sweep.TestProject is null
                ? $"Sweep over inputs nobody chose: not run. {sweep.Detail}."
                : $"Sweep over inputs nobody chose (S1 above). AYA found {sweep.Found} input(s) in the tests of `{sweep.TestProject}` as they were before the change, took {sweep.FromTests} as they are and altered {sweep.Altered} mechanically. " +
                  "No model chose or wrote any input; a model wrote only the harness that feeds them to the code and records what comes out. " +
                  (sweep.Status == "compared"
                      ? $"Run on the new code, the inputs executed {sweep.ChangedLinesExecuted} of the change's {sweep.ChangedLines} executable line(s). The inputs are in `{sweep.InputsFile}`."
                      : $"It could not be compared: {sweep.Status}. {sweep.Detail.Split('\n')[0]}"));
            foreach (var note in sweep.Notes)
            {
                sb.AppendLine();
                sb.AppendLine($"Seen by the sweep: {Esc(note)}.");
            }

            if (sweep.NewInputs > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Inputs that came with the change. {sweep.NewInputs} of them (the sources its own tests hold, as they are and altered) were run on the new code alone, since the old code has nothing to say about them. They are held to what must be true of any input: the code under test does not crash, and what it produces compiles unless it reported an error itself. {sweep.NewFindings.Count} break one of the two. The inputs are in `sweep-new-inputs.tsv`.");
                if (sweep.NewFindings.Count > 0)
                {
                    sb.AppendLine();
                    foreach (var f in sweep.NewFindings.Take(15))
                    {
                        sb.AppendLine($"- **{(f.Kind == "crash" ? "crash" : "output does not compile, no error reported")}**: {Esc(f.Origin)}. `{Esc(f.Line)}`");
                    }
                }
            }

            if (sweep.Alterations.Count > 0)
            {
                sb.AppendLine();
                foreach (var a in sweep.Alterations)
                {
                    sb.AppendLine($"- {a.Inputs} input(s): {Esc(a.Label)}" + (a.FromChange ? " — taken from the lines the change added" : " — a fixed alteration"));
                }
            }
        }

        if (r.Judge is { Groups: > 0 } judge)
        {
            sb.AppendLine();
            sb.AppendLine($"A model's reading of what was meant ({judge.Writer}). This is an opinion and is marked as one wherever it appears. It was given {string.Join(", ", judge.Material)}, " +
                $"and asked about the {judge.Groups} group(s) of differences that the change's own text does not explain: {judge.Intended} it reads as what the change set out to do, " +
                $"{judge.NotIntended} as not meant, {judge.Artefact} as artefacts of the build or the run, {judge.Unclear} as unclear" +
                (judge.QuoteNotFound > 0 ? $". {judge.QuoteNotFound} reading(s) of \"intended\" were discarded: the words quoted for them are not in what the change says it is for" : string.Empty) +
                (judge.Unanswered > 0 ? $". {judge.Unanswered} group(s) got no reading" : string.Empty) +
                ". A reading of \"not meant\" moves an item up inside its tier; no reading moves anything down.");
        }

        if (r.Obligations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("The requirement, read against the tests. The reading is a model's and is an opinion. Two things in it were checked here: the words in the first column are in the requirement, and a test named in the third is one that fails on the old code.");
            sb.AppendLine();
            sb.AppendLine("| The requirement asks | The model's reading | Test that shows it | Why |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var o in r.Obligations)
            {
                sb.AppendLine($"| {Esc(o.Asks)} | {o.Status} | {(o.Test.Length > 0 ? "`" + Esc(o.Test) + "`" : "—")} | {Esc(o.Reason)} |");
            }
        }

        if (r.Reverts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("The change undone one part at a time. With the old lines of a part back in the new code, the tests were run again:");
            sb.AppendLine();
            sb.AppendLine("| Part | Lines | Outcome |");
            sb.AppendLine("|---|---|---|");
            foreach (var v in r.Reverts)
            {
                var outcome = v.Status switch
                {
                    "needed" => $"needed: {v.NeededByCount} test(s) fail without it, {v.NeededByOldTests} of which existed before" + (v.NeededBy.Count > 0 ? $" (e.g. `{Esc(v.NeededBy[0])}`)" : string.Empty),
                    "not-needed" => "**no test needs it**: every test passes with the old lines back",
                    "cannot-be-undone-alone" => "does not compile without the rest of the change",
                    _ => v.Status + ": inconclusive",
                };
                sb.AppendLine($"| {v.Id} | `{v.File}` {v.Start}–{v.End} | {outcome} |");
            }
        }

        var attacks = r.Attacks.Where(a => a.Attempts > 0).ToList();
        if (attacks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Tests written to break the change, by {r.AttackWriter}. What their writer says about them is not used: each file is built and run here, and a test that fails on the new code is run on the old code, which decides whether the change caused it:");
            sb.AppendLine();
            sb.AppendLine("| Attack | Unit | Tests | Outcome | Saved as |");
            sb.AppendLine("|---|---|---:|---|---|");
            foreach (var a in attacks)
            {
                sb.AppendLine($"| {a.Request} | `{Esc(a.Symbol)}` | {a.Tests} | {Esc(a.Status + ": " + a.Detail.Split('\n')[0])} | {a.TestFile ?? "—"} |");
            }

            foreach (var f in attacks.SelectMany(a => a.Failing.Select(f => (a.Request, Failure: f))))
            {
                sb.AppendLine();
                sb.AppendLine($"{f.Request}: `{Esc(f.Failure.Test)}` fails on the new code; on the old code: {f.Failure.OnOld}.");
                sb.AppendLine("```");
                sb.AppendLine(f.Failure.Message.Length > 500 ? f.Failure.Message[..500] + "…" : f.Failure.Message);
                sb.AppendLine("```");
            }
        }

        if (r.Exhaustive is { } deep)
        {
            sb.AppendLine();
            sb.AppendLine(deep.Candidates == 0
                ? "Exhaustive mode. Every member that depends on the change and has executable code is run by at least one original test, so there was nothing to probe."
                : $"Exhaustive mode. {deep.Candidates} member(s) depend on the change and are run by no original test. {deep.Attempted} were probed with {deep.Sessions} of {deep.SessionBudget} session(s); {deep.Compared} could be compared on old and new code (rows X… above). The other {deep.Candidates - deep.Compared} remain unmeasured.");
            foreach (var i in deep.Impacts)
            {
                sb.AppendLine($"- With `{Esc(i.Symbol)}` broken ({Esc(i.Break)}), {i.ChangedDependents} of {i.ProbedDependents} probed dependents changed their output" + (i.Changed.Count > 0 ? $": {Esc(string.Join(", ", i.Changed.Take(5)))}" : string.Empty));
            }
        }

        sb.AppendLine();
        sb.AppendLine("## 5. What was not checked");
        sb.AppendLine();
        Bullets(sb, r.NotChecked);

        sb.AppendLine();
        sb.AppendLine("## 6. What AYA refuses to decide");
        sb.AppendLine();
        Bullets(sb, r.RefusedToDecide);

        sb.AppendLine();
        sb.AppendLine("## 7. Limits of this analysis");
        sb.AppendLine();
        foreach (var l in r.Limits)
        {
            sb.AppendLine($"- {l}");
        }

        sb.AppendLine();
        sb.AppendLine("## 8. Sign-off");
        sb.AppendLine();
        sb.AppendLine("This page is advice with evidence. It is not an approval.");
        sb.AppendLine();
        sb.AppendLine("| | |");
        sb.AppendLine("|---|---|");
        sb.AppendLine("| Owner (named person) | |");
        sb.AppendLine("| Decision | accept / reject / request more evidence |");
        sb.AppendLine($"| Commit | `{r.HeadSha}` |");
        sb.AppendLine($"| Evidence | sha256 `{evidenceHash}` |");
        sb.AppendLine("| Looked at by the owner | section 1, down to item no. ___ |");
        sb.AppendLine("| Residual risk accepted | the items in sections 5 and 6 |");
        sb.AppendLine("| Date | |");
        return sb.ToString();
    }

    /// <summary>
    /// The one screen a person reads before approving: what to look at, in order, why, and what to check there.
    /// Everything in it is taken from the full list; nothing is decided here.
    /// </summary>
    public static string Summary(ReviewResult r)
    {
        var sb = new StringBuilder();
        var withSignal = r.Attention.Where(a => a.Tier < 6).ToList();
        var quiet = r.Attention.Count - withSignal.Count;
        sb.AppendLine($"# Before approving {r.HeadRef}");
        sb.AppendLine();
        sb.AppendLine($"`{r.HeadSha[..8]}` {Esc(r.Subject)}" + (r.Requirement is null ? string.Empty : $", for {r.Requirement}") + $". {r.Files.Count} files, {r.ChangedLinesTotal} changed lines.");
        sb.AppendLine();

        // The two questions a person has before approving, answered from what was measured and nothing else.
        var compared = r.Comparisons.Where(p => p.Purpose is "modified-unit" or "sweep" && p.Status == "compared").ToList();
        var about = r.Evidence.Where(e => e.Kind is "build" or "run" or "regression" or "old-vs-new" or "sweep" or "judge" or "attack" or "exhaustive").ToList();
        var broke = r.Regressions.Count + compared.Sum(p => p.Count("now-fails")) + r.Attacks.Count(a => a.Status == "regression-demonstrated")
            + about.Count(e => e.Kind == "build" && e.Status == "fail");
        var altered = compared.Sum(p => p.Count("changed") + p.Count("fails-differently") + p.Count("missing-on-new"));
        var shownThrough = compared.Sum(p => p.Differences.Count(d => d.ByChange == "explained"));

        // "Nothing was found" is only said when the existing tests really ran on both versions and none of the
        // checks behind this answer came back with a warning. Otherwise the answer is that it was not fully measured.
        var ran = r.Runs.Any(x => x.Label == "head" && x.Problem is null && x.Total > 0) && r.Runs.Any(x => x.Label == "base" && x.Problem is null && x.Total > 0);
        var partial = about.Any(e => e.Kind is "build" or "run" or "regression" && e.Status == "warn");
        sb.AppendLine("## Does it break what existed?");
        sb.AppendLine();
        sb.AppendLine(broke > 0
            ? "**Something that held on the old code no longer does.** Whether that was meant to change is yours to decide."
            : !ran
                ? "**Not measured.** The existing tests did not run on both versions, so nothing can be said."
                : altered > shownThrough
                    ? $"**No existing test fails, but {altered - shownThrough} input(s) give a different result than on the old code that the change's own text does not explain.**"
                    : altered > 0
                        ? $"**No existing test fails. {altered} input(s) give a different result, all of them only by lines the change itself adds or removes.** That is the change showing through, which is not the same as correct."
                    : partial
                        ? "**No existing test fails, but part of the check did not run.** Read the warnings below before taking that as an answer."
                        : compared.Count > 0
                            ? "**Nothing was found.** That covers the existing tests and the inputs that were compared, not every input."
                            : "**Nothing was found by the existing tests.** Old and new code were not compared on the same inputs (`--compare`).");
        foreach (var e in about)
        {
            sb.AppendLine($"- {e.Status.ToUpperInvariant()}: {OneLine(e.Claim)}");
        }

        sb.AppendLine();
        sb.AppendLine("## Is the change itself guarded?");
        sb.AppendLine();
        foreach (var e in r.Evidence.Where(e => e.Kind is "new-tests" or "fail-before" or "coverage" or "mutation" or "revert" or "requirement" or "written-tests"))
        {
            sb.AppendLine($"- {e.Status.ToUpperInvariant()}: {OneLine(e.Claim)}");
        }

        sb.AppendLine();
        sb.AppendLine("## Where to look");
        sb.AppendLine();
        if (withSignal.Count == 0)
        {
            sb.AppendLine("No signal was found against anything the change touches. That is not a statement that it is right: see what was not checked, below.");
        }
        else
        {
            sb.AppendLine($"**{withSignal.Count} thing(s) to look at, in this order.** How far down to go is your decision.");
            sb.AppendLine();
            foreach (var a in withSignal.Take(8))
            {
                sb.AppendLine($"{a.Rank}. **{Esc(a.What)}**" + (a.File is not null && a.Lines is { Length: > 0 } && a.Kind == "unit" ? $" (`{Path.GetFileName(a.File)}`, lines {a.Lines})" : string.Empty));
                foreach (var why in a.Why.Take(2))
                {
                    sb.AppendLine($"   - {OneLine(why)}");
                }

                if (a.Check.FirstOrDefault() is { } check)
                {
                    sb.AppendLine($"   - Check: {OneLine(check)}");
                }
            }

            if (withSignal.Count > 8)
            {
                sb.AppendLine($"{withSignal.Count - 8} more with a signal are in `report.md`, section 1.");
            }
        }

        sb.AppendLine();
        if (quiet > 0)
        {
            sb.AppendLine($"{quiet} other item(s) the change touches showed no signal. They are listed in `report.md`; no signal is not the same as correct.");
            sb.AppendLine();
        }

        sb.AppendLine("## What nobody checked");
        sb.AppendLine();
        foreach (var f in r.RefusedToDecide.Take(4))
        {
            sb.AppendLine($"- {f.Topic}: {OneLine(f.Text)}");
        }

        sb.AppendLine();
        sb.AppendLine("This is advice with evidence, not an approval. The full list, every measurement and the sign-off block are in `report.md`.");
        return sb.ToString();
    }

    private static string OneLine(string text)
    {
        var line = text.Replace("\r", " ").Replace("\n", " ");
        return line.Length > 260 ? line[..260] + "…" : line;
    }

    private static void Section(StringBuilder sb, string title, List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        sb.AppendLine($"- **{title}**");
        foreach (var line in lines)
        {
            sb.AppendLine($"  - {line.Replace("\r", " ").Replace("\n", " ")}");
        }
    }

    private static void Bullets(StringBuilder sb, List<Finding> findings)
    {
        if (findings.Count == 0)
        {
            sb.AppendLine("- Nothing recorded.");
            return;
        }

        foreach (var f in findings)
        {
            sb.AppendLine($"- **{f.Topic}.** {f.Text}");
        }
    }

    private static string Esc(string s) => s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
