using Aya;

const string Help = """
    aya — evidence for reviewing AI-written code changes

    Usage:
      aya review --repo <path-or-url> --head <ref> [--base <ref>] [--req <id>] [--out <dir>]
                 [--sdk auto|repo|latest] [--skip-tests]
                 [--mutate] [--max-mutants <n>]
                 [--write-tests] [--max-tests <n>] [--writer-model <id>] [--writer-effort <level>]

    review
      Compares <head> with <base> (default: its merge base with the default branch) and writes
      evidence.json, graph.json and report.md.

      --repo        Local clone, or a URL to clone under ~/.aya/repos
      --head        Branch, tag or commit holding the change
      --base        Commit to compare against
      --req         Requirement or issue the change claims to implement
      --out         Output folder (default: aya-out/<head>)
      --sdk         Which .NET SDK builds the code. repo: the one the repository selects. latest: the newest
                    installed. auto (default): repo, switching to latest if a source generator needs a newer
                    compiler. Old and new code are always built with the same one.
      --skip-tests  Build the graph and map the change without running any test
      --mutate      Alter each changed line in turn and record whether a test fails (default budget: 20 breaks)
      --write-tests Ask a model for tests covering unexecuted lines and unnoticed breaks, and accept only those
                    that compile, pass and are measured to do so. Uses the claude command line
                    (default: claude-opus-5-5 at medium effort, 4 requests, 3 attempts each)

    Exit code: 0 no failing evidence, 1 failing evidence, 2 usage or environment error.
    """;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(Help);
    return 0;
}

try
{
    switch (args[0])
    {
        case "review":
            return await ReviewCommand.RunAsync(Options.Parse(args[1..], "skip-tests", "mutate", "write-tests"));
        default:
            Console.Error.WriteLine($"aya: unknown command '{args[0]}'");
            Console.Error.WriteLine(Help);
            return 2;
    }
}
catch (AyaException ex)
{
    Console.Error.WriteLine("aya: " + ex.Message);
    return 2;
}
