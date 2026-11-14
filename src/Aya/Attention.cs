using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aya;

/// <summary>
/// One thing the change touches, with everything that was measured about it under four headings. Risk is not
/// one number here: how likely it is to be wrong, how much breaks if it is, whether anything would notice, and
/// how far the analysis itself can be trusted are shown apart, because they are known with very different certainty.
/// </summary>
public sealed class AttentionItem
{
    public int Rank { get; set; }

    public required int Tier { get; init; }

    public required string TierName { get; init; }

    public required string What { get; init; }

    /// <summary>unit | deleted-code | similar-files | file | tests.</summary>
    public required string Kind { get; init; }

    public string? File { get; init; }

    public string? Lines { get; init; }

    public int Dependents { get; init; }

    /// <summary>Why it is in this tier.</summary>
    public List<string> Why { get; } = new();

    /// <summary>Indications of how likely it is to be wrong. The weakest of the four: there is no direct measurement.</summary>
    public List<string> Likelihood { get; } = new();

    /// <summary>How much stops working if it is wrong.</summary>
    public List<string> Impact { get; } = new();

    /// <summary>Whether anything would notice if it were wrong.</summary>
    public List<string> Detection { get; } = new();

    /// <summary>What the analysis could not see here.</summary>
    public List<string> Blind { get; } = new();

    /// <summary>What the person who signs should check.</summary>
    public List<string> Check { get; } = new();
}

/// <summary>What was measured about one unit, reduced to what the ordering rule looks at.</summary>
public sealed record Signals(
    bool ModifiedExisting,
    int Dependents,
    bool BreakDemonstrated,
    bool ResultChanged,
    bool DetectionGap,
    bool NotMeasured,
    bool PossibleDefect,
    bool Blind);

/// <summary>
/// Orders everything the change touches by how much it deserves a person's attention. The rule is fixed and
/// printed in the report so the owner can disagree with it. Nothing is ever removed from the list: the last
/// tier means no signal was found, not that the code is safe.
/// </summary>
public static class Attention
{
    public static readonly string[] TierNames =
    [
        string.Empty,
        "Breaks something that worked: demonstrated",
        "Changes a result that existed before",
        "Existing code others depend on, where a mistake would go unnoticed",
        "Could not be measured",
        "A mistake here would go unnoticed",
        "No signal found",
    ];

    public const string Rule =
        "Tier 1: an existing test now fails because of it, an input that gave a result now fails, or a test written to break it " +
        "fails on the new code and passes on the old. Tier 2: an input gives a different result than on the old code. " +
        "Tier 3: it modifies code that existed, other code depends on it, and a line is never executed or can be broken without " +
        "any test failing. Tier 4: the analysis could not measure it (deleted code, declarations, no break could be tried, " +
        "generated or conditionally compiled code, a failing test nothing can arbitrate). Tier 5: a line is never executed or can be " +
        "broken unnoticed, in code nothing else depends on or that is new. Tier 6: none of the above was found. Inside a tier, the " +
        "unit with more code depending on it comes first.";

    /// <summary>The fixed ordering rule.</summary>
    public static int Tier(Signals s) =>
        s.BreakDemonstrated ? 1
        : s.ResultChanged ? 2
        : s.ModifiedExisting && s.Dependents > 0 && s.DetectionGap ? 3
        : s.NotMeasured || s.PossibleDefect || s.Blind ? 4
        : s.DetectionGap ? 5
        : 6;

    public static List<AttentionItem> Build(ReviewResult result, string headWorktree)
    {
        var items = new List<AttentionItem>();
        var files = result.Files.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);
        var mutationRan = result.Mutants.Count > 0;
        var regressed = result.Regressions.Select(r => ModelSession.MethodOf(r).Trim()).ToHashSet(StringComparer.Ordinal);
        var sources = new Dictionary<string, (string[] Lines, SyntaxNode Root)>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in result.ChangedSymbols.Where(c => !c.IsTest))
        {
            var comparisons = result.Comparisons.Where(p => p.Purpose == "modified-unit" && p.SymbolId == c.Id).ToList();
            var compared = comparisons.Where(p => p.Status == "compared").ToList();
            var attacks = result.Attacks.Where(a => a.SymbolId == c.Id).ToList();
            var judged = c.Mutants.Where(m => m.Status is "killed" or "survived").ToList();
            var unnoticed = c.Mutants.Where(m => m.Status == "survived").ToList();
            var executable = c.CoveredLines.Count + c.UncoveredLines.Count;
            var brokenTests = c.FailingTests.Where(regressed.Contains).ToList();
            var nowFails = compared.Sum(p => p.Count("now-fails"));
            var changedResults = compared.Sum(p => p.Count("changed") + p.Count("fails-differently") + p.Count("missing-on-new"));
            var regressions = attacks.Where(a => a.Status == "regression-demonstrated").ToList();
            var blind = BlindSpots(c, files, headWorktree, sources);
            var noBreakTried = mutationRan && executable > 0 && judged.Count == 0;
            var undeclared = c.Status == "not-measured";

            var signals = new Signals(
                ModifiedExisting: c.ChangeKind == "modified",
                Dependents: c.Dependents,
                BreakDemonstrated: brokenTests.Count > 0 || nowFails > 0 || regressions.Count > 0,
                ResultChanged: changedResults > 0,
                DetectionGap: c.UncoveredLines.Count > 0 || unnoticed.Count > 0,
                NotMeasured: undeclared || noBreakTried,
                PossibleDefect: attacks.Any(a => a.Status == "possible-defect"),
                Blind: blind.Count > 0);
            var tier = Tier(signals);
            var item = new AttentionItem
            {
                Tier = tier,
                TierName = TierNames[tier],
                What = c.Display,
                Kind = "unit",
                File = c.File,
                Lines = ReviewCommand.Ranges(c.ChangedLines),
                Dependents = c.Dependents,
            };

            // Why it sits where it does.
            if (brokenTests.Count > 0)
            {
                item.Why.Add($"{brokenTests.Count} test(s) that passed on the old code fail on the new code and reach it: {string.Join(", ", brokenTests.Take(3))}");
            }

            if (nowFails > 0)
            {
                item.Why.Add($"{nowFails} input(s) that gave a result on the old code fail on the new code");
            }

            foreach (var a in regressions)
            {
                item.Why.Add($"a test written to break it fails on the new code and passes on the old ({a.TestFile})");
            }

            if (changedResults > 0)
            {
                item.Why.Add($"{changedResults} input(s) give a different result than on the old code");
            }

            if (tier == 3)
            {
                item.Why.Add($"it modifies code that existed, {c.Dependents} member(s) depend on it, and a mistake in it would go unnoticed");
            }

            if (undeclared)
            {
                item.Why.Add(!result.Runs.Any(r => r.Label == "head" && r.Problem is null)
                    ? "no test run produced results, so nothing was measured about it"
                    : "a declaration changed: there is no line to execute, so no test run says anything about it");
            }
            else if (noBreakTried)
            {
                item.Why.Add(c.Mutants.Count == 0 ? "none of its changed lines can be broken by the alterations AYA knows" : "no break of its lines was tried (budget, or no test executes them)");
            }

            foreach (var a in attacks.Where(a => a.Status == "possible-defect"))
            {
                item.Why.Add($"a test written to break it fails on the new code, and the old code cannot say who is wrong ({a.TestFile})");
            }

            if (tier == 4 && blind.Count > 0 && item.Why.Count == 0)
            {
                item.Why.Add("the analysis cannot see all of it: " + blind[0]);
            }

            if (tier == 5)
            {
                item.Why.Add("a mistake in it would go unnoticed, and nothing else was found against it");
            }

            if (tier == 6)
            {
                item.Why.Add("nothing was found against it; that is not a statement that it is correct");
            }

            // How likely it is to be wrong: indications only.
            item.Likelihood.Add(c.ChangeKind == "modified" ? $"modifies code that already existed ({c.ChangedLines.Count} line(s))" : $"new code ({c.ChangedLines.Count} line(s))");
            if (Nature(c, headWorktree, sources) is { Count: > 0 } nature)
            {
                item.Likelihood.Add("the changed lines touch " + string.Join(", ", nature));
            }

            foreach (var p in compared)
            {
                item.Likelihood.Add(p.Differences.Count == 0
                    ? $"old and new code give the same result on {p.Cases} input(s) ({p.ProbeFile})"
                    : $"old and new code differ on {p.Differences.Count} of {p.Cases} input(s): {string.Join(", ", p.Differences.GroupBy(d => d.Kind).Select(g => $"{g.Count()} {g.Key}"))} ({p.ProbeFile})");
            }

            foreach (var p in comparisons.Where(p => p.Status is not ("compared" or "not-attempted")))
            {
                item.Likelihood.Add($"old and new code could not be compared: {p.Status}");
            }

            foreach (var a in attacks.Where(a => a.Status == "resisted"))
            {
                item.Likelihood.Add($"{a.Tests} test(s) a model wrote to break it all pass ({a.TestFile})");
            }

            foreach (var a in attacks.Where(a => a.Status == "fails-on-both"))
            {
                item.Likelihood.Add($"a test written to break it fails on the old code as well, so the change is not the cause ({a.TestFile})");
            }

            // How much breaks if it is wrong.
            item.Impact.Add(c.Dependents == 0
                ? "nothing in the analysed code depends on it"
                : $"{c.Dependents} member(s) depend on it, by the compiler's account (an upper bound)" + (c.PublicDependents.Count > 0 ? $", among them public API such as {string.Join(", ", c.PublicDependents.Take(2))}" : string.Empty));
            if (judged.Count > 0)
            {
                var worst = c.Mutants.Where(m => m.Status == "killed").OrderByDescending(m => m.KilledByCount).FirstOrDefault();
                item.Impact.Add(worst is null
                    ? "no break of it made any test fail, so how much it would take down is unknown"
                    : $"breaking line {worst.Line} made {worst.KilledByCount} test(s) fail (measured)");
            }

            if (c.MeasuredDependents > 0)
            {
                item.Impact.Add($"{c.UnexecutedDependents} of the {c.MeasuredDependents} dependents with executable code are run by no test, so a break reaching them would not be seen" +
                    (c.UnexecutedDependentNames.Count > 0 ? $" (e.g. {string.Join(", ", c.UnexecutedDependentNames.Take(2))})" : string.Empty));
            }

            foreach (var impact in result.Exhaustive?.Impacts.Where(i => i.Symbol == c.Display) ?? [])
            {
                item.Impact.Add($"with it broken, {impact.ChangedDependents} of {impact.ProbedDependents} probed dependents that no test executes changed their output");
            }

            if (c.PublicApi)
            {
                item.Impact.Add("it is public API: code outside this repository may depend on it");
            }

            // Whether anything would notice.
            if (executable > 0)
            {
                item.Detection.Add($"{c.CoveredLines.Count} of {executable} changed executable line(s) ran under the tests" + (c.UncoveredLines.Count > 0 ? $"; never ran: {ReviewCommand.Ranges(c.UncoveredLines)}" : string.Empty));
            }

            if (judged.Count > 0)
            {
                item.Detection.Add($"{judged.Count - unnoticed.Count} of {judged.Count} deliberate break(s) made a test fail" +
                    (unnoticed.Count > 0 ? "; unnoticed: " + string.Join("; ", unnoticed.Take(3).Select(m => $"line {m.Line} ({m.Description})")) : string.Empty));
                var ownOnly = c.Mutants.Count(m => m.Status == "killed" && m.CaughtByOldTests == 0);
                if (ownOnly > 0)
                {
                    item.Detection.Add($"{ownOnly} of those were noticed only by tests that came with the change, none by a test that existed before");
                }
            }

            var inconclusive = c.Mutants.Count(m => m.Status is "timeout" or "crashed");
            if (inconclusive > 0)
            {
                item.Detection.Add($"{inconclusive} break(s) hung or crashed the test run: inconclusive, not counted as noticed");
            }

            var untried = c.Mutants.Count(m => m.Status is "not-run" or "not-covered");
            if (untried > 0)
            {
                item.Detection.Add($"{untried} possible break(s) were not tried (budget, or on a line no test executes)");
            }

            if (!mutationRan)
            {
                item.Detection.Add("no deliberate break was tried (run with --mutate): a line that ran is not a line that is checked");
            }

            item.Blind.AddRange(blind);

            // What the person who signs should look at.
            if (brokenTests.Count > 0 || nowFails > 0 || regressions.Count > 0)
            {
                item.Check.Add("Decide whether each demonstrated break is intended; if not, the change is wrong.");
            }

            if (changedResults > 0)
            {
                item.Check.Add("Go through the inputs whose result changed and confirm every change is one the requirement asks for.");
            }

            foreach (var m in unnoticed.Take(3))
            {
                item.Check.Add($"Line {m.Line}: decide what `{m.Original}` must do. Altering it ({m.Description}) makes no test fail.");
            }

            if (c.UncoveredLines.Count > 0)
            {
                item.Check.Add($"Lines {ReviewCommand.Ranges(c.UncoveredLines)} never run under any test: read them, or have them exercised.");
            }

            if (c.UnexecutedDependents > 0)
            {
                item.Check.Add($"{c.UnexecutedDependents} dependent(s) are run by no test: check the ones that rely on what changed here.");
            }

            if (undeclared || noBreakTried)
            {
                item.Check.Add("Read the changed lines: no measurement stands in for it.");
            }

            if (attacks.Any(a => a.Status == "possible-defect"))
            {
                item.Check.Add("Read the failing test and decide whether the test or the code is wrong.");
            }

            items.Add(item);
        }

        AddDeletions(items, result);
        AddFiles(items, result);
        AddWeakTests(items, result);

        var rank = 0;
        var ordered = items
            .OrderBy(i => i.Tier)
            .ThenByDescending(i => i.Dependents)
            .ThenBy(i => i.What, StringComparer.Ordinal)
            .ToList();
        foreach (var item in ordered)
        {
            item.Rank = ++rank;
        }

        return ordered;
    }

    /// <summary>Deleted code has no unit in a graph of the new code, so it is listed as not analysed instead of vanishing.</summary>
    private static void AddDeletions(List<AttentionItem> items, ReviewResult result)
    {
        foreach (var f in result.Files.Where(f => f.Category is "source" or "generated" or "template"))
        {
            if (f.Status == "D")
            {
                var item = Item(4, $"deleted file {f.Path}", "deleted-code", f.Path, null);
                item.Why.Add($"the whole file was deleted ({f.Deleted} lines); the graph is built from the new code, so what used it is unknown");
                item.Blind.Add("deleted code is not analysed");
                item.Check.Add("Search for what used the types and members this file declared.");
                items.Add(item);
                continue;
            }

            var removedOnly = f.Hunks.Where(h => h.NewLen == 0 && h.OldLen > 0).ToList();
            if (removedOnly.Count > 0)
            {
                var lines = removedOnly.Sum(h => h.OldLen);
                var item = Item(4, $"{lines} line(s) deleted from {f.Path}", "deleted-code", f.Path, string.Join(", ", removedOnly.Take(6).Select(h => $"old line {h.OldStart}")));
                item.Why.Add($"{removedOnly.Count} block(s) were removed with nothing put in their place; deleted code has no unit in the graph, so its consumers were not looked for");
                item.Blind.Add("deleted code is not analysed");
                item.Check.Add("Read what was removed and confirm nothing still relied on it.");
                items.Add(item);
            }
        }
    }

    private static void AddFiles(List<AttentionItem> items, ReviewResult result)
    {
        var grouped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in result.Patterns.Where(p => p.Files.Count > 1))
        {
            grouped.UnionWith(p.Files);
            var exact = p.Variants.Count == 1;
            var item = Item(exact ? 6 : 4, $"{p.Files.Count} {p.Category} files with a similar edit (e.g. {p.Files[0]})", "similar-files", p.Files[0], null);
            if (exact)
            {
                item.Why.Add($"all {p.Files.Count} files received exactly the same edit ({p.Variants[0].EditedLines} line(s)); nothing was found against them");
            }
            else
            {
                item.Why.Add($"the files use the same edit but not identically: {p.Variants.Count} variants, of " +
                    string.Join(", ", p.Variants.Select(v => $"{v.Files.Count} file(s) with {v.EditedLines} edited line(s)")));
                foreach (var odd in p.Variants.Where(v => v.Files.Count <= 2))
                {
                    item.Check.Add($"Odd one out: {string.Join(", ", odd.Files)} ({odd.EditedLines} edited line(s)).");
                }

                item.Check.Add("Read one file of each variant; similar is not the same.");
            }

            if (p.Category == "test-snapshot")
            {
                item.Detection.Add("these are expected outputs: the tests compare what the code produces with them, and were rewritten by the same change, so they show the output is stable, not that it is right");
            }

            item.Likelihood.Add("the edit: " + string.Join("  ", p.Edits.Take(4)));
            items.Add(item);
        }

        foreach (var f in result.Files.Where(f => !grouped.Contains(f.Path) && f.Category is "docs" or "build-config" or "other" or "template" or "test-snapshot"))
        {
            var tier = f.Category is "docs" or "test-snapshot" ? 6 : 4;
            var item = Item(tier, f.Path, "file", f.Path, $"{f.Added + f.Deleted} changed line(s)");
            item.Why.Add(f.Category switch
            {
                "docs" => "documentation: no tool checks that the text is accurate, and it cannot break the code",
                "test-snapshot" => "an expected-output file rewritten by the change",
                "build-config" => "build configuration: it can change how everything compiles, and no check here covers it",
                "template" => "a template that generates code: it was not re-run to confirm the committed output is what it produces",
                _ => "no automated check applies to this kind of file",
            });
            items.Add(item);
        }
    }

    private static void AddWeakTests(List<AttentionItem> items, ReviewResult result)
    {
        var weak = result.ProbedTests.Where(p => p.Verdict != "discriminating").ToList();
        if (weak.Count == 0)
        {
            return;
        }

        var item = Item(5, $"{weak.Count} of the {result.ProbedTests.Count} tests that came with the change", "tests", null, null);
        item.Why.Add("they were not shown to fail without the change, so they do not demonstrate it");
        foreach (var g in weak.GroupBy(p => p.Verdict))
        {
            item.Detection.Add(g.Key switch
            {
                "non-discriminating" => $"{g.Count()} pass on the old code too",
                "needs-new-code" => $"{g.Count()} cannot be compiled against the old code, so they could not be run there",
                _ => $"{g.Count()} gave no result on the old code",
            } + ": " + string.Join(", ", g.Take(4).Select(p => p.Name)) + (g.Count() > 4 ? ", …" : string.Empty));
        }

        item.Check.Add("Read these tests: what they assert may hold whether or not the change is right.");
        items.Add(item);
    }

    private static AttentionItem Item(int tier, string what, string kind, string? file, string? lines) =>
        new() { Tier = tier, TierName = TierNames[tier], What = what, Kind = kind, File = file, Lines = lines };

    /// <summary>What the changed lines of a unit consist of, read from the syntax tree.</summary>
    private static List<string> Nature(ChangedSymbol c, string headWorktree, Dictionary<string, (string[] Lines, SyntaxNode Root)> sources)
    {
        if (Load(c.File, headWorktree, sources) is not { } source)
        {
            return [];
        }

        var lines = c.ChangedLines.ToHashSet();
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var node in source.Root.DescendantNodes())
        {
            if (!lines.Contains(node.GetLocation().GetLineSpan().StartLinePosition.Line + 1))
            {
                continue;
            }

            switch (node)
            {
                case IfStatementSyntax or ConditionalExpressionSyntax or SwitchStatementSyntax or SwitchExpressionSyntax or WhileStatementSyntax or IsPatternExpressionSyntax:
                case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalAndExpression) || b.IsKind(SyntaxKind.LogicalOrExpression):
                    found.Add("a condition");
                    break;
                case ThrowStatementSyntax or ThrowExpressionSyntax or TryStatementSyntax or CatchClauseSyntax:
                    found.Add("error handling");
                    break;
                case ConditionalAccessExpressionSyntax:
                    found.Add("null handling");
                    break;
                case ForStatementSyntax or ForEachStatementSyntax:
                    found.Add("a loop");
                    break;
                case BaseMethodDeclarationSyntax or PropertyDeclarationSyntax when c.ChangeKind == "modified":
                    found.Add("a declaration's signature");
                    break;
            }
        }

        return found.ToList();
    }

    /// <summary>Reasons the analysis sees less than the whole of a unit.</summary>
    private static List<string> BlindSpots(ChangedSymbol c, Dictionary<string, FileChange> files, string headWorktree, Dictionary<string, (string[] Lines, SyntaxNode Root)> sources)
    {
        var blind = new List<string>();
        if (files.TryGetValue(c.File, out var file) && file.Category is "generated" or "template")
        {
            blind.Add("it is generated code: what produces it was not re-run, and the graph does not link it to its generator");
        }

        if (Load(c.File, headWorktree, sources) is { } source)
        {
            // The #if a changed line sits under decides in which builds it exists at all.
            var open = new Stack<string>();
            var conditions = new SortedSet<string>(StringComparer.Ordinal);
            var changed = c.ChangedLines.ToHashSet();
            for (var i = 0; i < source.Lines.Length; i++)
            {
                var text = source.Lines[i].TrimStart();
                if (text.StartsWith("#if", StringComparison.Ordinal))
                {
                    open.Push(text[3..].Trim());
                }
                else if (text.StartsWith("#endif", StringComparison.Ordinal) && open.Count > 0)
                {
                    open.Pop();
                }
                else if (open.Count > 0 && changed.Contains(i + 1))
                {
                    conditions.Add(open.Peek());
                }
            }

            foreach (var condition in conditions)
            {
                blind.Add($"changed lines are compiled only under `{condition}`; which configurations the tests were built for was not recorded");
            }
        }

        return blind;
    }

    private static (string[] Lines, SyntaxNode Root)? Load(string file, string headWorktree, Dictionary<string, (string[] Lines, SyntaxNode Root)> sources)
    {
        if (sources.TryGetValue(file, out var cached))
        {
            return cached;
        }

        var path = Path.Combine(headWorktree, file);
        if (!System.IO.File.Exists(path) || !file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var text = System.IO.File.ReadAllText(path);
        return sources[file] = (text.Replace("\r", string.Empty).Split('\n'), CSharpSyntaxTree.ParseText(text).GetRoot());
    }
}
