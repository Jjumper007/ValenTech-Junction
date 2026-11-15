# AYA: concept card

*Tieto challenge, Junction 2026: how can AI review code made by AI so that humans can trust it?*

## The problem

An agent writes a thousand lines in minutes. A person still has to approve them, and reading every line with the
same attention does not scale. The usual answer is a second model that reads the change and gives a verdict. A
verdict about AI-written code, from an AI, asks to be believed. That is not something a person can sign under.

## The idea

**Tell the person where to look, with a measurement behind every reason, and never tell them what they can skip.**

AYA orders everything a change touches by how much it deserves attention before approval. It is built to be
trusted when it raises an alarm. It does not issue all-clears: the last place in the list means "no signal was
found", not "safe". And it does not replace the model that reviews: it is what that model's claims have to pass.

## How it earns trust

1. **The old code is the arbiter.** Every check runs on the code before the change and on the code after it. A
   test that fails on the new code counts only if it passes on the old.
2. **Measured first, without a model.** The existing tests on both versions. Each changed line broken on
   purpose. The change undone one part at a time, to see whether any test needs that part. Thousands of inputs
   built by rule from the ones the tests already hold, run through both versions.
3. **What a model writes is run, not believed.** Models write probes and tests, one session per unit, each
   knowing only its own unit. AYA builds and runs every file. A test that showed a regression is kept and run
   again on every later review, so a finding does not depend on a model writing it twice.
4. **What a model believes is shown as a belief.** A judge says whether a difference was meant, and reads the
   requirement against the tests. The words it quotes must be in the requirement, and a test it names must be one
   that fails on the old code. Its reading can move an item up the list, never down.
5. **Risk is four things, not one score:** how likely a unit is to be wrong, how much breaks if it is, whether
   anything would notice, and what the analysis cannot see. The order is a fixed rule printed in every report.
6. **A named person signs.** AYA never approves. The report ends with a sign-off block: how far down the list
   the owner looked, and what residual risk they accept.

## What it found, refused to decide, and did not check

Four agent-written changes, all of which passed every existing test. Reports in [`results/`](../results/).

- **Found, measured.** A change that stops users' projects compiling when they already have the attribute it adds
  (three inputs out of 2,995, and tests kept from two earlier reviews). A lost diagnostic. A new filter that
  exactly one test needs, and that test came with the change. Four lines that can be put back with every test
  passing. A crash that the fix left in the block it edited.
- **Refused to decide.** Whether a difference was meant. The judge gives a reading; on one change it read 11
  groups of differences as meant and 3 as not, and the report shows both as opinions next to what was measured.
- **Did not check.** Whether the documentation a change adds is true. Whether a proposed test asserts the right
  thing. Anything the tests of the project hold no input for.
- **Discarded on evidence.** Fifteen sets of tests that a model wrote to break the changes failed on the old code
  too, and were dropped.

## Against a review by reading and running

Four independent reviewers, one per change, read the issue and the code and ran their own tests, without seeing
AYA's output. They reported 29 findings. AYA puts 11 in front of the owner as something measured, 2 as a model's
opinion and 4 as a signal pointing at the place without naming the defect. It does not show 12. Seven of the 11
rest on checks that existed before we read the reviews; four on checks we added afterwards to catch them.

The reviewers found more. What they cannot give is the denominator: "2,542 inputs give the same result on both
versions, and they execute 10 of the change's 10 lines" is a statement a person can weigh, and silence from a
reviewer is not. So the two were joined: the reviewers' own tests were handed to AYA's arbiter. Finding by
finding, with the arbiter's result: [`results/contrast.md`](../results/contrast.md).

## Where it stops

It measures what code does, not what it should do. The sweep reaches only what the existing tests hold inputs
for, and its alterations were chosen after the reviews, so nothing here shows it finds defects nobody has seen.
For code that is entirely new the old version cannot arbitrate: a failing test about a new feature is an open
question for a person, never a regression (AYA got this wrong once, ranking such a test first, and the rule that
prevents it came out of that). The judge's reading of a requirement changed from one run to the next. It is an evidence layer under a review, not a
replacement for one.
