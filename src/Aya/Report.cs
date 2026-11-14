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
        sb.AppendLine("Every statement below comes from a tool run, not from a model's opinion. The source of each one is named.");
        sb.AppendLine();

        sb.AppendLine("## 1. What the owner still has to read");
        sb.AppendLine();
        sb.AppendLine($"**{r.LinesToRead} of {r.ChangedLinesTotal} changed lines.** The rest is covered by the evidence in section 4, or repeats a pattern already listed here.");
        sb.AppendLine();
        sb.AppendLine("| Depth | What | Why | Lines |");
        sb.AppendLine("|---|---|---|---:|");
        foreach (var item in r.ReadingPlan.OrderBy(i => i.Depth switch { "full" => 0, "targeted" => 1, _ => 2 }).ThenByDescending(i => i.Lines))
        {
            sb.AppendLine($"| {item.Depth} | {Esc(item.What)} | {Esc(item.Why)} | {item.Lines} |");
        }

        sb.AppendLine();
        sb.AppendLine("Rule used: *full* when a failing test reaches the symbol or none of its changed lines ran; *targeted* for lines that never ran, edits to existing members that other code depends on, tests that do not fail on the old code, one representative per repeated pattern, and files no tool can check; *light* for new code whose every changed executable line ran under passing tests.");
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
            sb.AppendLine("Repeated edit patterns (files that received exactly the same line edits):");
            sb.AppendLine();
            foreach (var p in repeated.Take(8))
            {
                sb.AppendLine($"- **{p.Files.Count} {p.Category} files**, pattern `{p.Id}`, e.g. `{p.Files[0]}`");
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
        sb.AppendLine("| Layer | Symbol | Change | Public API | Dependents | Tests that can reach it | Changed lines run / not run | Status |");
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
        sb.AppendLine("| Code under test | Project | Compiled | Passed | Failed | Seconds |");
        sb.AppendLine("|---|---|---|---:|---:|---:|");
        foreach (var run in r.Runs)
        {
            var label = run.Label switch { "head" => "new", "base" => "old", _ => "old + new tests" };
            sb.AppendLine($"| {label} | {run.Project} | {(run.Built ? "yes" : "no")} | {run.Passed} | {run.Failed} | {run.Seconds} |");
        }

        if (r.Mutants.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Deliberate breaks of the changed lines. Each row alters one line and re-runs the tests that execute it:");
            sb.AppendLine();
            sb.AppendLine("| Break | Where | Alteration | Original code | Result |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var m in r.Mutants.OrderBy(m => m.Status switch { "survived" => 0, "not-covered" => 1, "killed" or "timeout" => 2, _ => 3 }).ThenBy(m => m.Line))
            {
                var outcome = m.Status switch
                {
                    "killed" => $"caught by `{m.KilledBy.FirstOrDefault()}`" + (m.KilledByCount > 1 ? $" and {m.KilledByCount - 1} more" : string.Empty),
                    "survived" => "**not noticed by any test**",
                    "not-covered" => "no test executes this line",
                    "invalid" => "does not compile; discarded",
                    "timeout" => "tests hung; counted as caught",
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
        sb.AppendLine("| Read by the owner | the items in section 1 |");
        sb.AppendLine("| Residual risk accepted | the items in sections 5 and 6 |");
        sb.AppendLine("| Date | |");
        return sb.ToString();
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
