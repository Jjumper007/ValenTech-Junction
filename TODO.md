# AYA — TODO

What the prototype does not do yet, most useful first.

- [ ] **Deleted code.** The graph is built from the new code only, so a deleted class or method has no unit.
      It is listed as "not analysed", but what used it is not looked for. Needs a graph of the old code and a
      comparison of the two.
- [ ] **Requirement.** `--req` stores the reference and nothing more. Read the issue, turn it into obligations
      that each quote their source, have a person approve them, and tie every piece of evidence to one.
- [ ] **Importance from outside the code.** Let the owner mark the zones that matter to the business, and let a
      model propose more with the text it bases them on. A model may raise an item's place in the list, never lower it.
- [ ] **Build configurations.** Record which target frameworks and `#if` symbols the tests were built for, and
      say which changed lines were never compiled in any tested configuration.
- [ ] **Equivalent breaks.** Some deliberate breaks change nothing a caller can observe. Detect the obvious ones
      instead of asking for a test nobody can write.
- [ ] **Resume.** A review cannot restart from a finished step; every run repeats all of them.
- [ ] **A measured check of the ordering.** Seed known defects, hidden from the tool, and count how many land near
      the top of the list against reading the diff in order. The seeded defects must not be of the same kind as the
      deliberate breaks the ordering already uses.
- [ ] **Other model providers.** The writer is the `claude` command line; the session class is the only place
      that knows it.
- [ ] **Run AYA on its own source.**

## Ideas taken from two other tools

Compared on 2026-11-14. Neither runs the code: both read the repository and its history. AYA keeps what it
measures by running and adds what they see and it does not. All of these come from git or the compiler, no model.

From farol (github.com/winnin/farol), a static merge-risk map for pull requests:

- [ ] **Compare declarations between the old and the new code.** By a hash of each declaration's code, so a
      change of comments or formatting is not a change. Gives deleted members and changed public signatures
      cheaply, which is most of the "deleted code" item above.
- [ ] **Path to the entry point.** Not only "30 members depend on it" but which public API ends up running the
      changed code, and through which chain of calls (the shortest one).
- [ ] **Who knows the file.** From git history and CODEOWNERS: the people who have worked most on what changed,
      as candidates for the named owner.
- [ ] **A short summary to paste into the pull request.**
- [ ] **Configuration, infrastructure and CI files in a section of their own.**
- Not taken: the 0 to 100 score and the "likely safe to approve" verdict. A published test of farol showed a
  test that asserts nothing lowering the score from 57 to 17; AYA does not say that anything is safe.
- Not taken for now: calibrating the ordering against the repository's history (hundreds of runs of minutes each),
  lint findings and vulnerable dependencies.

From CodeScene, which measures where a codebase is fragile from its history:

- [ ] **Missing companions.** Files that have historically changed together with a changed file and are not part
      of this change. A dependency the compiler cannot see, and the closest thing to "what this may break in the
      old code".
- [ ] **History of the unit.** How many commits touched it and how many of those were fixes. Turns "how likely it
      is to be wrong" from an indication into a count. The target repository has 3,302 commits since 2017.
- [ ] **Complexity before and after.** Whether the change made the unit more convoluted, computed on both versions.
- [ ] **Who knows the code** (same as above): with a change written by an AI the author never has prior
      experience, so what matters is which person does.
- Not taken: the 1 to 10 code-health score, blocking the pull request, automatic refactoring.

From the original idea, kept for the record. They belong to a tool that rewrites code, which AYA no longer is.

- [ ] **Performance optimizations.** Performance-oriented transformations, beyond duplicate merging,
      repeated-block extraction, dead-code removal and logic simplification.
- [ ] **Automatic mode.** Apply all validated changes without per-change approval.
