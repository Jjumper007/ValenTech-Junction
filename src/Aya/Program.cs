using Aya;

const string Help = """
    aya — where to look, and what to trust, in an AI-written code change

    Usage:
      aya review --repo <path-or-url> --head <ref> [--base <ref>] [--req <id>] [--out <dir>]
                 [--sdk auto|repo|latest] [--skip-tests]
                 [--mutate] [--compare] [--write-tests] [--attack] [--exhaustive]
                 [--attack-brief] [--attack-from <dir>]
                 [--max-mutants <n>] [--max-probes <n>] [--max-tests <n>] [--max-attacks <n>] [--max-sessions <n>]
                 [--writer-model <id>] [--writer-effort <level>]

    review
      Compares <head> with <base> (default: its merge base with the default branch) and writes report.md,
      evidence.json and graph.json. The report orders everything the change touches by how much it deserves
      a person's attention, and says why. Nothing is left out of that list.

      --repo        Local clone, or a URL to clone under ~/.aya/repos
      --head        Branch, tag or commit holding the change
      --base        Commit to compare against
      --req         Requirement or issue the change claims to implement
      --out         Output folder (default: aya-out/<head>)
      --sdk         Which .NET SDK builds the code. repo: the one the repository selects. latest: the newest
                    installed. auto (default): repo, switching to latest if a source generator needs a newer
                    compiler. Old and new code are always built with the same one.
      --skip-tests  Build the graph and map the change without running any test

    Deeper stages, all off by default. The last four open model sessions (the claude command line,
    default claude-opus-5-5 at medium effort); nothing a model writes is believed without being run.
      --mutate      Alter each changed line in turn and record whether a test fails (default: 20 breaks)
      --compare     Have a probe written for each modified unit, run it on the old and the new code, and
                    report the inputs whose result differs (default: 4 probes)
      --write-tests Have tests written for unexecuted lines and unnoticed breaks, accepting only those that
                    compile, pass and are measured to reach their target (default: 4 requests)
      --attack      Have tests written to break the units at the top of the list. One that fails on the new
                    code is run on the old code, which decides whether the change caused it (default: 3 units)
      --attack-brief  Instead of asking a model, write one request per unit and a brief for a swarm of
                    agents that work in parallel in a copy of the code kept for them
      --attack-from <dir>  Collect the test files such a swarm (or a person) left, named A1.cs, A2.cs..., and
                    arbitrate them all in one build and run on the new code and one on the old
      --exhaustive  Probe every older member that depends on the change and that no original test executes,
                    and count how many change their output when the change is broken on purpose
                    (default budget: 10 sessions; the run prints how many members there are first)

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
            return await ReviewCommand.RunAsync(Options.Parse(args[1..], "skip-tests", "mutate", "write-tests", "compare", "attack", "attack-brief", "exhaustive"));
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
