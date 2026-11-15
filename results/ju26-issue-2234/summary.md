# Before approving ju26-issue-2234

`9007cee1` Implementation for #2234 The analyzer does not work if SuppressSourceGeneration = true, for MessagePack-CSharp#2234. 4 files, 266 changed lines.

## Does it break what existed?

**Something that held on the old code no longer does.** Whether that was meant to change is yours to decide.
- PASS: 6 test project(s) and their dependencies compile with the change
- PASS: 1254 of 1260 pre-existing tests pass on both old and new code; 0 regressed
- WARN: 44 of those tests are fed random data, so their inputs differ on every run; they were compared per test method, not input by input
- WARN: 1 of 1 modified unit(s) were run on the old and the new code with the same 14 input(s): 3 give the same result, 11 differ (11 changed)
- FAIL: 1 unit(s) were attacked with tests written to break them: 0 resisted (0 tests), 1 with a demonstrated regression, 0 with a failing test nothing can arbitrate, 0 failing on the old code too. 3 test(s) kept from earlier runs because they showed a regression were…
- WARN: 2542 input(s) nobody chose (151 taken from the existing tests, 2391 altered mechanically) were run on the old and the new code: 2493 give the same result, 49 differ (1 fails-differently, 38 now-works, 10 changed); 49 difference(s) show only once the input is a…
- INFO: OPINION, not a measurement. gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) was asked whether 17 group(s) of differences the change's own text does not explain were meant: it reads 0 as what the change set out to do, 17 as not meant, 0 …
- INFO: 0 member(s) depend on the change and are run by no original test; 0 were probed within a budget of 6 session(s) and 0 could be compared on old and new code

## Is the change itself guarded?

- PASS: 10 new and 0 modified tests; 10 pass on the new code, 0 fail
- WARN: 4 of 10 of those tests fail on the old code (they demonstrate the change); 6 pass without it; 0 cannot compile without it
- WARN: 13 of 18 changed executable source lines were executed by the test suite; 5 were never executed
- WARN: 7 of 7 deliberate breaks of changed lines were caught by a test; 0 went unnoticed; 3 more are on lines no test executes
- WARN: 6 part(s) of the change were put back as they were, one at a time: 3 are needed by at least one test (3 only by tests that came with the change), 3 can be undone with every test still passing
- INFO: 1 of 1 tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) were accepted: they compile, pass, and now execute 5 previously unexecuted line(s) and catch 0 of the 0 unnoticed break(s)
- WARN: OPINION, bounded by measurements. A model read 2 thing(s) the requirement asks for: 1 are shown by a test that fails without the change, 0 the change appears to do with no such test, 0 it does not address, and in 1 the change does less than, or something other…

## Where to look

**4 thing(s) to look at, in this order.** How far down to go is your decision.

1. **TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)** (`TypeCollector.cs`, lines 1008, 1023–1026, 1039–1042, 1148, 1163–1166, 1179–1182)
   - a test written to break it fails on the new code and passes on the old (attacks/A1.cs)
   - a test written to break it fails on the new code and passes on the old (attacks/K1.cs, kept from the run of 2026-11-14 and run again now)
   - Check: Something that held on the old code no longer does. Decide whether that was meant to change; if not, the change is wrong.
2. **The change as a whole, over 2542 inputs nobody chose**
   - 11 input(s) give a different result than on the old code, and the change's own text does not explain it
   - a model reads 12 of the differences as not what the change set out to do (its opinion, not a measurement)
   - Check: Go through the groups of differences in the report and confirm each is one the requirement asks for.
3. **The requirement: 1 of the 2 thing(s) it asks for are not shown by any test**
   - a model reads "The analyzer does not work if SuppressSourceGeneration = true." as contradicted: The change also alters analyzer behavior for generated MessagePackObject types without SuppressSourceGeneration = true, which goes beyond what the requirement asks …
   - Check: Read the requirement against this list. For each thing no test shows, decide whether the change does it.
4. **6 of the 10 tests that came with the change**
   - they were not shown to fail without the change, so they do not demonstrate it
   - Check: Read these tests: what they assert may hold whether or not the change is right.

1 other item(s) the change touches showed no signal. They are listed in `report.md`; no signal is not the same as correct.

## What nobody checked

- Requirement: Whether this change does what requirement MessagePack-CSharp#2234 asks. AYA measured what the code does under test; it has not interpreted the requirement.
- Test value: Whether SuppressSourceGenerationAnalyzerTests.AllowPrivateRequiredIsReported is evidence for this change: it passes on the old code too, so it does not demonstrate the new behaviour.
- Test value: Whether SuppressSourceGenerationAnalyzerTests.MemberTypeMustBeMessagePackObjectIsReported is evidence for this change: it passes on the old code too, so it does not demonstrate the new behaviour.
- Test value: Whether SuppressSourceGenerationAnalyzerTests.MemberWithoutKeyIsReported is evidence for this change: it passes on the old code too, so it does not demonstrate the new behaviour.

This is advice with evidence, not an approval. The full list, every measurement and the sign-off block are in `report.md`.
