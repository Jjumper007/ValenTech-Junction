# Before approving ju26-issue-2129

`083afd99` Agentic implementation of #2129 Feature Request: Allow generation of Formatter for a type in a referenced assembly, for MessagePack-CSharp#2129. 28 files, 1043 changed lines.

## Does it break what existed?

**No existing test fails, but 6 input(s) give a different result than on the old code that the change's own text does not explain.**
- PASS: 7 test project(s) and their dependencies compile with the change
- PASS: 1274 of 1280 pre-existing tests pass on both old and new code; 0 regressed
- WARN: 44 of those tests are fed random data, so their inputs differ on every run; they were compared per test method, not input by input
- WARN: 6 of 6 modified unit(s) were run on the old and the new code with the same 66 input(s): 60 give the same result, 6 differ (6 changed)
- WARN: 12 unit(s) were attacked with tests written to break them: 3 resisted (14 tests), 0 with a demonstrated regression, 2 with a failing test nothing can arbitrate, 7 failing on the old code too. 1 test(s) kept from earlier runs because they showed a regression we…
- WARN: 2542 input(s) nobody chose (151 taken from the existing tests, 2391 altered mechanically) were run on the old and the new code: 2542 give the same result, 0 differ. The inputs drove 58 of the change's 276 executable line(s), less than half: what they do not re…
- INFO: OPINION, not a measurement. gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) was asked whether 5 group(s) of differences the change's own text does not explain were meant: it reads 5 as what the change set out to do, 0 as not meant, 0 as…
- INFO: 1 member(s) depend on the change and are run by no original test; 0 were probed within a budget of 12 session(s) and 0 could be compared on old and new code

## Is the change itself guarded?

- PASS: 13 new and 0 modified tests; 13 pass on the new code, 0 fail
- WARN: 6 of 13 of those tests fail on the old code (they demonstrate the change); 0 pass without it; 7 cannot compile without it
- WARN: 211 of 276 changed executable source lines were executed by the test suite; 65 were never executed
- WARN: 36 of 55 deliberate breaks of changed lines were caught by a test; 19 went unnoticed; 51 more are on lines no test executes; 80 were not tried (budget)
- WARN: 8 part(s) of the change were put back as they were, one at a time: 0 are needed by at least one test, 3 can be undone with every test still passing, 5 do not compile without the rest of the change
- INFO: 8 of 8 tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) were accepted: they compile, pass, and now execute 5 previously unexecuted line(s) and catch 4 of the 19 unnoticed break(s); 21 gap(s) were not asked for (budget)
- WARN: OPINION, bounded by measurements. A model read 5 thing(s) the requirement asks for: 0 are shown by a test that fails without the change, 3 the change appears to do with no such test, 2 it does not address

## Where to look

**29 thing(s) to look at, in this order.** How far down to go is your decision.

1. **MsgPack00xMessagePackAnalyzer.SupportedDiagnostics** (`MsgPack00xMessagePackAnalyzer.cs`, lines 381–385)
   - 6 input(s) give a different result than on the old code, and the change's own text does not explain it
   - a model reads 6 difference(s) as the new functionality (its opinion; they keep their place)
   - Check: Go through the inputs whose result changed and confirm every change is one the requirement asks for.
2. **ObjectSerializationInfo.Equals(ObjectSerializationInfo?)** (`ObjectSerializationInfo.cs`, lines 168)
   - it modifies code that existed, 57 member(s) depend on it, and a mistake in it would go unnoticed
   - no break of its lines was tried (budget, or no test executes them)
   - Check: Lines 168 never run under any test: read them, or have them exercised.
3. **FormatterTemplate.TransformText()** (`FormatterTemplate.cs`, lines 33–35)
   - it modifies code that existed, 3 member(s) depend on it, and a mistake in it would go unnoticed
   - Check: Lines 33–35: with the old code back, no test fails. Find out what this part of the change is for, and whether the requirement asks for it.
4. **GeneratorUtilities.GetAnalyzerOption(IncrementalGeneratorInitializationContext)** (`GeneratorUtilities.cs`, lines 35–36, 38)
   - it modifies code that existed, 2 member(s) depend on it, and a mistake in it would go unnoticed
   - Check: Lines 35–36: with the old code back, no test fails. Find out what this part of the change is for, and whether the requirement asks for it.
5. **The requirement: 5 of the 5 thing(s) it asks for are not shown by any test**
   - a model reads "Allow the generator and analyzers to work with user-defined versions of the MessagePack attributes." as not addressed: The change uses contractless mode or a SurrogateType in the consuming assembly instead of recognizing user-defined copies of M…
   - a model reads "Source-generate the attributes themselves using [`IncrementalGeneratorInitializationContext.RegisterPostInitializationOutput`](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md#outputting-values) instead of them …
   - Check: Read the requirement against this list. For each thing no test shows, decide whether the change does it.
6. **ObjectSerializationInfo.IsUserDeclaredFormatter** (`ObjectSerializationInfo.cs`, lines 13–17)
   - none of its changed lines can be broken by the alterations AYA knows
   - Check: 1 dependent(s) are run by no test: check the ones that rely on what changed here.
7. **MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial** (`MsgPack00xMessagePackAnalyzer.cs`, lines 318–325)
   - a test written to break it fails on the new code, and the old code cannot say who is wrong (attacks/A12.cs)
   - Check: Line 324: decide what `true` must do. Altering it (true → false) makes no test fail.
8. **GeneratedFormatterCollector.BinaryWriteTypeFormat** (`GeneratedFormatterCollector.cs`, lines 19–22)
   - none of its changed lines can be broken by the alterations AYA knows
   - Check: Read the changed lines: no measurement stands in for it.
21 more with a signal are in `report.md`, section 1.

17 other item(s) the change touches showed no signal. They are listed in `report.md`; no signal is not the same as correct.

## What nobody checked

- Requirement: Whether this change does what requirement MessagePack-CSharp#2129 asks. AYA measured what the code does under test; it has not interpreted the requirement.
- Side effects: Whether behaviour changes in already-existing members are intended. They are reachable from code the requirement does not mention.
- Proposed tests: Whether the 8 accepted test(s) written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) assert the right behaviour. AYA checked that they compile, pass and reach their target; a model chose what to assert. They are proposals in the pr…

This is advice with evidence, not an approval. The full list, every measurement and the sign-off block are in `report.md`.
