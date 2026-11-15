using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aya;

/// <summary>
/// Fresh, tool-less model sessions that write code on request. Nothing a session writes is believed: the callers
/// build it, run it and measure what it does. This class only asks, and refuses code that games those checks.
/// </summary>
public sealed partial class ModelSession(string provider, string model, string effort)
{
    /// <summary>Sessions run at the same time. More than this and the provider starts slowing every one of them.</summary>
    private const int AtOnce = 8;

    /// <summary>
    /// Put before every request sent through the Antigravity command line, which would otherwise act as an
    /// agent and go and read files. A session is given its whole module in the request and nothing else.
    /// </summary>
    private const string NoTools =
        "Answer directly with the code. Do not use any tool, do not read or search any file and do not ask anything: " +
        "everything you need is in this message, and nothing outside it is yours to look at.\n\n";

    private int sessions;
    private int fallbacks;
    private volatile bool quotaSpent;

    /// <summary>
    /// Ways of forcing a state the program cannot reach by itself. A test that needs one of them to tell two
    /// versions apart is telling apart something no caller could ever observe.
    /// </summary>
    private static readonly string[] Tricks =
    [
        "GetUninitializedObject", "FormatterServices", "BindingFlags.NonPublic", "System.Reflection.Emit",
        "TypeBuilder", "DynamicMethod", "ILGenerator",
    ];

    public string Name => provider == "gemini" ? $"{model} (Gemini, through the Antigravity command line)" : $"{model} (effort {effort})";

    /// <summary>How many sessions were opened, whatever came of them.</summary>
    public int Sessions => this.sessions;

    /// <summary>Starts a session with the request and returns the code it wrote.</summary>
    public string? AskForCode(string prompt, out string error)
    {
        // A session that dies, hangs or comes back empty says nothing about the request, so it is tried once more
        // before giving up. With Gemini the second try goes to Claude: waiting twice on the same slow answer
        // would only double the wait.
        // Once the provider has said its quota is spent, asking it again only costs time: every later request of
        // this run goes straight to Claude.
        if (provider == "gemini" && this.quotaSpent)
        {
            Interlocked.Increment(ref this.fallbacks);
            return this.Ask(prompt, "claude", out error);
        }

        var code = this.Ask(prompt, provider, out error);
        if (code is not null)
        {
            return code;
        }

        if (provider == "gemini")
        {
            Interlocked.Increment(ref this.fallbacks);
            return this.Ask(prompt, "claude", out error);
        }

        return this.Ask(prompt, provider, out error);
    }

    /// <summary>Sessions that Gemini did not answer and Claude was asked instead.</summary>
    public int Fallbacks => this.fallbacks;

    /// <summary>The provider refused a request because its quota is spent. That is not a slow answer, and it is said apart.</summary>
    public bool QuotaSpent => this.quotaSpent;

    /// <summary>
    /// One session per request, all at the same time. Each session knows only its own request: that is the
    /// module it works in, and it cannot see the others. The answers come back in the order of the requests.
    /// </summary>
    public (string? Code, string Error)[] AskAll(IReadOnlyList<string> prompts)
    {
        var answers = new (string? Code, string Error)[prompts.Count];
        Parallel.For(0, prompts.Count, new ParallelOptions { MaxDegreeOfParallelism = AtOnce }, i =>
        {
            // Whatever goes wrong with one session is that request's failure and nobody else's.
            try
            {
                var code = this.AskForCode(prompts[i], out var error);
                answers[i] = (code, error);
            }
            catch (Exception ex)
            {
                answers[i] = (null, ex.Message);
            }
        });
        return answers;
    }

    private string? Ask(string prompt, string via, out string error)
    {
        Interlocked.Increment(ref this.sessions);
        var cwd = Path.Combine(Paths.AyaHome, "model-cwd");
        Directory.CreateDirectory(cwd);
        string text;
        try
        {
            if (via == "gemini")
            {
                // The request goes in as one JSON line on standard input, so its length and quotes never meet a shell.
                // An answer normally takes under a minute; past two, the session is given up on.
                var line = JsonSerializer.Serialize(new { @event = "user", message = new { content = NoTools + prompt } }) + "\n";
                var run = Proc.Run(Antigravity(), ["--input-format", "stream-json", "--output-format", "stream-json", "--model", model, "--print-timeout", "2m"], cwd, 150 * 1000, line);
                var (status, response) = LastResult(run.StdOut);
                if (!run.Ok || status != "SUCCESS" || response.Trim().Length == 0)
                {
                    if (run.StdOut.Contains("RESOURCE_EXHAUSTED", StringComparison.Ordinal) || run.StdErr.Contains("RESOURCE_EXHAUSTED", StringComparison.Ordinal))
                    {
                        this.quotaSpent = true;
                        error = $"the quota of {model} is spent";
                        return null;
                    }

                    error = run.ExitCode == -1 ? "timed out" : $"exit code {run.ExitCode}, status {(status.Length > 0 ? status : "unknown")}" + (response.Trim().Length == 0 ? ", empty answer" : string.Empty);
                    return null;
                }

                text = response;
            }
            else
            {
                var run = Proc.Run(
                    "claude",
                    ["-p", "--model", via == provider ? model : "claude-opus-5-5", "--effort", effort, "--tools", string.Empty, "--strict-mcp-config", "--no-session-persistence", "--output-format", "text"],
                    cwd,
                    6 * 60 * 1000,
                    prompt);
                if (!run.Ok || run.StdOut.Trim().Length == 0)
                {
                    error = run.ExitCode == -1 ? "timed out" : $"exit code {run.ExitCode}: {run.StdErr.Trim().Split('\n').FirstOrDefault()}";
                    return null;
                }

                text = run.StdOut;
            }
        }
        catch (AyaException ex)
        {
            error = ex.Message;
            return null;
        }

        error = string.Empty;
        var blocks = CodeBlock().Matches(text).Select(m => m.Groups[1].Value).ToList();
        return blocks.Count > 0 ? blocks.MaxBy(b => b.Length) : text;
    }

    /// <summary>The status and the answer in the last result event of an Antigravity event stream.</summary>
    public static (string Status, string Response) LastResult(string stream)
    {
        var (status, response) = (string.Empty, string.Empty);
        foreach (var raw in stream.Split('\n'))
        {
            if (!raw.Contains("\"result\"", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("result", out var result))
                {
                    status = result.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty;
                    response = result.TryGetProperty("response", out var r) ? r.GetString() ?? string.Empty : string.Empty;
                }
            }
            catch (JsonException)
            {
                // Not an event line.
            }
        }

        return (status, response);
    }

    private static string Antigravity()
    {
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "agy", "bin", OperatingSystem.IsWindows() ? "agy.exe" : "agy");
        return File.Exists(local) ? local : "agy";
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
            // Each file on its own: one that cannot be removed yet must not leave the others behind.
            foreach (var path in written)
            {
                Files.Retry(() => File.Delete(path));
            }

            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }
}
