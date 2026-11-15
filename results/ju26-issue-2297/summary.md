# Before approving ju26-issue-2297

`a0aa5ab2` implement issue #2297 : Add ExcludeFromCodeCoverage attribute to generated code, for MessagePack-CSharp#2297. 237 files, 1392 changed lines.

## Does it break what existed?

**Something that held on the old code no longer does.** Whether that was meant to change is yours to decide.
- PASS: 5 test project(s) and their dependencies compile with the change
- PASS: 1240 of 1246 pre-existing tests pass on both old and new code; 0 regressed
- WARN: 44 of those tests are fed random data, so their inputs differ on every run; they were compared per test method, not input by input
- WARN: 4 of 4 modified unit(s) were run on the old and the new code with the same 40 input(s): 0 give the same result, 40 differ (40 changed)
- FAIL: 7 unit(s) were attacked with tests written to break them: 0 resisted (0 tests), 3 with a demonstrated regression, 0 with a failing test nothing can arbitrate, 4 failing on the old code too. 10 test(s) kept from earlier runs because they showed a regression wer…
- FAIL: 2995 input(s) nobody chose (151 taken from the existing tests, 2844 altered mechanically) were run on the old and the new code: 1135 give the same result, 1860 differ (1857 changed, 3 now-fails); 3 input(s) that worked on the old code do not work on the new; 2…
- INFO: OPINION, not a measurement. gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) was asked whether 14 group(s) of differences the change's own text does not explain were meant: it reads 11 as what the change set out to do, 3 as not meant, 0 …
- INFO: 0 member(s) depend on the change and are run by no original test; 0 were probed within a budget of 6 session(s) and 0 could be compared on old and new code

## Is the change itself guarded?

- PASS: 2 new and 0 modified tests; 2 pass on the new code, 0 fail
- PASS: 2 of 2 of those tests fail on the old code (they demonstrate the change); 0 pass without it; 0 cannot compile without it
- PASS: 14 of 14 changed executable source lines were executed by the test suite; 0 were never executed
- PASS: 7 of 7 deliberate breaks of changed lines were caught by a test; 0 went unnoticed
- PASS: 8 part(s) of the change were put back as they were, one at a time: 8 are needed by at least one test, 0 can be undone with every test still passing
- WARN: OPINION, bounded by measurements. A model read 4 thing(s) the requirement asks for: 1 are shown by a test that fails without the change, 0 the change appears to do with no such test, 1 it does not address, and in 2 the change does less than, or something other…

## Where to look

**11 thing(s) to look at, in this order.** How far down to go is your decision.

1. **The change as a whole, over 2995 inputs nobody chose**
   - 3 input(s) that worked on the old code do not work on the new; 3 of them only once the input carries an alteration (for example: the input of an existing test (CompositeResolverGeneratorTests.cs:11), altered: [global::System.Diagnostics.CodeAnalysis.ExcludeFro…
   - 46 input(s) give a different result than on the old code, and the change's own text does not explain it
   - Check: These inputs have no old code to be compared with. Read each: a crash or uncompilable output is a defect whatever the requirement says, unless the input itself is wrong and the tool should have said so.
2. **CompositeResolverTemplate.TransformText()** (`CompositeResolverTemplate.cs`, lines 27, 56–58)
   - a test written to break it fails on the new code and passes on the old (attacks/A2.cs)
   - a test written to break it fails on the new code and passes on the old (attacks/K1.cs, kept from the run of 2026-11-14 and run again now)
   - Check: Something that held on the old code no longer does. Decide whether that was meant to change; if not, the change is wrong.
3. **EnumTemplate.TransformText()** (`EnumTemplate.cs`, lines 27)
   - a test written to break it fails on the new code and passes on the old (attacks/K2.cs, kept from the run of 2026-11-14 and run again now)
   - 10 input(s) differ only by lines the change itself adds or removes: the change showing through, which is not the same as correct
   - Check: Something that held on the old code no longer does. Decide whether that was meant to change; if not, the change is wrong.
4. **FormatterTemplate.TransformText()** (`FormatterTemplate.cs`, lines 33)
   - a test written to break it fails on the new code and passes on the old (attacks/A4.cs)
   - a test written to break it fails on the new code and passes on the old (attacks/K3.cs, kept from the run of 2026-11-14 and run again now)
   - Check: Something that held on the old code no longer does. Decide whether that was meant to change; if not, the change is wrong.
5. **ResolverTemplate.TransformText()** (`ResolverTemplate.cs`, lines 38, 55–57, 67)
   - a test written to break it fails on the new code and passes on the old (attacks/A7.cs)
   - a test written to break it fails on the new code and passes on the old (attacks/K4.cs, kept from the run of 2026-11-14 and run again now)
   - Check: Something that held on the old code no longer does. Decide whether that was meant to change; if not, the change is wrong.
6. **TransformUtilities.EmitNestingTypesAndNamespaces(TypeContainer, Action<string>)** (`TransformUtilities.cs`, lines 58)
   - 4 input(s) give a different result than on the old code, and the change's own text does not explain it
   - 6 input(s) differ only by lines the change itself adds or removes: the change showing through, which is not the same as correct
   - Check: Go through the inputs whose result changed and confirm every change is one the requirement asks for.
7. **The requirement: 3 of the 4 thing(s) it asks for are not shown by any test**
   - a model reads "I would like all generated code to be decorated with ExcludeFromCodeCoverage attribute." as contradicted: The change wraps the attribute in `#if !NETSTANDARD2_0 && !NETFRAMEWORK`, so generated code is not decorated on those target frameworks, an…
   - a model reads "Add ExcludeFromCodeCoverage attribute to generated code." as contradicted: The change alters `TransformUtilities.cs` to emit `{\r\n` instead of `{` for nesting types with generic constraints, modifying existing formatting behaviour that the requ…
   - Check: Read the requirement against this list. For each thing no test shows, decide whether the change does it.
8. **StringKeyFormatterTemplate.TransformText()** (`StringKeyFormatterTemplate.cs`, lines 37)
   - the analysis cannot see all of it: it is generated code: what produces it was not re-run, and the graph does not link it to its generator
3 more with a signal are in `report.md`, section 1.

3 other item(s) the change touches showed no signal. They are listed in `report.md`; no signal is not the same as correct.

## What nobody checked

- Requirement: Whether this change does what requirement MessagePack-CSharp#2297 asks. AYA measured what the code does under test; it has not interpreted the requirement.
- Side effects: Whether behaviour changes in already-existing members are intended. They are reachable from code the requirement does not mention.

This is advice with evidence, not an approval. The full list, every measurement and the sign-off block are in `report.md`.
