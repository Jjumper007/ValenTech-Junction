using Aya;

const string Help = """
    aya — where to look, and what to trust, in an AI-written code change

    Usage:
      aya review --repo <path-or-url> --head <ref> [--base <ref>] [--req <id>] [--req-file <path>] [--out <dir>]
                 [--sdk auto|repo|latest] [--skip-tests]
                 [--mutate] [--compare] [--sweep] [--write-tests] [--attack] [--exhaustive] [--judge]
                 [--max-mutants <n>] [--max-probes <n>] [--max-inputs <n>] [--max-tests <n>] [--max-attacks <n>]
                 [--max-sessions <n>] [--max-reverts <n>]
                 [--writer gemini|claude] [--writer-model <id>] [--writer-effort <level>] [--attack-from <dir>]

    review
      Compares <head> with <base> (default: its merge base with the default branch) and writes report.md,
      evidence.json and graph.json. The report orders everything the change touches by how much it deserves
      a person's attention before the change is approved, and says why. Nothing is left out of that list.

      --repo        Local clone, or a URL to clone under ~/.aya/repos
      --head        Branch, tag or commit holding the change
      --base        Commit to compare against
      --req         Requirement or issue the change claims to implement
      --req-file    A file with the text of that requirement. AYA does not fetch it; the judge reads it
      --out         Output folder (default: aya-out/<head>)
      --sdk         Which .NET SDK builds the code. repo: the one the repository selects. latest: the newest
                    installed. auto (default): repo, switching to latest if a source generator needs a newer
                    compiler. Old and new code are always built with the same one.
      --skip-tests  Build the graph and map the change without running any test

    Deeper stages, all off by default. All but the first use a model: one session per unit, all at the same
    time, each knowing only the text of its own request. Nothing a model writes is believed without being
    built and run here, and what a model only believes is shown as an opinion.
      --mutate      Alter each changed line in turn and record whether a test fails (default: 20 breaks).
                    Then undo the change one part at a time, putting the old lines back, and record whether
                    any test needs that part (default: 8 parts)
      --compare     Have a probe written for each modified unit, run it on the old and the new code, and
                    report the inputs whose result differs (default: 4 probes)
      --sweep       Run inputs nobody chose through the old and the new code: the inputs the existing tests
                    already hold, as they are and altered mechanically, some alterations taken from what the
                    change adds. Inputs that came with the change are run on the new code alone and held
                    to what must hold for any input. A model writes only the harness (default: 240 inputs)
      --write-tests Have tests written for unexecuted lines and unnoticed breaks, accepting only those that
                    compile, pass and are measured to reach their target (default: 4 requests)
      --attack      Have tests written to break every changed unit. All the files are built and run together;
                    a test that fails on the new code is run on the old code, which decides whether the
                    change caused it (default: every changed unit)
      --exhaustive  Probe every older member that depends on the change and that no original test executes,
                    and count how many change their output when the change is broken on purpose
                    (default budget: 10 sessions; the run prints how many members there are first)
      --judge       For each difference between old and new code that the change's own text does not explain,
                    ask a model whether it was meant, was not, or is an artefact of the build. Its reading
                    is shown as an opinion; "intended" must quote the change's stated purpose or it is
                    discarded; nothing moves down the list. With --req-file it also says, for each thing
                    the requirement asks for, whether a test that fails without the change shows it

      --writer      Who writes: gemini (default; gemini-4-hydrogen-medium through the Antigravity command line,
                    agy) or claude (claude-opus-5-5 at medium effort through the claude command line)
      --writer-model  Another model of the same provider
      --attack-from <dir>  Skip the writing: take attack tests someone left as A1.cs, A2.cs... and arbitrate them

    Exit code: 0 no failing evidence, 1 failing evidence, 2 usage or environment error.
    """;

// The reports are compared and hashed, so a number or a date must read the same on every machine.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

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
            return await ReviewCommand.RunAsync(Options.Parse(args[1..], "skip-tests", "mutate", "write-tests", "compare", "sweep", "attack", "exhaustive", "judge"));
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
