# AYA

Know where to look, and what to trust, in a code change written by an AI.

Built for the Tieto challenge at Junction 2026: *how can AI review code made by AI so that humans can trust it?*

## What it does

`aya review` takes a change (a branch or commit) and orders everything it touches by how much it deserves a
person's attention, with the reason for every place in the list. The named person who signs decides how far down
to go. Nothing is ever left out of the list: its last tier means *no signal was found*, not *this is safe*.

AYA is built to be trusted when it raises an alarm, and it never says that something can be skipped. An alarm has
a measurement behind it. An all-clear would only have the absence of one.

## How risk is shown

Not as one number. Four things are known with very different certainty, so they are shown apart:

| | The question | Where it comes from | How solid |
|---|---|---|---|
| Likelihood | Is it easy for this to be wrong? | Modified or new code, what the changed lines consist of, old code against new code on the same inputs, tests written to break it | Indications, plus two measurements when the deeper stages run |
| Impact | How much breaks if it is wrong? | What depends on it (compiler), how many tests fail when it is broken on purpose, how much of what depends on it no test executes | Partly measured |
| Detection | Would anything notice? | Which changed lines ran, deliberate breaks that no test caught | Measured |
| Sight | What can the analysis not see? | Deleted code, generated code, conditional compilation, declarations | Stated per item |

The order is a fixed rule, printed in every report so the owner can disagree with it:

1. **Breaks something that worked, demonstrated.** An existing test now fails, an input that gave a result now
   fails, or a test written to break it fails on the new code and passes on the old.
2. **Changes a result that existed before.**
3. **Existing code others depend on, where a mistake would go unnoticed.**
4. **Could not be measured.**
5. **A mistake here would go unnoticed** (new code, or code nothing depends on).
6. **No signal found.**

## What it runs

Always:

1. Classifies every changed file by role and groups files that use the same edit, split into the variants that
   are *exactly* the same edit.
2. Builds a dependency graph from the C# compiler's semantic model (Roslyn).
3. Runs the tests on the old code and on the new code, compares them test by test, and records which changed
   lines executed.
4. Runs the tests that came with the change against the old code. A test that passes without the change proves
   nothing about it.

On request, all off by default:

| Option | What it measures |
|---|---|
| `--mutate` | Alters each changed line in turn and records whether a test that passed now fails. A line that ran is not a line that is checked. |
| `--compare` | A model writes a probe that records what a modified unit does on a set of inputs. The probe runs twice on the old code, to discard what is unstable, and once on the new. Inputs whose result differs are reported. This does not depend on how good the existing tests are. |
| `--write-tests` | A model writes tests for unexecuted lines and unnoticed breaks. One is accepted only if it compiles, passes and is measured to reach its target. |
| `--attack` | A model writes tests to break the units at the top of the list. One that fails on the new code is run on the old code, which decides whether the change caused it. |
| `--exhaustive` | Probes every older member that depends on the change and that no original test executes, within a session budget, and counts how many change their output when the change is broken on purpose. |

The rule for everything a model contributes: **a model may lower trust on its opinion, and raise it only through
something that was measured.** A probe or a test that reaches private members by force, or builds objects the
program cannot build, is refused.

## What it found

On `ju26-issue-2283` of `te-industry-tech-office/junction26-messagepack-csharp`, an agent-written fix (4 files,
228 changed lines) whose 3 new tests all fail on the old code and whose 10 changed executable lines all ran:

- **Three deliberate breaks of the one new condition go unnoticed** (8 of 11 breaks were caught). The condition
  ran in every test and none of them checks what it decides.
- **Old and new code differ on 4 of 25 inputs.** Two are the fix itself: a type from another assembly used to
  crash the generator and now generates. The other two are a case the requirement never mentions: a positional
  record parameter with a null key used to crash the analyzer and now produces no error at all, where an ordinary
  property still gets one. No test of the change, and no deliberate break, shows this.

Both findings are in units whose every changed line was executed by passing tests.

The other three branches (`ju26-issue-2234`, `-2297`, `-2129`) were analysed before the ordering and the
comparison existed and have not been re-run yet. Their earlier headline figures are withdrawn: one of them (221
files "sharing one 3-line edit") was wrong, since the files turned out to be four different variants of that edit.

## Running it

Requires git and the .NET 8 SDK. The challenge repository also needs the .NET 9 SDK: its source generator is
built against a newer compiler than the SDK it pins. AYA detects this and builds old and new code with the same,
newer one. The deeper stages that use a model need the `claude` command line.

```
dotnet build src/Aya/Aya.csproj
dotnet src/Aya/bin/Debug/net8.0/aya.dll review \
    --repo https://github.com/te-industry-tech-office/junction26-messagepack-csharp \
    --head ju26-issue-2283 --req "MessagePack-CSharp#2283" \
    --mutate --compare --attack
dotnet test tests/Aya.Tests/Aya.Tests.csproj
```

Output goes to `aya-out/<head>/`: `report.md`, `evidence.json`, `graph.json`, `pipeline.log`, and what the models
wrote under `probes/`, `proposed-tests/` and `attacks/`. `dotnet aya.dll --help` lists every option and its
default budget. Clones and working copies are kept under `~/.aya` (override with `AYA_HOME`).

A plain run takes four to five minutes and executes about 1,200 tests on both versions. Each deeper stage adds
minutes; the ones that use a model spend most of their time waiting for it.

## What it does not do

- It does not judge whether the change fulfils its requirement, and says so in the report.
- A line that ran under a passing test is not proven correct. An accepted test is not proven worth keeping.
- Old and new code are compared only on the inputs a probe chose. No difference on them is not the same behaviour.
- The order ranks where a mistake would matter and go unseen. It is not a probability that a mistake exists.
- Deleted code is listed as not analysed; what used it is not looked for.
- Calls made through reflection, generated code or compiler-loaded analyzers are not edges in the graph.
- It never approves. The report ends with a sign-off block for a named owner.

What is planned, including ideas taken from two other tools, is in [TODO.md](TODO.md).
