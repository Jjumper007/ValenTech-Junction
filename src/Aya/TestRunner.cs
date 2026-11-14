using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Aya;

public sealed record TestResult(string Project, string Name, string Outcome, double Milliseconds, string? Message)
{
    public string Key => this.Project + "::" + this.Name;

    public bool Passed => this.Outcome == "Passed";

    public bool Failed => this.Outcome == "Failed";

    /// <summary>The test method, without the arguments a data-driven test appends.</summary>
    public string Method => this.Name.IndexOf('(') is var i and > 0 ? this.Name[..i] : this.Name;
}

public sealed class TestRun
{
    public required string Label { get; init; }

    public required string Project { get; init; }

    /// <summary>False when the compiler reported errors, so no test could run.</summary>
    public bool Built { get; init; }

    /// <summary>The run was stopped at the time limit. Says nothing about the code either way.</summary>
    public bool TimedOut { get; init; }

    /// <summary>It compiled, but the test runner ended in error without reporting a single result.</summary>
    public bool Crashed => this.Built && !this.TimedOut && this.Results.Count == 0 && this.ExitCode != 0;

    /// <summary>Too many tests were named to fit a filter, so the whole project ran instead.</summary>
    public bool FilterDropped { get; init; }

    /// <summary>Why this run gives no evidence, or null when it ran normally.</summary>
    public string? Problem =>
        !this.Built ? "did not compile"
        : this.TimedOut ? "stopped at the time limit"
        : this.Crashed ? "the test runner ended in error without results"
        : null;

    public int ExitCode { get; init; }

    public double Seconds { get; init; }

    public List<TestResult> Results { get; init; } = new();

    public List<string> BuildErrors { get; init; } = new();

    public string? CoverageFile { get; init; }

    public string? ResultsDirectory { get; init; }

    /// <summary>
    /// The build said an analyzer or source generator was skipped because it needs a newer compiler than the
    /// one in use (CS9057). Code that depends on generated sources then fails to compile.
    /// </summary>
    public bool NeedsNewerCompiler { get; init; }

    public int PassedCount => this.Results.Count(r => r.Passed);

    public int FailedCount => this.Results.Count(r => r.Failed);

    /// <summary>Neither passed nor failed: skipped, not executed or inconclusive. Never evidence of anything.</summary>
    public int SkippedCount => this.Results.Count(r => !r.Passed && !r.Failed);

    public RunSummary Summary() =>
        new(this.Label, this.Project, this.Built, this.PassedCount, this.FailedCount, this.SkippedCount, this.Results.Count, this.Seconds, this.BuildErrors, this.Problem);
}

public static partial class TestRunner
{
    /// <summary>Builds a test project and runs it, optionally recording which source lines were executed.</summary>
    /// <param name="sdkFolder">
    /// Folder to launch dotnet from. The SDK is chosen by the global.json found from there upwards, so launching
    /// from the worktree honours the repo's pinned SDK and launching from elsewhere uses the newest one installed.
    /// </param>
    public static TestRun Run(string worktree, string projectRel, string resultsDir, string label, IReadOnlyCollection<string>? onlyMethods, bool coverage, string? sdkFolder = null, int timeoutMs = 30 * 60 * 1000)
    {
        var projectName = Path.GetFileNameWithoutExtension(projectRel);
        var dir = Path.Combine(resultsDir, label, projectName);
        Directory.CreateDirectory(dir);

        var args = new List<string>
        {
            "test", Path.Combine(worktree, projectRel), "-c", "Debug", "--nologo", "-v", "q",
            "--logger", "trx;LogFileName=results.trx", "--results-directory", dir,
        };
        if (coverage)
        {
            args.Add("--collect");
            args.Add("Code Coverage;Format=cobertura");
        }

        // A filter with too many terms exceeds the command line limit; past that, run the whole project.
        if (onlyMethods is { Count: > 0 and <= 60 })
        {
            args.Add("--filter");
            args.Add(string.Join("|", onlyMethods.Select(m => "FullyQualifiedName~" + m)));
        }

        var clock = Stopwatch.StartNew();
        var proc = Proc.Run(Dotnet.Exe, args, sdkFolder ?? worktree, timeoutMs);
        clock.Stop();
        File.WriteAllText(Path.Combine(dir, "dotnet-test.log"), proc.All);

        // Shown to the reviewer, so drop the machine-specific worktree prefix and the trailing project tag.
        var errors = BuildError().Matches(proc.All)
            .Select(m => m.Value.Trim().Replace(worktree + Path.DirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.LastIndexOf(" [", StringComparison.Ordinal) is var i and > 0 ? e[..i] : e)
            .Distinct()
            .Take(20)
            .ToList();
        var results = new List<TestResult>();
        foreach (var trx in Directory.EnumerateFiles(dir, "*.trx", SearchOption.AllDirectories))
        {
            results.AddRange(ParseTrx(trx, projectName));
        }

        return new TestRun
        {
            Label = label,
            Project = projectName,
            Built = errors.Count == 0,
            TimedOut = proc.ExitCode == -1,
            FilterDropped = onlyMethods is { Count: > 60 },
            ExitCode = proc.ExitCode,
            Seconds = Math.Round(clock.Elapsed.TotalSeconds, 1),
            Results = results,
            BuildErrors = errors,
            CoverageFile = Directory.EnumerateFiles(dir, "*.cobertura.xml", SearchOption.AllDirectories).FirstOrDefault(),
            ResultsDirectory = dir,
            NeedsNewerCompiler = proc.All.Contains("CS9057", StringComparison.Ordinal),
        };
    }

    public static IEnumerable<TestResult> ParseTrx(string file, string project)
    {
        var doc = XDocument.Load(file);
        var ns = doc.Root!.Name.Namespace;
        foreach (var r in doc.Descendants(ns + "UnitTestResult"))
        {
            var name = (string?)r.Attribute("testName");
            if (name is null)
            {
                continue;
            }

            var duration = TimeSpan.TryParse((string?)r.Attribute("duration"), out var d) ? d.TotalMilliseconds : 0;
            var message = r.Descendants(ns + "Message").FirstOrDefault()?.Value;
            if (message is { Length: > 600 })
            {
                message = message[..600] + "…";
            }

            yield return new TestResult(project, name, (string?)r.Attribute("outcome") ?? "Unknown", Math.Round(duration, 1), message);
        }
    }

    /// <summary>Reads a Cobertura report into file → line → hit count, with files relative to the worktree.</summary>
    public static Dictionary<string, Dictionary<int, int>> ParseCoverage(string file, string worktree)
    {
        var map = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase);
        var doc = XDocument.Load(file);
        foreach (var cls in doc.Descendants("class"))
        {
            var filename = (string?)cls.Attribute("filename");
            if (string.IsNullOrEmpty(filename))
            {
                continue;
            }

            var full = Path.IsPathRooted(filename) ? filename : Path.Combine(worktree, filename);
            var rel = Paths.Rel(worktree, full);
            if (rel.StartsWith("..", StringComparison.Ordinal))
            {
                continue;
            }

            if (!map.TryGetValue(rel, out var lines))
            {
                map[rel] = lines = new Dictionary<int, int>();
            }

            foreach (var line in cls.Descendants("line"))
            {
                if (int.TryParse((string?)line.Attribute("number"), out var number) && int.TryParse((string?)line.Attribute("hits"), out var hits))
                {
                    lines[number] = lines.TryGetValue(number, out var existing) ? Math.Max(existing, hits) : hits;
                }
            }
        }

        return map;
    }

    [GeneratedRegex(@"^.*: error [A-Z]+\d+:.*$", RegexOptions.Multiline)]
    private static partial Regex BuildError();
}
