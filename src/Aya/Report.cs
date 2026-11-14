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
                var unit = p.Purpose == "modified-unit" ? p.Symbol : $"{p.Symbol} (depends on {p.DependsOn}; run by no test)";
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
                    sb.AppendLine($"- **{meaning}** in {g.Count()} input(s): {Esc(string.Join("; ", g.Take(4).Select(d => d.Case)))}{(g.Count() > 4 ? "; …" : string.Empty)}");
                    sb.AppendLine("  ```");
                    foreach (var line in Comparer.Delta(first.Old, first.New).Split('\n').Take(12))
                    {
                        sb.AppendLine("  " + (line.Length > 200 ? line[..200] + "…" : line));
                    }

                    sb.AppendLine("  ```");
                }
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
