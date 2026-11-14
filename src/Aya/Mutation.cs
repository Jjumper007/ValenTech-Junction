using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aya;

/// <summary>
/// What the optional deeper stages (mutation, written tests) need from the main review, plus a scratch copy of
/// the new code they are allowed to alter.
/// </summary>
public sealed class Workbench
{
    private string? work;

    public required Git Git { get; init; }

    public required string HeadSha { get; init; }

    public required string HeadWorktree { get; init; }

    public required string WorktreesRoot { get; init; }

    /// <summary>Repo-relative paths of the test projects in scope.</summary>
    public required List<string> TestProjects { get; init; }

    public required string RunsDir { get; init; }

    public string? SdkFolder { get; init; }

    public required Trace Trace { get; init; }

    /// <summary>Test project name → source file → line → times executed, on the unaltered new code.</summary>
    public required Dictionary<string, Dictionary<string, Dictionary<int, int>>> CoverageByProject { get; init; }

    /// <summary>"project::method" of tests that already fail on the unaltered new code.</summary>
    public required HashSet<string> FailingOnHead { get; init; }

    /// <summary>A second copy of the new code. Files altered here are always put back.</summary>
    public string Work => this.work ??= this.Git.EnsureWorktree(this.HeadSha, Path.Combine(this.WorktreesRoot, this.HeadSha[..6] + "work"));

    public string? ProjectPath(string name) => this.TestProjects.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);

    /// <summary>The test projects whose run executed the given line: the only ones worth re-running for it.</summary>
    public List<string> ProjectsExecuting(string file, int line) =>
        this.CoverageByProject
            .Where(kv => kv.Value.TryGetValue(file, out var lines) && lines.TryGetValue(line, out var hits) && hits > 0)
            .Select(kv => kv.Key)
            .ToList();

    /// <summary>Runs an action with one file of the scratch copy replaced, then restores the original bytes.</summary>
    public T WithFile<T>(string relFile, string text, Func<T> action)
    {
        var target = Path.Combine(this.Work, relFile);
        var original = File.ReadAllBytes(Path.Combine(this.HeadWorktree, relFile));
        var hasBom = original.Length >= 3 && original[0] == 0xEF && original[1] == 0xBB && original[2] == 0xBF;
        try
        {
            File.WriteAllText(target, text, new UTF8Encoding(hasBom));
            return action();
        }
        finally
        {
            File.WriteAllBytes(target, original);
        }
    }
}

/// <summary>Applies each mutant in turn and records whether a test that passed before now fails.</summary>
public static class MutationRunner
{
    public static (List<Mutant> Mutants, Dictionary<string, string> Texts) Run(Workbench w, List<ChangedSymbol> changed, int max)
    {
        var targets = changed.Where(c => !c.IsTest && c.Category is "source" or "generated").ToList();
        var all = new List<Mutant>();
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var group in targets.GroupBy(c => c.File))
        {
            var executable = group.SelectMany(c => c.CoveredLines.Concat(c.UncoveredLines)).ToHashSet();
            var executed = group.SelectMany(c => c.CoveredLines).ToHashSet();
            var text = File.ReadAllText(Path.Combine(w.HeadWorktree, group.Key));
            foreach (var (mutant, mutated) in Mutator.Generate(text, group.Key, executable))
            {
                mutant.Id = "M" + (all.Count + 1);
                if (!executed.Contains(mutant.Line))
                {
                    // No test runs this line, so none can notice a break in it. Running would only confirm that.
                    mutant.Status = "not-covered";
                }

                all.Add(mutant);
                texts[mutant.Id] = mutated;
            }
        }

        // Spend the budget across lines: one mutant for every line first, then a second for each, and so on.
        var runnable = all
            .Where(m => m.Status == "not-run")
            .GroupBy(m => (m.File, m.Line))
            .SelectMany(g => g.Select((m, round) => (Mutant: m, Round: round)))
            .OrderBy(x => x.Round)
            .Select(x => x.Mutant)
            .Take(max)
            .ToList();
        w.Trace.Info($"{all.Count} possible breaks on {all.Select(m => (m.File, m.Line)).Distinct().Count()} changed lines; running {runnable.Count}" +
            (all.Count(m => m.Status == "not-covered") is var blind and > 0 ? $"; {blind} are on lines no test executes" : string.Empty));

        foreach (var mutant in runnable)
        {
            var clock = Stopwatch.StartNew();
            mutant.Status = w.WithFile(mutant.File, texts[mutant.Id], () => Judge(w, mutant));
            mutant.Seconds = Math.Round(clock.Elapsed.TotalSeconds, 1);
            var verdict = mutant.Status switch
            {
                "killed" => $"CAUGHT by {mutant.KilledBy.FirstOrDefault()}" + (mutant.KilledByCount > 1 ? $" and {mutant.KilledByCount - 1} more" : string.Empty),
                "survived" => "NOT NOTICED by any test",
                "invalid" => "does not compile, discarded",
                "timeout" => "tests hung, counted as caught",
                _ => mutant.Status,
            };
            w.Trace.Info($"{mutant.Id,-4} {Path.GetFileName(mutant.File)}:{mutant.Line,-5} {mutant.Description,-24} {verdict}   {mutant.Seconds}s");
            w.Trace.Info($"         {mutant.Original}");
        }

        foreach (var c in targets)
        {
            c.Mutants.AddRange(all.Where(m => m.File == c.File && c.ChangedLines.Contains(m.Line)));
        }

        return (all, texts);
    }

    private static string Judge(Workbench w, Mutant mutant)
    {
        var projects = w.ProjectsExecuting(mutant.File, mutant.Line);
        foreach (var name in projects)
        {
            if (w.ProjectPath(name) is not { } path)
            {
                continue;
            }

            var run = TestRunner.Run(w.Work, path, w.RunsDir, Path.Combine("mutants", mutant.Id), null, coverage: false, w.SdkFolder, timeoutMs: 8 * 60 * 1000);
            if (run.ExitCode == -1)
            {
                return "timeout";
            }

            if (!run.Built)
            {
                return "invalid";
            }

            if (run.Results.Count == 0 && run.ExitCode != 0)
            {
                mutant.KilledBy = ["(the test run crashed)"];
                mutant.KilledByCount = 1;
                return "killed";
            }

            var killers = run.Results
                .Where(r => r.Failed && !w.FailingOnHead.Contains(r.Project + "::" + r.Method))
                .Select(r => r.Method)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            if (killers.Count > 0)
            {
                mutant.KilledBy = killers.Take(5).ToList();
                mutant.KilledByCount = killers.Count;
                return "killed";
            }
        }

        return projects.Count == 0 ? "not-covered" : "survived";
    }
}

/// <summary>One deliberate break of a changed line, and whether any test noticed it.</summary>
public sealed class Mutant
{
    public required string Id { get; set; }

    public required string File { get; init; }

    public required int Line { get; init; }

    /// <summary>What was changed, e.g. "== → !=" or "statement removed".</summary>
    public required string Description { get; init; }

    /// <summary>The original code that was altered, on one line.</summary>
    public required string Original { get; init; }

    /// <summary>killed | survived | invalid | not-covered | not-run | timeout.</summary>
    public string Status { get; set; } = "not-run";

    /// <summary>Tests that passed on the unchanged code and failed with this break.</summary>
    public List<string> KilledBy { get; set; } = new();

    public int KilledByCount { get; set; }

    public double Seconds { get; set; }
}

/// <summary>
/// Produces small, syntactically valid alterations of the code on given lines. Each one is a question put to
/// the test suite: "would you notice if this line were wrong in this way?"
/// </summary>
public static class Mutator
{
    private static readonly Dictionary<SyntaxKind, (SyntaxKind Expression, SyntaxKind Token)> Swaps = new()
    {
        [SyntaxKind.EqualsExpression] = (SyntaxKind.NotEqualsExpression, SyntaxKind.ExclamationEqualsToken),
        [SyntaxKind.NotEqualsExpression] = (SyntaxKind.EqualsExpression, SyntaxKind.EqualsEqualsToken),
        [SyntaxKind.LessThanExpression] = (SyntaxKind.GreaterThanOrEqualExpression, SyntaxKind.GreaterThanEqualsToken),
        [SyntaxKind.GreaterThanOrEqualExpression] = (SyntaxKind.LessThanExpression, SyntaxKind.LessThanToken),
        [SyntaxKind.GreaterThanExpression] = (SyntaxKind.LessThanOrEqualExpression, SyntaxKind.LessThanEqualsToken),
        [SyntaxKind.LessThanOrEqualExpression] = (SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanToken),
        [SyntaxKind.LogicalAndExpression] = (SyntaxKind.LogicalOrExpression, SyntaxKind.BarBarToken),
        [SyntaxKind.LogicalOrExpression] = (SyntaxKind.LogicalAndExpression, SyntaxKind.AmpersandAmpersandToken),
    };

    /// <summary>Returns each mutant together with the full text of the file once it is applied.</summary>
    public static List<(Mutant Mutant, string Text)> Generate(string text, string relFile, IReadOnlySet<int> lines)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        var result = new List<(Mutant, string)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(SyntaxNode target, SyntaxNode replacement, SyntaxToken anchor, string description)
        {
            var line = anchor.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            if (!lines.Contains(line))
            {
                return;
            }

            var mutated = root.ReplaceNode(target, replacement).ToFullString();
            if (mutated == text || !seen.Add(mutated))
            {
                return;
            }

            var original = string.Join(" ", target.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            result.Add((new Mutant
            {
                Id = "M" + (result.Count + 1),
                File = relFile,
                Line = line,
                Description = description,
                Original = original.Length > 110 ? original[..110] + "…" : original,
            }, mutated));
        }

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case BinaryExpressionSyntax binary when Swaps.TryGetValue(binary.Kind(), out var to):
                    var op = binary.OperatorToken;
                    Add(
                        binary,
                        SyntaxFactory.BinaryExpression(to.Expression, binary.Left, SyntaxFactory.Token(op.LeadingTrivia, to.Token, op.TrailingTrivia), binary.Right),
                        op,
                        $"{op.Text} → {SyntaxFacts.GetText(to.Token)}");
                    break;

                case IsPatternExpressionSyntax isPattern:
                    var pattern = isPattern.Pattern;
                    var flipped = pattern is UnaryPatternSyntax negated
                        ? negated.Pattern
                        : SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space), pattern.WithoutTrivia());
                    Add(isPattern, isPattern.WithPattern(flipped.WithTriviaFrom(pattern)), isPattern.IsKeyword, pattern is UnaryPatternSyntax ? "is not X → is X" : "is X → is not X");
                    break;

                case PrefixUnaryExpressionSyntax not when not.IsKind(SyntaxKind.LogicalNotExpression):
                    Add(not, not.Operand.WithTriviaFrom(not), not.OperatorToken, "!x → x");
                    break;

                case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.TrueLiteralExpression) || literal.IsKind(SyntaxKind.FalseLiteralExpression):
                    var isTrue = literal.IsKind(SyntaxKind.TrueLiteralExpression);
                    Add(
                        literal,
                        SyntaxFactory.LiteralExpression(isTrue ? SyntaxKind.FalseLiteralExpression : SyntaxKind.TrueLiteralExpression).WithTriviaFrom(literal),
                        literal.Token,
                        isTrue ? "true → false" : "false → true");
                    break;

                case IfStatementSyntax ifStatement when IsPlainCondition(ifStatement.Condition):
                    Add(ifStatement.Condition, Negate(ifStatement.Condition), ifStatement.Condition.GetFirstToken(), "if (c) → if (!c)");
                    break;

                case ConditionalExpressionSyntax conditional when IsPlainCondition(conditional.Condition):
                    Add(conditional.Condition, Negate(conditional.Condition), conditional.Condition.GetFirstToken(), "c ? a : b → !c ? a : b");
                    break;

                case ExpressionStatementSyntax statement when statement.Expression is InvocationExpressionSyntax or AssignmentExpressionSyntax or ConditionalAccessExpressionSyntax:
                    Add(statement, SyntaxFactory.EmptyStatement().WithTriviaFrom(statement), statement.GetFirstToken(), "statement removed");
                    break;

                case ContinueStatementSyntax:
                    Add(node, SyntaxFactory.EmptyStatement().WithTriviaFrom(node), node.GetFirstToken(), "continue removed");
                    break;

                case BreakStatementSyntax when node.Parent is not SwitchSectionSyntax:
                    Add(node, SyntaxFactory.EmptyStatement().WithTriviaFrom(node), node.GetFirstToken(), "break removed");
                    break;
            }
        }

        return result;
    }

    /// <summary>Conditions whose operator is not already altered by another rule, so negating adds a new mutant.</summary>
    private static bool IsPlainCondition(ExpressionSyntax condition) =>
        condition is not (IsPatternExpressionSyntax or LiteralExpressionSyntax)
        && !condition.IsKind(SyntaxKind.LogicalNotExpression)
        && !Swaps.ContainsKey(condition.Kind());

    private static ExpressionSyntax Negate(ExpressionSyntax condition) =>
        SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SyntaxFactory.ParenthesizedExpression(condition.WithoutTrivia())).WithTriviaFrom(condition);
}
