# Before approving ju26-issue-2283

`9df83c38` implementation for #2283, for MessagePack-CSharp#2283. 4 files, 228 changed lines.

## Does it break what existed?

**Something that held on the old code no longer does.** Whether that was meant to change is yours to decide.
- PASS: 5 test project(s) and their dependencies compile with the change
- PASS: 1240 of 1246 pre-existing tests pass on both old and new code; 0 regressed
- WARN: 44 of those tests are fed random data, so their inputs differ on every run; they were compared per test method, not input by input
- WARN: 2 of 2 modified unit(s) were run on the old and the new code with the same 20 input(s): 19 give the same result, 1 differ (1 changed)
- FAIL: 4 unit(s) were attacked with tests written to break them: 0 resisted (0 tests), 0 with a demonstrated regression, 0 with a failing test nothing can arbitrate, 4 failing on the old code too. 2 test(s) kept from earlier runs because they showed a regression were…
- WARN: 2542 input(s) nobody chose (151 taken from the existing tests, 2391 altered mechanically) were run on the old and the new code: 2542 give the same result, 0 differ. The inputs drove 10 of the change's 10 executable line(s). 72 input(s) that came with the chang…
- INFO: OPINION, not a measurement. gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) was asked whether 1 group(s) of differences the change's own text does not explain were meant: it reads 1 as what the change set out to do, 0 as not meant, 0 as…
- INFO: 0 member(s) depend on the change and are run by no original test; 0 were probed within a budget of 10 session(s) and 0 could be compared on old and new code

## Is the change itself guarded?

- PASS: 3 new and 0 modified tests; 3 pass on the new code, 0 fail
- PASS: 3 of 3 of those tests fail on the old code (they demonstrate the change); 0 pass without it; 0 cannot compile without it
- PASS: 10 of 10 changed executable source lines were executed by the test suite; 0 were never executed
- WARN: 8 of 11 deliberate breaks of changed lines were caught by a test; 3 went unnoticed
- WARN: 4 part(s) of the change were put back as they were, one at a time: 2 are needed by at least one test (2 only by tests that came with the change), 1 can be undone with every test still passing, 1 do not compile without the rest of the change
- INFO: 0 of 3 tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) were accepted: they compile, pass, and now execute 0 previously unexecuted line(s) and catch 0 of the 3 unnoticed break(s)
- INFO: OPINION, bounded by measurements. A model read 4 thing(s) the requirement asks for: 4 are shown by a test that fails without the change, 0 the change appears to do with no such test, 0 it does not address

## Where to look

**4 thing(s) to look at, in this order.** How far down to go is your decision.

1. **TypeCollector.TypeCollector(Compilation, AnalyzerOptions, ReferenceSymbols, ITypeSymbol, Action<Diagnostic>?, CancellationToken)** (`TypeCollector.cs`, lines 239)
   - a test written to break it fails on the new code and passes on the old (attacks/K1.cs, kept from the run of 2026-11-14 and run again now)
   - 1 input(s) give a different result than on the old code, and the change's own text does not explain it
   - Check: Something that held on the old code no longer does. Decide whether that was meant to change; if not, the change is wrong.
2. **TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)** (`TypeCollector.cs`, lines 985, 1077)
   - it modifies code that existed, 30 member(s) depend on it, and a mistake in it would go unnoticed
   - Check: Lines 985–985: with the old code back, no test fails. Find out what this part of the change is for, and whether the requirement asks for it.
3. **The change as a whole, over 2542 inputs nobody chose**
   - for 2 input(s) that came with the change the new code produces output that does not compile and reports no error of its own (for example: taken from a test that came with the change (GenericsFormatterTests.cs:523) — ERROR-GEN CS0246: The type or namespace name…
   - on 40 input(s) the code under test crashes on the old and on the new code alike (for example i032+a3 GenerationTests.cs:785 + an attribute argument made to repeat its sibling's)
   - Check: These inputs have no old code to be compared with. Read each: a crash or uncompilable output is a defect whatever the requirement says, unless the input itself is wrong and the tool should have said so.
4. **TypeCollector.ReportIfLocatedInCompilation(Action<Diagnostic>, Diagnostic)** (`TypeCollector.cs`, lines 1436–1450)
   - a mistake in it would go unnoticed, and nothing else was found against it
   - Check: Line 1446: decide what `diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)` must do. Altering it (&& → ||) makes no test fail.

3 other item(s) the change touches showed no signal. They are listed in `report.md`; no signal is not the same as correct.

## What nobody checked

- Requirement: Whether this change does what requirement MessagePack-CSharp#2283 asks. AYA measured what the code does under test; it has not interpreted the requirement.
- Side effects: Whether behaviour changes in already-existing members are intended. They are reachable from code the requirement does not mention.

This is advice with evidence, not an approval. The full list, every measurement and the sign-off block are in `report.md`.
