using System.Diagnostics;
using System.Text;

namespace Aya;

/// <summary>An error the user can act on; printed without a stack trace.</summary>
public sealed class AyaException(string message) : Exception(message);

public sealed record ProcResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Ok => ExitCode == 0;

    public string All => StdErr.Length > 0 ? StdOut + "\n" + StdErr : StdOut;
}

/// <summary>
/// File operations that put a working copy back as it was. Right after a process is killed its files can still
/// be held for a moment, and giving up at the first refusal would leave the copy altered.
/// </summary>
public static class Files
{
    public static void Retry(Action action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 10)
            {
                Thread.Sleep(300);
            }
        }
    }
}

public static class Proc
{
    public static ProcResult Run(string file, IEnumerable<string> args, string? cwd = null, int timeoutMs = 30 * 60 * 1000, string? stdin = null)
    {
        var psi = new ProcessStartInfo(file)
        {
            WorkingDirectory = cwd ?? Environment.CurrentDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        // Tool output is parsed, so keep it in English regardless of the machine locale.
        psi.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        psi.Environment["DOTNET_NOLOGO"] = "1";

        Process process;
        try
        {
            process = Process.Start(psi) ?? throw new AyaException($"cannot start '{file}'");
        }
        catch (Exception ex) when (ex is not AyaException)
        {
            throw new AyaException($"cannot start '{file}': {ex.Message}");
        }

        using (process)
        {
            // The output is read from the start, so a child that writes a lot before reading its input cannot
            // fill a pipe and stall both sides.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try
            {
                // Written as UTF-8 bytes so the text survives whatever code page the console uses. The input is
                // always closed, so a child that asks for something gets end-of-file instead of waiting on a console.
                using var input = process.StandardInput.BaseStream;
                if (stdin is not null)
                {
                    var bytes = Encoding.UTF8.GetBytes(stdin);
                    input.Write(bytes, 0, bytes.Length);
                }
            }
            catch (IOException)
            {
                // The child exited before reading its input. Its exit code and output say why.
            }

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch (InvalidOperationException)
                {
                    // Already exited between the timeout and the kill.
                }

                return new ProcResult(-1, string.Empty, $"timed out after {timeoutMs / 1000}s");
            }

            process.WaitForExit();
            return new ProcResult(process.ExitCode, stdout.Result, stderr.Result);
        }
    }
}

public sealed class Git(string repo)
{
    public string Repo { get; } = repo;

    /// <summary>Working copies found altered and put back to their commit during this run.</summary>
    public List<string> Restored { get; } = new();

    public ProcResult Try(params string[] args) =>
        Proc.Run("git", new[] { "-c", "core.longpaths=true", "-c", "core.quotepath=false" }.Concat(args), this.Repo);

    public string Run(params string[] args)
    {
        var r = this.Try(args);
        if (!r.Ok)
        {
            throw new AyaException($"git {string.Join(' ', args)} failed: {r.StdErr.Trim()}");
        }

        return r.StdOut;
    }

    /// <summary>Resolves a branch, tag or SHA to a commit SHA, also trying the <c>origin/</c> remote.</summary>
    public string Resolve(string reference)
    {
        foreach (var candidate in new[] { reference, "origin/" + reference })
        {
            var r = this.Try("rev-parse", "--verify", "--quiet", candidate + "^{commit}");
            if (r.Ok)
            {
                return r.StdOut.Trim();
            }
        }

        throw new AyaException($"cannot resolve git ref '{reference}' in {this.Repo}");
    }

    /// <summary>The merge base between the head commit and the repository's default branch.</summary>
    public string DefaultBase(string headSha)
    {
        var symbolic = this.Try("symbolic-ref", "--quiet", "refs/remotes/origin/HEAD");
        var target = symbolic.Ok ? symbolic.StdOut.Trim() : "HEAD";
        return this.Run("merge-base", target, headSha).Trim();
    }

    /// <summary>Creates a detached worktree at <paramref name="sha"/>, or reuses one that is already there.</summary>
    public string EnsureWorktree(string sha, string path)
    {
        if (Directory.Exists(path))
        {
            var copy = new Git(path);
            var head = copy.Try("rev-parse", "HEAD");
            if (head.Ok && head.StdOut.Trim() == sha)
            {
                // Being at the right commit is not enough: an interrupted run can leave an altered file or a
                // candidate test behind, and the results would then be reported against a commit they do not
                // describe. These copies belong to AYA, so they are put back to the commit before every use.
                var dirty = copy.Try("status", "--porcelain");
                if (dirty.Ok && dirty.StdOut.Trim().Length > 0)
                {
                    copy.Run("reset", "--hard", sha);
                    copy.Run("clean", "-fd");
                    this.Restored.Add(Path.GetFileName(path));
                }

                return path;
            }

            throw new AyaException($"worktree path {path} exists but is not at {sha}; remove it and retry");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        this.Run("worktree", "add", "--detach", path, sha);
        return path;
    }
}

/// <summary>Finds a <c>dotnet</c> executable that actually has an SDK and makes child processes use it.</summary>
public static class Dotnet
{
    private static string? located;

    public static string Exe => located ??= Locate();

    private static string Locate()
    {
        var exe = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("AYA_DOTNET"),
            Environment.ProcessPath is { } self && Path.GetFileName(self).Equals(exe, StringComparison.OrdinalIgnoreCase) ? self : null,
            Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root ? Path.Combine(root, exe) : null,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "dotnet", exe),
            "dotnet",
        };

        foreach (var candidate in candidates)
        {
            if (candidate is null || (Path.IsPathRooted(candidate) && !File.Exists(candidate)))
            {
                continue;
            }

            ProcResult sdks;
            try
            {
                sdks = Proc.Run(candidate, ["--list-sdks"]);
            }
            catch (AyaException)
            {
                continue;
            }

            if (!sdks.Ok || sdks.StdOut.Trim().Length == 0)
            {
                continue;
            }

            if (Path.IsPathRooted(candidate))
            {
                // MSBuildWorkspace launches its build host through PATH, so put this SDK first.
                var dir = Path.GetDirectoryName(candidate)!;
                Environment.SetEnvironmentVariable("PATH", dir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
                Environment.SetEnvironmentVariable("DOTNET_ROOT", dir);
            }

            return candidate;
        }

        throw new AyaException("no .NET SDK found. Install the .NET 8 SDK, or set AYA_DOTNET to a dotnet executable that has one.");
    }
}

/// <summary>
/// The step-by-step record of a run: what each stage received and produced. Printed as it happens and saved
/// next to the report, so a reader can follow how a conclusion was reached.
/// </summary>
public sealed class Trace
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly List<string> lines = new();
    private int step;

    public void Step(string title)
    {
        this.step++;
        this.Write(string.Empty);
        this.Write($"[{this.clock.Elapsed:mm\\:ss}] STEP {this.step}  {title}");
    }

    public void Info(string text) => this.Write("        " + text);

    /// <summary>Writes up to <paramref name="max"/> items, then how many were left out.</summary>
    public void List(IEnumerable<string> items, int max = 12)
    {
        var all = items.ToList();
        foreach (var item in all.Take(max))
        {
            this.Info(item);
        }

        if (all.Count > max)
        {
            this.Info($"… and {all.Count - max} more");
        }
    }

    public void Save(string path) => File.WriteAllLines(path, this.lines);

    private void Write(string line)
    {
        this.lines.Add(line);
        Console.Error.WriteLine(line);
    }
}

public sealed class Options
{
    private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> flags = new(StringComparer.OrdinalIgnoreCase);

    public static Options Parse(string[] args, params string[] knownFlags)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!a.StartsWith("--", StringComparison.Ordinal))
            {
                throw new AyaException($"unexpected argument '{a}'");
            }

            var key = a[2..];
            if (knownFlags.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                o.flags.Add(key);
            }
            else if (i + 1 < args.Length)
            {
                o.values[key] = args[++i];
            }
            else
            {
                throw new AyaException($"option '{a}' needs a value");
            }
        }

        return o;
    }

    public string? Get(string key) => this.values.GetValueOrDefault(key);

    public string Require(string key) => this.Get(key) ?? throw new AyaException($"missing required option --{key}");

    public bool Has(string flag) => this.flags.Contains(flag);
}

public static class Paths
{
    public static string AyaHome =>
        Environment.GetEnvironmentVariable("AYA_HOME") is { Length: > 0 } home
            ? home
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aya");

    public static string Rel(string root, string full) => Path.GetRelativePath(root, full).Replace('\\', '/');

    /// <summary>Heuristic: a project is a test project if it lives under a tests folder or is named like one.</summary>
    public static bool IsTestPath(string relPath)
    {
        var p = relPath.Replace('\\', '/');
        return p.StartsWith("tests/", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("test/", StringComparison.OrdinalIgnoreCase)
            || p.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            || p.Split('/').Any(seg => seg.EndsWith("Tests", StringComparison.Ordinal) || seg.EndsWith(".Test", StringComparison.Ordinal));
    }
}
