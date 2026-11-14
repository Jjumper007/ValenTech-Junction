using System.Text;
using System.Text.RegularExpressions;

namespace Aya;

/// <summary>
/// Fresh, tool-less model sessions that write code on request. Nothing a session writes is believed: the callers
/// build it, run it and measure what it does. This class only asks, and refuses code that games those checks.
/// </summary>
public sealed partial class ModelSession(string model, string effort)
{
    /// <summary>
    /// Ways of forcing a state the program cannot reach by itself. A test that needs one of them to tell two
    /// versions apart is telling apart something no caller could ever observe.
    /// </summary>
    private static readonly string[] Tricks =
    [
        "GetUninitializedObject", "FormatterServices", "BindingFlags.NonPublic", "System.Reflection.Emit",
        "TypeBuilder", "DynamicMethod", "ILGenerator",
    ];

    public string Name => $"{model} (effort {effort})";

    /// <summary>How many sessions were opened, whatever came of them.</summary>
    public int Sessions { get; private set; }

    /// <summary>Starts a session with the prompt on standard input and returns the code it wrote.</summary>
    public string? AskForCode(string prompt, out string error)
    {
        // A session that dies or hangs says nothing about the request, so it is tried once more before giving up.
        var code = this.Ask(prompt, out error);
        return code ?? this.Ask(prompt, out error);
    }

    private string? Ask(string prompt, out string error)
    {
        this.Sessions++;
        var cwd = Path.Combine(Paths.AyaHome, "model-cwd");
        Directory.CreateDirectory(cwd);
        ProcResult answer;
        try
        {
            answer = Proc.Run(
                "claude",
                ["-p", "--model", model, "--effort", effort, "--tools", string.Empty, "--strict-mcp-config", "--no-session-persistence", "--output-format", "text"],
                cwd,
                6 * 60 * 1000,
                prompt);
        }
        catch (AyaException ex)
        {
            error = ex.Message;
            return null;
        }

        if (!answer.Ok || answer.StdOut.Trim().Length == 0)
        {
            error = answer.ExitCode == -1 ? "timed out" : $"exit code {answer.ExitCode}: {answer.StdErr.Trim().Split('\n').FirstOrDefault()}";
            return null;
        }

        error = string.Empty;
        var blocks = CodeBlock().Matches(answer.StdOut).Select(m => m.Groups[1].Value).ToList();
        return blocks.Count > 0 ? blocks.MaxBy(b => b.Length) : answer.StdOut;
    }

    /// <summary>The first forbidden construct the code uses, or null when it uses none.</summary>
    public static string? Trick(string code) => Tricks.FirstOrDefault(t => code.Contains(t, StringComparison.Ordinal));

    public static string TrickMessage(string trick) =>
        $"It uses {trick}. Building objects or reaching private members by force creates states the program cannot " +
        "reach by itself, so whatever such a test shows is not behaviour a caller could observe. Use only what a caller can use.";

    /// <summary>The declaration's first lines plus a window around each marked line, with gaps elided.</summary>
    public static string Excerpt(string[] lines, int start, int end, HashSet<int> marked)
    {
        var show = new SortedSet<int>(Enumerable.Range(start, Math.Min(3, end - start + 1)));
        foreach (var m in marked)
        {
            for (var l = Math.Max(start, m - 16); l <= Math.Min(end, m + 6); l++)
            {
                show.Add(l);
            }
        }

        if (marked.Count == 0)
        {
            show.UnionWith(Enumerable.Range(start, Math.Min(60, end - start + 1)));
        }

        var sb = new StringBuilder();
        var previous = start - 1;
        foreach (var l in show.Where(l => l >= 1 && l <= lines.Length))
        {
            if (l != previous + 1)
            {
                sb.AppendLine("         …");
            }

            sb.AppendLine($"{(marked.Contains(l) ? ">>" : "  ")}{l,5} | {lines[l - 1]}");
            previous = l;
        }

        return sb.ToString();
    }

    /// <summary>
    /// The existing test that best shows how to reach a unit: one that came with the change and was shown to
    /// exercise it, then one whose project executed the file, then the closest in the graph.
    /// </summary>
    public static Node? FindPattern(Workbench w, CodeGraph graph, ReviewResult result, string nodeId, string file)
    {
        var changedTests = result.ChangedSymbols.Where(c => c.IsTest).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var demonstrating = result.ProbedTests.Where(p => p.Verdict == "discriminating").Select(p => MethodOf(p.Name)).ToHashSet(StringComparer.Ordinal);
        return graph.Reach([nodeId], reverse: true)
            .Select(kv => (Node: graph.Nodes[kv.Key], Distance: kv.Value))
            .Where(x => x.Node is { IsTest: true, TestName: not null } && w.ProjectPath(x.Node.Project) is not null)
            .OrderByDescending(x => demonstrating.Contains(x.Node.TestName!))
            .ThenByDescending(x => changedTests.Contains(x.Node.Id))
            .ThenByDescending(x => w.CoverageByProject.TryGetValue(x.Node.Project, out var cov) && cov.ContainsKey(file))
            .ThenBy(x => x.Distance)
            .ThenBy(x => x.Node.TestName, StringComparer.Ordinal)
            .Select(x => x.Node)
            .FirstOrDefault();
    }

    /// <summary>The prompt section that shows an existing test to imitate.</summary>
    public static void AppendPattern(StringBuilder sb, Workbench w, Node pattern)
    {
        var testSpan = pattern.Spans[0];
        var testLines = File.ReadAllLines(Path.Combine(w.HeadWorktree, testSpan.File));
        sb.AppendLine("## An existing test that reaches this code. Follow its style, helpers and usings.");
        sb.AppendLine($"File: {testSpan.File}   Test: {pattern.TestName}");
        sb.AppendLine("```csharp");
        if (testLines.Length <= 260)
        {
            sb.AppendLine(string.Join("\n", testLines));
        }
        else
        {
            sb.AppendLine(string.Join("\n", testLines.Take(40)));
            sb.AppendLine("// …");
            sb.AppendLine(string.Join("\n", testLines.Skip(testSpan.Start - 1).Take(Math.Min(120, testSpan.End - testSpan.Start + 1))));
        }

        sb.AppendLine("```");
    }

    public static string MethodOf(string testName) => testName.IndexOf('(') is var i and > 0 ? testName[..i] : testName;

    [GeneratedRegex(@"```(?:csharp|cs|c#)?[ \t]*\r?\n(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CodeBlock();
}

/// <summary>Files a model wrote, placed in a test project for one run and always taken out again.</summary>
public static class Candidates
{
    public static T With<T>(string worktree, string testProject, IEnumerable<(string Name, string Code)> files, Func<T> action)
    {
        var dir = Path.Combine(worktree, Path.GetDirectoryName(testProject)!, "AyaCandidates");
        Directory.CreateDirectory(dir);
        var written = new List<string>();
        try
        {
            foreach (var (name, code) in files)
            {
                var path = Path.Combine(dir, name);
                File.WriteAllText(path, code);
                written.Add(path);
            }

            return action();
        }
        finally
        {
            foreach (var path in written)
            {
                File.Delete(path);
            }

            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }
}
