# AYA

Evidence for reviewing AI-written code changes, so a named person can accept a change without reading every line.

Built for the Tieto challenge at Junction 2026: *how can AI review code made by AI so that humans can trust it?*

## What it does

`aya review` takes a change (a branch or commit) and produces a one-page report that says what was checked, by which tool, what was not checked, and which lines a person still has to read. Nothing in the report is a model's opinion; every statement comes from a compiler, a test run or a diff.

For one change it:

1. Classifies every changed file by role (source, test, expected-output file, template, generated code, docs) and groups files that received exactly the same line edits.
2. Builds a dependency graph of the code from the C# compiler's semantic model (Roslyn), collapses cycles, and assigns each symbol a layer.
3. Maps the changed lines to the symbols that own them and finds everything that depends on those symbols.
4. Runs the test suite on the old code and on the new code and compares them test by test.
5. Runs the tests that came with the change against the *old* code. A test that passes without the change proves nothing about it.
6. Records which changed lines the tests actually executed.
7. Writes `report.md`, `evidence.json` and `graph.json`. The report quotes the hash of the evidence it was derived from and ends with a sign-off block for the owner.

## Results on the challenge branches

Run against the four agent-written branches of `te-industry-tech-office/junction26-messagepack-csharp`:

| Branch | Files | Changed lines | Lines left for a human | Notable finding |
|---|---:|---:|---:|---|
| `ju26-issue-2283` | 4 | 228 | 14 | All 3 new tests fail on the old code and all 10 changed lines ran, yet one deliberate break of the new condition goes unnoticed |
| `ju26-issue-2234` | 4 | 266 | 108 | 6 of the 10 new tests pass on the old code; 5 changed lines never ran |
| `ju26-issue-2297` | 237 | 1392 | 31 | 221 expected-output files share one 3-line edit |
| `ju26-issue-2129` | 28 | 1043 | 349 | 65 changed executable lines never ran; 4 of 16 deliberate breaks go unnoticed |

These figures are from runs with `--mutate --write-tests`. A plain run takes four to five minutes on a laptop and executes roughly 1,200 tests on both versions; mutation and written tests add five to ten minutes.

## Running it

Requires git and the .NET 8 SDK. The challenge repository also needs the .NET 9 SDK installed: its source
generator is built against a newer compiler than the SDK it pins, so with .NET 8 alone three of its test
projects do not compile. AYA detects this and switches to the newest installed SDK for both versions of the code.

```
dotnet build src/Aya/Aya.csproj
dotnet src/Aya/bin/Debug/net8.0/aya.dll review \
    --repo https://github.com/te-industry-tech-office/junction26-messagepack-csharp \
    --head ju26-issue-2283 \
    --req "MessagePack-CSharp#2283"
```

Output goes to `aya-out/<head>/`. Clones and worktrees are kept under `~/.aya` (override with `AYA_HOME`).

| Option | Meaning |
|---|---|
| `--repo` | Local clone, or a URL to clone |
| `--head` | Branch, tag or commit holding the change |
| `--base` | Commit to compare against (default: merge base with the default branch) |
| `--req` | Requirement or issue the change claims to implement |
| `--out` | Output folder |
| `--sdk` | `repo`: build with the SDK the repository selects. `latest`: the newest installed. `auto` (default): `repo`, switching to `latest` when a source generator needs a newer compiler |
| `--skip-tests` | Build the graph and map the change without running tests |
| `--mutate` | Alter each changed line in turn and record whether any test fails (`--max-mutants`, default 20) |
| `--write-tests` | Ask a model for tests that cover unexecuted lines and unnoticed breaks, and accept only those that compile, pass and are measured to reach their target (`--max-tests`, default 4) |

### Going deeper: `--mutate` and `--write-tests`

Coverage says a changed line ran. It does not say anything checked what the line did. `--mutate` asks that
question directly: it alters each changed line (flips a comparison, negates a condition, removes a statement),
re-runs only the test projects that execute that line, and records whether a test that passed before now fails.
A break no test notices is listed in the report as not checked.

`--write-tests` then asks a model to fill the gaps: one request per stretch of unexecuted code and per unnoticed
break. Nothing the model writes is taken on trust. Each test is added to a scratch copy of the code and must
compile, pass, and be measured to execute the target lines (or to fail once the line is altered). A rejected test
is sent back with the compiler or test output, up to three times. Accepted tests are saved under
`proposed-tests/` as proposals; they are not added to the change, and the report says a model chose what they assert.

The writer is the `claude` command line run as a fresh session with no tools (`--writer-model`, default
`claude-opus-5-5`; `--writer-effort`, default `medium`).

## What it does not do

- It does not judge whether the change fulfils its requirement. It says so in the report.
- A line that ran under a passing test is not proven correct.
- Calls made through reflection, generated code or compiler-loaded analyzers are not edges in the graph.
- A test project that does not compile on either version is reported as not built, and its tests as giving no evidence.
- When it builds with a newer SDK than the repository pins, the report says so. Old and new code are always built with the same one.

Work deferred until after the prototype is listed in [TODO.md](TODO.md).
