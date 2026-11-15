# AYA against a review by reading and running

Four independent reviewers, one per branch, were asked to review each change the way a careful engineer would:
read the upstream issue, read the whole change, and settle every suspicion by running code on the old and on the
new version. They were language-model agents with a shell. **None was shown anything AYA had produced.** Their
findings are in [`reviews/`](reviews/), the tests they wrote in [`reviews/tests/`](reviews/tests/).

This page sets each of their findings against AYA's last pass on the same branch (the reports in this folder),
and then gives the reviewers' own tests to AYA's arbiter.

Two things to keep in mind while reading it:

- **AYA was changed after the reviews came back.** Three checks were added to catch what the reviewers had
  found: undoing the change part by part, reading the requirement, and the sweep's alterations. The column
  *Since* says whether a finding is shown by a check that already existed (**before**) or by one added afterwards
  (**after**). Only the first kind says anything about findings nobody has seen yet.
- **"Shown" means it is in the report where the owner would read it.** It does not mean AYA explains the defect
  the way the reviewer does.

## The count

| | Findings | Shown, measured | Shown, as a model's opinion | A signal pointing at the place | Not shown |
|---|---:|---:|---:|---:|---:|
| `ju26-issue-2297` | 4 | 1 | 1 | 1 | 1 |
| `ju26-issue-2283` | 7 | 4 | 0 | 0 | 3 |
| `ju26-issue-2234` | 6 | 4 | 0 | 1 | 1 |
| `ju26-issue-2129` | 12 | 2 | 1 | 2 | 7 |
| **All** | **29** | **11** | **2** | **4** | **12** |

Of the 11 shown as something measured, 7 rest on checks that existed before the reviews were read and 4 on
checks added after.

## `ju26-issue-2297`: generated code marked as excluded from coverage

| # | The reviewer found | In AYA's report | By what | Since |
|---|---|---|---|---|
| 1 | A user's resolver that already has the attribute stops compiling (`CS0579`) | **Measured.** Three of the first five places of the list. The other two of those five rest on model-written tests whose failure is about the `#if` guard (finding 3), which is an opinion about the change and not a break | Three inputs of the sweep that worked and no longer do; two attack tests of this pass; eight kept tests | before (attack tests). The sweep's rule came after |
| 2 | The attribute lands on the user's own class, so the user's code leaves the coverage report | **A signal.** 31 inputs where the user's type gains an attribute the user did not write, listed as a difference the change's text does not explain. AYA does not say what follows from it, and the judge read it as meant | Sweep, the line that describes the user's types | after |
| 3 | The `#if` guard rests on a false premise: on netstandard2.0 and .NET Framework the change does nothing | **Opinion.** "All generated code" read as contradicted by the guard | Reading of the requirement | after |
| 4 | The new tests do not show what their names claim | **Not shown.** AYA reports the opposite signals: both new tests fail on the old code, every changed line runs, every deliberate break is caught | | |

## `ju26-issue-2283`: crash on generic types of a referenced assembly

| # | The reviewer found | In AYA's report | By what | Since |
|---|---|---|---|---|
| 1 | Repeated or mixed keys in a referenced type: the error that stopped the build is gone | **Measured**, as one item with 2 and 3. First place of the list | A test kept from the first review: a defective type of a referenced project no longer gets its diagnostic. The fresh attack tests of this pass did not find it, and neither did the sweep | before |
| 2 | An unkeyed member of a referenced type: no error, and the member is silently not serialized | **Measured**, same item. The lost data is not shown, only the lost error | same | before |
| 3 | Explanatory errors dropped while the generated code still fails to compile | **Measured**, same item | same | before |
| 4 | The filter is wider than the requirement: without it 169 of 170 tests pass | **Measured.** The filter's line is needed by exactly one test, which came with the change; three deliberate breaks of its condition go unnoticed | The change undone part by part; deliberate breaks | after (undone parts), before (breaks) |
| 5 | A positional record with a null key: no crash now, and the right error is silently dropped | **Not shown in this pass.** Probes written by a model found it in three earlier passes | | |
| 6 | The documentation the change adds says things the code does not do | **Not shown.** AYA lists documentation as unchecked | | |
| 7 | Weaknesses of the new tests (a type parameter never used, only names asserted) | **Not shown** | | |

## `ju26-issue-2234`: the analyzer and `SuppressSourceGeneration`

| # | The reviewer found | In AYA's report | By what | Since |
|---|---|---|---|---|
| 1 | The change does not fix what the issue reports; the reported bug is not in this code base | **A signal.** 6 of the 10 new tests pass without the change. The requirement is read as contradicted, because the change alters types that do not use the option. The same reading wrongly calls the requirement demonstrated, naming a test that fails on the old code for another reason | The change's tests on the old code; reading of the requirement | before; after |
| 2 | With a repeated or mixed key the generator now emits a formatter that drops a member, and a misleading error with code that does not compile | **Measured.** 38 inputs that crashed now give a result; one now gets `MsgPack007` and generated code with `CS7036`; ten give another result once an attribute argument is made null, one of them with a formatter that is no longer generated | Sweep, attribute arguments repeated, changed of kind or made null | after |
| 3 | A diagnostic is lost (`MsgPack003`) when a text key comes before an integer one | **Measured.** First place of the list | An attack test of this pass and three kept ones | before |
| 4 | `[Key(null)]` on a field still crashes: the fix guarded properties and not fields | **Measured**, as a note and not as a place in the list: one input crashes on the old and on the new code alike | Sweep, an attribute argument made null | after |
| 5 | Half the new tests show nothing, and the field half of the change is untested | **Measured.** 6 of 10 pass on the old code; three parts of the change, lines 1148, 1163–1166 and 1179–1182, are needed by no test | The change's tests on the old code; the change undone part by part | before; after |
| 6 | The README overstates what is validated | **Not shown** | | |

## `ju26-issue-2129`: formatters for types of another assembly (new feature)

| # | The reviewer found | In AYA's report | By what | Since |
|---|---|---|---|---|
| 1 | With default options the generated formatter is not found | **Not shown** | | |
| 2 | Attributes on the data type are ignored: members marked "ignore" are serialized | **Opinion.** "Work with user-defined versions of the attributes" read as not addressed | Reading of the requirement | after |
| 3 | Members without a setter are dropped with no diagnostic | **A signal.** The method that decides it (`IsPublicReadWrite`) has four deliberate breaks that no test notices and two lines that never run | Deliberate breaks; coverage | before |
| 4 | A surrogate error in a referenced project crashes the whole generator | **Not shown** | | |
| 5 | An explicit callback implementation gives code that does not compile | **A signal.** The lines are among those listed as never executed | Coverage | before |
| 6 | The surrogate honours only two attributes; a surrogate type that cannot be used is silently ignored | **Measured, for the last case.** A test kept from an earlier pass shows the silent fallback. It is reported as failing on the new code with no arbiter: the old code passes it only because the feature is not there, and AYA takes that pass back. (It had ranked the same test first as a regression, which is how that rule came about.) | Kept attack test | before |
| 7 | Map mode is ignored for integer-keyed surrogates | **Not shown** | | |
| 8 | Six shapes of formatter are accepted and then fail in generated code with no diagnostic | **Not shown.** The sweep had nine inputs for the new feature | | |
| 9 | Member types are not collected, so the resolver is incomplete | **Not shown** | | |
| 10 | The documented usage raises a warning in projects with nullable enabled | **Not shown** | | |
| 11 | The tests cover the diagnostics and not the generated code | **Measured.** 7 of 13 new tests cannot be shown to fail without the change; 65 of 276 changed lines never run; 19 deliberate breaks go unnoticed; none of the five things the requirement asks for is shown by a test | Tests on the old code; coverage; deliberate breaks | before |
| 12 | Minor points (an unneeded lookup, a project missing from the solution) | **Not shown** | | |

The reviewer's verdict on existing behaviour, "no regression", AYA measured too: the same 1,274 tests pass on
both versions and 2,542 inputs of the sweep give the same result.

## The reviewers' tests in front of the arbiter

Each reviewer was then asked to turn every finding reproduced by running code into a test: one that states what a
user should get, so that it fails on the new code while the defect is there. AYA built and ran those files on the
new code and on the old (`aya review --attack-from`), exactly as it does with tests a model writes for it.

| | Tests | Fail on the new code, pass on the old | Fail on both | Fail on the new code, no old code to arbitrate | Pass on both |
|---|---:|---:|---:|---:|---:|
| `ju26-issue-2297` | 6 | 4 | 2 | 0 | 0 |
| `ju26-issue-2283` | 12 | 8 | 4 | 0 | 0 |
| `ju26-issue-2234` | 7 | 4 | 2 | 0 | 1 |
| `ju26-issue-2129` | 26 | 0 | 0 | 23 | 3 |
| **All** | **51** | **16** | **8** | **23** | **4** |

What the arbiter's answer means for each kind:

- **Fails on the new code and passes on the old (16).** The change caused it. These are the reviewers' findings
  that no longer rest on the reviewer's word: `CS0579` and the attribute on the user's class (2297), the eight
  lost diagnostics (2283), the dropped member, the misleading error and the lost `MsgPack003` (2234). AYA kept
  each file; every later review of the branch runs it again.
- **Fails on both (8).** True of the code, and not caused by the change: `[Key(null)]` on a field (2234), three
  shapes the README says are supported and are not (2283). Two of these are gaps against the requirement and not
  old defects: on netstandard2.0 the old code had no attribute and the new code switches it off (2297). The
  arbiter cannot tell a gap from an old defect, and its answer for those two is misleading.
- **Fails on the new code with nothing to arbitrate (23).** Defects of the new feature (2129). The file was made
  not to compile on the old code on purpose, so that AYA would not read "the feature does not exist there" as
  "not caused by the change". AYA reports them as failing tests for a person to read. This is the largest group,
  and it is where the old code as arbiter has nothing to give.
- **Pass on both (4).** Written to pass: three pin the absence of a regression (2129), one shows that the symptom
  the issue reports is absent on the old code too (2234).

On every one of the 51 tests the arbiter's result matched what the reviewer had seen when running it.

## What AYA showed that no reviewer reported

- On three branches where nobody was looking for it, about forty inputs of the sweep make the generator crash on
  the old and on the new code alike (an attribute argument repeated). It is the crash that `ju26-issue-2234`
  fixes, found on the other three branches.
- How much of each change the evidence reaches: 14 of 14, 10 of 10, 18 of 18 and 58 of 276 changed lines driven
  by the sweep; which deliberate breaks no test notices; how many members depend on each changed unit.
- Fifteen sets of model-written tests that looked like findings and failed on the old code too.

None of these is a defect of a change that a reviewer missed.

## What this shows, and what it does not

The reviewers found more than AYA: 29 findings against 11 shown as measured. On the changes to existing behaviour
(2283, 2234) AYA shows most of what matters; on the new feature (2129) it shows little beyond "this is thinly
tested".

What AYA adds is not more findings. It is what a person can do with a finding:

- A reviewer's claim becomes a test that the old code confirms or does not, kept for every later review.
- Silence has a size: "2,542 inputs give the same result and they run every changed line" can be weighed, and a
  reviewer who says nothing about a file cannot.
- What was not checked is listed, and the order is a rule the owner can read and disagree with.

What it does not show: that AYA would find, on a change nobody has reviewed, what a reviewer would find. Four of
the eleven measured findings are shown by checks written after reading the reviews, and the reviewers were run
once, so nothing here says how much two reviews of the same change would agree with each other.
