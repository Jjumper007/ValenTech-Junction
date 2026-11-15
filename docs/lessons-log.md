# AYA: lessons log

What went wrong while building it, what that taught us, and what we decided. Every entry happened; the figures
are from runs on the challenge branches.

## Failure modes we hit

**1. A line that ran is not a line that is checked.**
On `ju26-issue-2283` every changed line was executed by passing tests, and all three new tests failed without the
change. It looked verified. Breaking the one new condition on purpose, three different ways, made no test fail.
*Decision:* coverage is reported as "executed", never as "verified", and deliberate breaks are part of the evidence.

**2. The tool was wrong whenever it said something could be skipped.**
The first version reported "14 of 228 lines left to read". An outside audit showed that figure was wrong on two of
four branches, always too low: 221 files "sharing one edit" were four different variants of it, and 40 changed
lines of declarations were counted as nothing to read.
*Decision:* the reading plan was removed. AYA orders; it does not exclude. Nothing leaves the list.

**3. Our own labels claimed more than was measured.**
The same audit found ten such defects, all confirmed in the code: skipped tests counted as passing, a PASS given
because nothing had been shown to fail, a hung test run counted as a caught defect. It happened again later: a
sweep whose inputs drove 58 of 276 changed lines was labelled PASS because none of them differed.
*Decision:* each one became a test of AYA itself (74 today). "Pass" is said only when the check reached most of
what it is about. A tool that asks for trust has to be tested like the code it reviews.

**4. A model gamed the check.**
Asked for a test that catches a break which changes nothing a caller can observe, a model built an object that
cannot exist, through reflection, to force a difference. The test compiled, passed, and caught the break.
*Decision:* tests that reach private members or build objects by force are refused. "Accepted" is stated to mean
"compiles, passes and reaches its target", not "worth keeping".

**5. Most tests written to break the code were false alarms.**
Of nine attack tests on one branch, seven failed on the new code and on the old code alike.
*Decision:* a failing test written by a model counts for nothing until it is run on the old code. Only one that
fails on the new and passes on the old is reported as a demonstrated change.

**6. Comparing old and new measured the feature, not regressions.**
On a branch that adds a feature, probes fed the new feature to both versions and 28 of 60 inputs were reported as
"changed behaviour".
*Decision:* a probe may only use inputs the old code already handled. The same branch then showed 41 of 45 inputs
unchanged.

**7. A swarm of agents was slower than one agent at a time.**
A coordinated team of agents took eight and a half minutes and delivered two of four files. Independent sessions
launched at the same time, each given only its own unit, delivered a file each in about forty seconds.
*Decision:* the coordinator is not a model. The dependency graph does the split and AYA does the checking; models
only write, in parallel, each inside its own module.

**8. The best finding depended on what a model happened to write.**
The one demonstrated regression on `ju26-issue-2297` (user code that already has the attribute stops compiling)
was found because a model thought of that input. The next day the same stage, on two other branches, did not find
what it had found the day before.
*Decision:* two things. A test that showed a regression is kept and run again on every later review. And inputs
are also built by rule, without a model: the sweep (entry 13).

**9. A change that alters every output buries the one difference that matters.**
On `ju26-issue-2297`, 30 of 36 compared inputs differed, all looking alike, with the regression among them.
*Decision:* a difference made only of lines the change itself adds or removes is labelled as the change showing
through, without a model, and goes last. In the last pass 1,811 of 1,860 differences were set aside this way.

**10. The judge sent noise to the top.**
We let a model say whether each remaining difference was meant. It called a build-stamped version number and a
compiler-generated name "not meant", correctly, and that moved them up the list.
*Decision:* such noise is removed before comparing, and the judge has a fourth answer, "artefact", that moves
nothing.

**11. The judge believed the change's description of itself.**
On `ju26-issue-2283` the change stops reporting errors for types of referenced projects. The judge read that as
meant, quoting the documentation the agent had added. A reviewer showed the filter is needed by one test only,
which the agent also wrote.
*Decision:* the owner supplies the text of the requirement, and it weighs more than what the change says about
itself. A reading of "meant" must quote the stated purpose word for word or it is discarded (it was, once). The
readings still vary: the same requirement was read as contradicted in one run and as fully shown in the next.
They are shown as opinions and can only move an item up.

**12. No test needed part of the change, and nothing said so.**
Line coverage and deliberate breaks both passed over it.
*Decision:* the change is undone one part at a time and the tests are run. On `ju26-issue-2283` the new filter
turned out to be needed by exactly one test, which came with the change; on `ju26-issue-2234` four lines could be
put back with every test passing.

**13. We fitted the sweep to defects we already knew.**
The sweep takes the inputs the tests hold and alters them by rule. Its first alterations were chosen after reading
what a manual review had found, so that the sweep would find the same. It did: on `ju26-issue-2234` it went from
no difference in 300 inputs to 49 in 750. That shows nothing about defects nobody has seen.
*Decision:* the hand-picked alterations were replaced by a family of 34 built from a few schemes over C# syntax,
with the budget shared evenly. It found the same things and added no noise. It has not been tried on a change
nobody reviewed, and we say so wherever the sweep is described.

**14. The old code "passes" a test about a feature it does not have.**
On `ju26-issue-2129` a test about the new attribute passed on the old code, where the attribute does not exist
and nothing is generated, and failed on the new. AYA ranked it first as a regression. It is a real defect of the
new feature, and it is not a regression.
*Decision:* a pass on the old code is taken back when the test uses something the change introduced. The test is
then reported as failing with no arbiter, for a person to read. The branch was reviewed again with that rule.

**15. The model ran out mid-run, and the report named the wrong author.**
The quota of the writing model ended during a run of four branches. AYA fell back to another model, as designed,
and the report still said the first one had written everything.
*Decision:* the report says how many sessions each model answered and why. A spent quota is told apart from a
slow answer.

**16. Three of our own defects would have produced false statements.**
During one evening of fixing and re-running we stopped three runs: a part of the change no test runs was about to
be reported wrongly, a file of inputs deleted itself, and two alterations cut past the end of a nested type.
*Decision:* each became a test. The loop of fixing and re-running is worth its cost only because every run is
read before the next change.

## What the sweep still cannot find

- A defect that needs two alterations at once. On `ju26-issue-2283` the dropped errors appear only when a type is
  in a referenced library and is also defective. Alterations are applied one at a time.
- Defects of a new feature whose tests do not hold their inputs as plain text (`ju26-issue-2129`).
- A configuration in which the change has no effect: the sweep compares the text written, not what it does.
- Two false alarms we know of: inputs that are incomplete because the test builds the rest elsewhere, and old
  limitations of the tool under test shown as findings because only the new code was run on those inputs.

## AYA against a review by reading and running

Four independent reviewers, one per branch, read the issue and the change and ran their own tests. None was shown
AYA's output. The comparison, finding by finding, is in [`results/contrast.md`](../results/contrast.md).
*Decision:* AYA is not a second reviewer. It is what a reviewer's claim has to pass to be believed: the reviewers'
own tests were given to AYA's arbiter, and what the old code confirmed is kept and run on every later review.

## Decisions that shaped the design

- Risk is four separate things, known with different certainty, and is never shown as one number.
- The order is a fixed rule, printed in every report, so the owner can disagree with it.
- A model may lower trust on its opinion and raise it only through something measured.
- What a model writes is run; what a model believes is shown as a belief, with the words it rests on checked.
- AYA never approves. A named person decides how far down the list to go and signs for the rest.
