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

    /// <summary>
    /// What was measured about the changed lines, as a fact and not a verdict:
    /// executed | partly-executed | not-executed | failing | not-measured.
    /// </summary>
    public string Status { get; set; } = "not-measured";

    /// <summary>Deliberate breaks tried on this symbol's changed lines. Empty unless mutation was requested.</summary>
    public List<Mutant> Mutants { get; } = new();

    /// <summary>Members that depend on this one and whose lines appear in the execution data.</summary>
    public int MeasuredDependents { get; set; }

    /// <summary>Of those, the ones no test executed: a break reaching them would go unseen.</summary>
    public int UnexecutedDependents { get; set; }

    public List<string> UnexecutedDependentNames { get; } = new();
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

public sealed record RunSummary(string Label, string Project, bool Built, int Passed, int Failed, int Skipped, int Total, double Seconds, List<string> BuildErrors, string? Problem);

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

    /// <summary>Everything the change touches, most deserving of attention first. Nothing is left out of it.</summary>
    public List<AttentionItem> Attention { get; set; } = new();

    public int ChangedLinesTotal { get; set; }

    public List<string> Limits { get; set; } = new();

    public List<Mutant> Mutants { get; set; } = new();

    public List<ProposedTest> ProposedTests { get; set; } = new();

    /// <summary>Who wrote the proposed tests, e.g. "claude-opus-5-5 (effort medium)". Null when none were asked for.</summary>
    public string? TestWriter { get; set; }

    public List<TestOutcome> Tests { get; set; } = new();

    /// <summary>Old code against new code on the same inputs.</summary>
    public List<Comparison> Comparisons { get; set; } = new();

    /// <summary>Tests a model wrote to make the changed code fail.</summary>
    public List<AttackResult> Attacks { get; set; } = new();

    /// <summary>Who wrote the attack tests: a model session, or whoever left the files that were collected.</summary>
    public string? AttackWriter { get; set; }

    public ExhaustiveSummary? Exhaustive { get; set; }
}
