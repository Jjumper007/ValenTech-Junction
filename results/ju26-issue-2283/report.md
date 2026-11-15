# Review evidence — ju26-issue-2283

| | |
|---|---|
| Requirement | MessagePack-CSharp#2283 |
| Change | `9df83c38` implementation for #2283 |
| Written by | Stelian Vultur, 2026-11-12T01:14:05+02:00 |
| Compared with | `32258fdb` |
| Built with | .NET SDK 9.0.321 (the repository selects 8.0.428) |
| Produced by | aya 0.1.0, 2026-11-15T02:06:47Z |
| Evidence file | `evidence.json`, sha256 `6ac58bc7329e91d7454420b7951b03ab51ea7a1b52aa3373f5bb28ba550198eb` |

Every statement below comes from a tool run, and its source is named. Where a model wrote a probe or a test, what is stated is what running it showed, never what the model said.

## 1. Where to look, most deserving first

Everything the change touches is in this list; nothing was left out of it. The order says where a mistake is most likely to matter and least likely to be noticed. The last tier means no signal was found, not that the code is right. How far down to go is the owner's decision.

| # | Tier | What | Why it is here |
|---:|---|---|---|
| 1 | 1. A check that passed on the old code fails on the new | TypeCollector.TypeCollector(Compilation, AnalyzerOptions, ReferenceSymbols, ITypeSymbol, Action<Diagnostic>?, CancellationToken) | a test written to break it fails on the new code and passes on the old (attacks/K1.cs, kept from the run of 2026-11-14 and run again now) |
| 2 | 3. Existing code others depend on, where a mistake would go unnoticed | TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?) | it modifies code that existed, 30 member(s) depend on it, and a mistake in it would go unnoticed |
| 3 | 4. Could not be measured | The change as a whole, over 2542 inputs nobody chose | for 2 input(s) that came with the change the new code produces output that does not compile and reports no error of its own (for example: taken from a test that came with the change (GenericsFormatterTests.cs:523) — ERROR-GEN CS0246: The type or namespace name 'MyGenericStruct<>' could not be found (are you missing a using directive or an assembly reference?)) |
| 4 | 5. A mistake here would go unnoticed | TypeCollector.ReportIfLocatedInCompilation(Action<Diagnostic>, Diagnostic) | a mistake in it would go unnoticed, and nothing else was found against it |
| 5 | 6. No signal found | TypeCollector.GetPropertyIdentifierLocation(IPropertySymbol) | nothing was found against it; that is not a statement that it is correct |
| 6 | 6. No signal found | README.md | documentation: no tool checks that the text is accurate, and it cannot break the code |
| 7 | 6. No signal found | doc/analyzers/MsgPack004.md | documentation: no tool checks that the text is accurate, and it cannot break the code |

Rule used. Tier 1: an existing test now fails because of it, an input that gave a result now fails, or a test written to break it fails on the new code and passes on the old. Tier 2: an input gives a different result than on the old code. Tier 3: it modifies code that existed, other code depends on it, and a line is never executed or can be broken without any test failing. Tier 4: the analysis could not measure it (deleted code, modified declarations, no break could be tried, generated or conditionally compiled code, a failing test nothing can arbitrate). Tier 5: a line is never executed or can be broken unnoticed, in code nothing else depends on or that is new; or tests that came with the change were not shown to fail without it. Tier 6: none of the above was found. Inside a tier, a difference the change's own text does not explain, or that a model reads as not meant, comes before one made only of lines the change adds or removes; then the unit with more code depending on it comes first. A model's reading can only move an item up.

### 1. TypeCollector.TypeCollector(Compilation, AnalyzerOptions, ReferenceSymbols, ITypeSymbol, Action<Diagnostic>?, CancellationToken)

`src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs`, changed lines 239

- **Why it is here**
  - a test written to break it fails on the new code and passes on the old (attacks/K1.cs, kept from the run of 2026-11-14 and run again now)
  - 1 input(s) give a different result than on the old code, and the change's own text does not explain it
  - a model reads 1 difference(s) as the new functionality (its opinion; they keep their place)
- **How likely it is to be wrong (indications, not a measurement)**
  - modifies code that already existed (1 line(s))
  - the changed lines touch a condition
  - old and new code differ on 1 of 10 input(s): 1 changed (probes/P2.cs)
  - a test written to break it fails on the old code as well, so the change is not the cause (attacks/A1.cs)
- **How much breaks if it is wrong**
  - 30 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as TypeCollector.Collect(Compilation, AnalyzerOptions, ReferenceSymbols, Action<Diagnostic>?, ITypeSymbol, CancellationToken), MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - breaking line 239 made 37 test(s) fail (measured)
  - 0 of the 28 dependents with executable code are run by no test, so a break reaching them would not be seen
- **Whether anything would notice**
  - 1 of 1 changed executable line(s) ran under the tests
  - 2 of 2 deliberate break(s) made a test fail
  - with lines 239–239 put back as they were, 1 test(s) fail, all of them tests that came with the change
- **For the owner to check**
  - Something that held on the old code no longer does. Decide whether that was meant to change; if not, the change is wrong.
  - Go through the inputs whose result changed and confirm every change is one the requirement asks for.

### 2. TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)

`src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs`, changed lines 985, 1077

- **Why it is here**
  - it modifies code that existed, 30 member(s) depend on it, and a mistake in it would go unnoticed
- **How likely it is to be wrong (indications, not a measurement)**
  - modifies code that already existed (2 line(s))
  - the changed lines touch null handling
  - old and new code give the same result on 10 input(s) (probes/P1.cs)
  - a test written to break it fails on the old code as well, so the change is not the cause (attacks/A2.cs)
- **How much breaks if it is wrong**
  - 30 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as TypeCollector.Collect(), TypeCollector.Collect(Compilation, AnalyzerOptions, ReferenceSymbols, Action<Diagnostic>?, ITypeSymbol, CancellationToken)
  - breaking line 1077 made 3 test(s) fail (measured)
  - 0 of the 28 dependents with executable code are run by no test, so a break reaching them would not be seen
- **Whether anything would notice**
  - 2 of 2 changed executable line(s) ran under the tests
  - 4 of 4 deliberate break(s) made a test fail
  - lines 985–985 can be put back as they were and every test still passes: no test needs this part of the change
  - with lines 1077–1077 put back as they were, 3 test(s) fail, all of them tests that came with the change
- **For the owner to check**
  - Lines 985–985: with the old code back, no test fails. Find out what this part of the change is for, and whether the requirement asks for it.

### 3. The change as a whole, over 2542 inputs nobody chose

`tests/MessagePack.SourceGenerator.Tests/MessagePack.SourceGenerator.Tests.csproj`

- **Why it is here**
  - for 2 input(s) that came with the change the new code produces output that does not compile and reports no error of its own (for example: taken from a test that came with the change (GenericsFormatterTests.cs:523) — ERROR-GEN CS0246: The type or namespace name 'MyGenericStruct<>' could not be found (are you missing a using directive or an assembly reference?))
  - on 40 input(s) the code under test crashes on the old and on the new code alike (for example i032+a3 GenerationTests.cs:785 + an attribute argument made to repeat its sibling's)
- **How likely it is to be wrong (indications, not a measurement)**
  - 151 input(s) taken from the existing tests of tests/MessagePack.SourceGenerator.Tests/MessagePack.SourceGenerator.Tests.csproj as they are, 2391 altered mechanically; no model chose any of them
  - 58 input(s) altered: the types other types use moved to a referenced library
  - 151 input(s) altered: every type nested in an outer class
  - 30 input(s) altered: an attribute argument made to repeat its sibling's
  - 30 input(s) altered: an attribute argument changed to the other kind of literal
  - 30 input(s) altered: an attribute argument made null
  - 31 input(s) altered: the attributes removed from every type another type uses
  - 131 input(s) altered: every attribute removed from every type
  - 91 input(s) altered: every attribute removed from every member
  - 11 input(s) altered: `partial` removed wherever it appears
  - 2 input(s) altered: `sealed` removed wherever it appears
  - 7 input(s) altered: `static` removed wherever it appears
  - 1 input(s) altered: `abstract` removed wherever it appears
  - 2 input(s) altered: `readonly` removed wherever it appears
  - 86 input(s) altered: the first member removed from every type
  - 86 input(s) altered: the last member removed from every type
  - 34 input(s) altered: the base types removed from every type
  - 90 input(s) altered: the setter removed from every property
  - 140 input(s) altered: every class made a record
  - 139 input(s) altered: every class made a struct
  - 9 input(s) altered: every struct made a class
  - 13 input(s) altered: every record made a class
  - 130 input(s) altered: every `public` type made `internal`
  - 32 input(s) altered: every type that is not `public` made `public`
  - 121 input(s) altered: every `public` member made `private`
  - 121 input(s) altered: every `public` member made `internal`
  - 10 input(s) altered: every `private` member made `public`
  - 92 input(s) altered: every auto-property made a field
  - 26 input(s) altered: every field made an auto-property
  - 90 input(s) altered: every setter made `init`
  - 89 input(s) altered: every setter made `private`
  - 139 input(s) altered: every class that can be made `sealed`
  - 139 input(s) altered: every class that can be made `abstract`
  - 113 input(s) altered: the type of every member made nullable
  - 117 input(s) altered: the type of every member made an array
  - 2542 of 2542 give the same result on old and new code; 0 differ
- **How much breaks if it is wrong**
  - an input here is a whole program handed to the changed code, as a user of the library would write it
- **Whether anything would notice**
  - the inputs drove 10 of the change's 10 executable line(s), measured on the new code
- **What the analysis cannot see**
  - the harness that feeds the inputs and records the results was written by a model; it was accepted because it runs on both versions, gives the same results twice on the old code and executes the change, not because it was read
- **For the owner to check**
  - These inputs have no old code to be compared with. Read each: a crash or uncompilable output is a defect whatever the requirement says, unless the input itself is wrong and the tool should have said so.

### 4. TypeCollector.ReportIfLocatedInCompilation(Action<Diagnostic>, Diagnostic)

`src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs`, changed lines 1436–1450

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (15 line(s))
  - the changed lines touch a condition
  - a test written to break it fails on the old code as well, so the change is not the cause (attacks/A3.cs)
- **How much breaks if it is wrong**
  - 31 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as TypeCollector.Collect(Compilation, AnalyzerOptions, ReferenceSymbols, Action<Diagnostic>?, ITypeSymbol, CancellationToken), MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - breaking line 1448 made 36 test(s) fail (measured)
  - 0 of the 28 dependents with executable code are run by no test, so a break reaching them would not be seen
- **Whether anything would notice**
  - 6 of 6 changed executable line(s) ran under the tests
  - 1 of 4 deliberate break(s) made a test fail; unnoticed: line 1446 (&& → ||); line 1446 (a && b → a); line 1446 (a && b → b)
  - lines 1429–1451 could not be undone on their own: the code does not compile without the rest of the change
- **For the owner to check**
  - Line 1446: decide what `diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)` must do. Altering it (&& → ||) makes no test fail.
  - Line 1446: decide what `diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)` must do. Altering it (a && b → a) makes no test fail.
  - Line 1446: decide what `diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)` must do. Altering it (a && b → b) makes no test fail.

## 2. What changed

| Role | Files | Lines added | Lines removed |
|---|---:|---:|---:|
| docs | 2 | 10 | 0 |
| source | 1 | 26 | 3 |
| test | 1 | 189 | 0 |

## 3. Changed symbols, from the bottom of the dependency graph up

Graph: 6232 nodes, 19363 edges, 37 layers, over 17 projects. Layer 0 depends on nothing else in the code; a higher layer depends on lower ones.

| Layer | Symbol | Change | Public API | Dependents | Tests that can reach it | Changed lines run / not run | Measured |
|---:|---|---|---|---:|---:|---|---|
| 0 | `TypeCollector.GetPropertyIdentifierLocation(IPropertySymbol)` | added | no | 31 | 133 | 1 / 0 | executed |
| 1 | `TypeCollector.ReportIfLocatedInCompilation(Action<Diagnostic>, Diagnostic)` | added | no | 31 | 133 | 6 / 0 | executed |
| 13 | `TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)` | modified | no | 30 | 133 | 2 / 0 | executed |
| 13 | `TypeCollector.TypeCollector(Compilation, AnalyzerOptions, ReferenceSymbols, ITypeSymbol, Action<Diagnostic>?, CancellationToken)` | modified | no | 30 | 133 | 1 / 0 | executed |

Plus 6 test methods and test helpers added or modified by the change (section 4 says what they prove).

## 4. What was checked

| ID | Result | Finding | Checked by |
|---|---|---|---|
| E1 | info | 4 changed files fall into 4 groups of similar edits | git diff; files grouped by the distinct lines they edit, then split by exact edit (deterministic) |
| E2 | info | 4 non-test symbols changed across layers 0–13; the most depended-on has 31 dependent member(s); 0 public API symbols touched | Roslyn semantic model over 17 projects (6232 nodes, 19363 edges) |
| E3 | info | Old and new code were both built with .NET SDK 9.0.321, not the 8.0.428 the repository selects, because its source generator needs a newer compiler | dotnet --version; compiler warning CS9057 |
| E4 | pass | 5 test project(s) and their dependencies compile with the change | dotnet build (new code) |
| E5 | pass | 1240 of 1246 pre-existing tests pass on both old and new code; 0 regressed | dotnet test on old and new code, compared test by test |
| E6 | warn | 44 of those tests are fed random data, so their inputs differ on every run; they were compared per test method, not input by input | dotnet test on old and new code |
| E7 | pass | 3 new and 0 modified tests; 3 pass on the new code, 0 fail | dotnet test (new code) |
| E8 | pass | 3 of 3 of those tests fail on the old code (they demonstrate the change); 0 pass without it; 0 cannot compile without it | dotnet test on old source with the change's test files applied |
| E9 | pass | 10 of 10 changed executable source lines were executed by the test suite; 0 were never executed | Microsoft Code Coverage during dotnet test (new code) |
| E10 | warn | 8 of 11 deliberate breaks of changed lines were caught by a test; 3 went unnoticed | each changed line altered in turn, then dotnet test (mutation testing) |
| E11 | warn | 4 part(s) of the change were put back as they were, one at a time: 2 are needed by at least one test (2 only by tests that came with the change), 1 can be undone with every test still passing, 1 do not compile without the rest of the change | the old lines of one part restored in the new code, then dotnet test |
| E12 | info | 0 of 3 tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) were accepted: they compile, pass, and now execute 0 previously unexecuted line(s) and catch 0 of the 3 unnoticed break(s) | gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) wrote them; dotnet test and coverage checked each one, and each accepted test was run against every other unnoticed break |
| E13 | warn | 2 of 2 modified unit(s) were run on the old and the new code with the same 20 input(s): 19 give the same result, 1 differ (1 changed) | probes written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line); the same probe run twice on the old code and once on the new |
| E14 | fail | 4 unit(s) were attacked with tests written to break them: 0 resisted (0 tests), 0 with a demonstrated regression, 0 with a failing test nothing can arbitrate, 4 failing on the old code too. 2 test(s) kept from earlier runs because they showed a regression were run again: 1 still fail on the new code and pass on the old | tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line); each file is built and run here, and a failing test is run on the old code, which decides whether the change caused it |
| E15 | warn | 2542 input(s) nobody chose (151 taken from the existing tests, 2391 altered mechanically) were run on the old and the new code: 2542 give the same result, 0 differ. The inputs drove 10 of the change's 10 executable line(s). 72 input(s) that came with the change were run on the new code alone: 0 make the code under test crash, 2 get output that does not compile with no error reported. on 40 input(s) the code under test crashes on the old and on the new code alike (for example i032+a3 GenerationTests.cs:785 + an attribute argument made to repeat its sibling's) | inputs built by AYA from the tests of tests/MessagePack.SourceGenerator.Tests/MessagePack.SourceGenerator.Tests.csproj, with no model involved; the harness that feeds them was written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) and run twice on the old code and once on the new |
| E16 | info | OPINION, not a measurement. gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) was asked whether 1 group(s) of differences the change's own text does not explain were meant: it reads 1 as what the change set out to do, 0 as not meant, 0 as artefacts of the build or the run and 0 as unclear | 1 session(s); it was given the text of the requirement, supplied by the owner, the commit message(s) of the change, 6 line(s) of documentation the change added, 19 comment line(s) the change added to the code. A reading moves nothing down the list |
| E17 | info | OPINION, bounded by measurements. A model read 4 thing(s) the requirement asks for: 4 are shown by a test that fails without the change, 0 the change appears to do with no such test, 0 it does not address | the words of each thing asked for were checked to be in the requirement, and a test named as showing one was checked to be a test that fails on the old code; the reading itself is the model's |
| E18 | info | 0 member(s) depend on the change and are run by no original test; 0 were probed within a budget of 10 session(s) and 0 could be compared on old and new code | probes written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line); 0 session(s) used |

E10 detail:
```
src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs:1446  && → ||  in  diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)
src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs:1446  a && b → a  in  diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)
src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs:1446  a && b → b  in  diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)
```

E11 detail:
```
src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs:985–985  no test fails with the old lines back
```

E12 detail:
```
R1: rejected-no-effect — The tests pass on the code as it is, but they still pass when the line is altered as described.
R2: rejected-no-effect — The tests pass on the code as it is, but they still pass when the line is altered as described.
R3: rejected-compile — It does not compile:
<home>\.aya-b\wt\9df83cwork\tests\MessagePack.SourceGenerator.Tests\AyaCandidates\R3.cs(1,11): error CS1003: Syntax error, ',' expected
<home>\.aya-b\wt\9df83cwork\tests\MessagePack.SourceGenerator.Tests\AyaCandidates\R3.cs(1,50): error CS1056: Unexpected character '`'
<home>\.aya-b\wt\9df83cwork\tests\MessagePack.SourceGenerator.Tests\AyaCandidates\R3.cs(1,58): error CS1056: Unexpected character '→'
<home>\.aya-b\wt\9df83cwork\tests\MessagePack.SourceGenerator.Tests\AyaCandidates\R3.cs(1,61): error CS1056: Unexpected character '`'
<home>\.aya-b\wt\9df83cwork\tests\MessagePack.SourceGenerator.Tests\AyaCandidates\R3.cs(1,137): error CS1056: Unexpected character '`'
<home>\.aya-b\wt\9df83cwork\tests\MessagePack.SourceGenerator.Tests\AyaCandidates\R3.cs(1,146): error CS1056: Unexpected character '`'
```

E14 detail:
```
A1 TypeCollector.TypeCollector(Compilation, AnalyzerOptions, ReferenceSymbols, ITypeSymbol, Action<Diagnostic>?, CancellationToken): fails-on-both — 4 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 1 other(s) pass
A2 TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?): fails-on-both — 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass
A3 TypeCollector.ReportIfLocatedInCompilation(Action<Diagnostic>, Diagnostic): fails-on-both — 3 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 1 other(s) pass
A4 TypeCollector.GetPropertyIdentifierLocation(IPropertySymbol): fails-on-both — 4 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass
```

E15 detail:
```
58 input(s): the types other types use moved to a referenced library
151 input(s): every type nested in an outer class
30 input(s): an attribute argument made to repeat its sibling's
30 input(s): an attribute argument changed to the other kind of literal
30 input(s): an attribute argument made null
31 input(s): the attributes removed from every type another type uses
131 input(s): every attribute removed from every type
91 input(s): every attribute removed from every member
11 input(s): `partial` removed wherever it appears
2 input(s): `sealed` removed wherever it appears
7 input(s): `static` removed wherever it appears
1 input(s): `abstract` removed wherever it appears
2 input(s): `readonly` removed wherever it appears
86 input(s): the first member removed from every type
86 input(s): the last member removed from every type
34 input(s): the base types removed from every type
90 input(s): the setter removed from every property
140 input(s): every class made a record
139 input(s): every class made a struct
9 input(s): every struct made a class
13 input(s): every record made a class
130 input(s): every `public` type made `internal`
32 input(s): every type that is not `public` made `public`
121 input(s): every `public` member made `private`
121 input(s): every `public` member made `internal`
10 input(s): every `private` member made `public`
92 input(s): every auto-property made a field
26 input(s): every field made an auto-property
90 input(s): every setter made `init`
89 input(s): every setter made `private`
139 input(s): every class that can be made `sealed`
139 input(s): every class that can be made `abstract`
113 input(s): the type of every member made nullable
117 input(s): the type of every member made an array
```

Tests that came with the change, run against the old code:

| Test | Origin | On new code | On old code | Meaning |
|---|---|---|---|---|
| `GenericsFormatterTests.ClosedGenericRecordStruct_FromMetadataReference` | new | Passed | Failed | fails without the change: it demonstrates it |
| `GenericsFormatterTests.ClosedGenericStruct_FromMetadataReference` | new | Passed | Failed | fails without the change: it demonstrates it |
| `GenericsFormatterTests.ClosedGenericStruct_FromMetadataReference_UnkeyedMember` | new | Passed | Failed | fails without the change: it demonstrates it |

Test runs:

| Code under test | Project | Compiled | Passed | Failed | Skipped or not run | Seconds | Problem |
|---|---|---|---:|---:|---:|---:|---|
| new | MessagePack.AspNetCoreMvcFormatter.Tests | yes | 6 | 0 | 0 | 14.8 |  |
| new | MessagePack.Experimental.Tests | yes | 95 | 0 | 0 | 9.8 |  |
| new | MessagePack.GeneratedCode.Tests | yes | 11 | 0 | 0 | 13.7 |  |
| new | MessagePack.SourceGenerator.Tests | yes | 170 | 0 | 0 | 20.5 |  |
| new | MessagePack.Tests | yes | 961 | 0 | 6 | 17.7 |  |
| old | MessagePack.AspNetCoreMvcFormatter.Tests | yes | 6 | 0 | 0 | 10.6 |  |
| old | MessagePack.Experimental.Tests | yes | 95 | 0 | 0 | 6.7 |  |
| old | MessagePack.GeneratedCode.Tests | yes | 11 | 0 | 0 | 7.8 |  |
| old | MessagePack.SourceGenerator.Tests | yes | 167 | 0 | 0 | 18.5 |  |
| old | MessagePack.Tests | yes | 961 | 0 | 6 | 16.8 |  |
| old + new tests | MessagePack.SourceGenerator.Tests | yes | 0 | 3 | 0 | 10.2 |  |

Deliberate breaks of the changed lines. Each row alters one line and re-runs the tests that execute it:

| Break | Where | Alteration | Original code | Result |
|---|---|---|---|---|
| M10 | TypeCollector.cs:1446 | && → \|\| | `diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)` | **not noticed by any test** |
| M11 | TypeCollector.cs:1446 | a && b → a | `diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)` | **not noticed by any test** |
| M12 | TypeCollector.cs:1446 | a && b → b | `diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)` | **not noticed by any test** |
| M1 | TypeCollector.cs:239 | statement removed | `this.reportDiagnostic = reportAnalyzerDiagnostic is null ? null : diagnostic => ReportIfLocatedInCompilation(r…` | caught by `GenerationTests.DeserializingConstructorParameterMemberTypeAssignability_Incompatible` and 35 more |
| M2 | TypeCollector.cs:239 | is X → is not X | `reportAnalyzerDiagnostic is null` | caught by `GenerationTests.DeserializingConstructorParameterMemberTypeAssignability_Incompatible` and 36 more |
| M3 | TypeCollector.cs:985 | statement removed | `this.reportDiagnostic?.Invoke(Diagnostic.Create(MsgPack00xMessagePackAnalyzer.BothStringAndIntKeyAreNull, GetP…` | caught by `MsgPack00xAnalyzerTests.NullStringKey` |
| M4 | TypeCollector.cs:985 | call passed as argument → default | `Diagnostic.Create(MsgPack00xMessagePackAnalyzer.BothStringAndIntKeyAreNull, GetPropertyIdentifierLocation(item…` | caught by `MsgPack00xAnalyzerTests.NullStringKey` |
| M5 | TypeCollector.cs:985 | call passed as argument → default | `GetPropertyIdentifierLocation(item)` | caught by `MsgPack00xAnalyzerTests.NullStringKey` |
| M7 | TypeCollector.cs:1077 | call passed as argument → default | `item.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().GetLocation()` | caught by `MsgPack00xAnalyzerTests.AddAttributeToField_WithComment` and 2 more |
| M9 | TypeCollector.cs:1434 | => x → => default | `(property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as PropertyDeclarationSyntax)?.Identifier.Ge…` | caught by `MsgPack00xAnalyzerTests.NullStringKey` |
| M13 | TypeCollector.cs:1448 | statement removed | `report(diagnostic);` | caught by `GenerationTests.DeserializingConstructorParameterMemberTypeAssignability_Incompatible` and 35 more |
| M6 | TypeCollector.cs:985 | call passed as argument → default | `formattedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)` | does not compile; discarded |
| M8 | TypeCollector.cs:1077 | call passed as argument → default | `formattedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)` | does not compile; discarded |

Tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) to fill the gaps. None is trusted on the model's word; each was built and run:

| Request | Gap | Target | Tries | Result | Saved as |
|---|---|---|---:|---|---|
| R1 | unnoticed break: && → \|\| in 'diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)' | `TypeCollector.ReportIfLocatedInCompilation(Action<Diagnostic>, Diagnostic)` line(s) 1446 | 3 | rejected-no-effect: The tests pass on the code as it is, but they still pass when the line is altered as described. | proposed-tests/R1.rejected.cs |
| R2 | unnoticed break: a && b → a in 'diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)' | `TypeCollector.ReportIfLocatedInCompilation(Action<Diagnostic>, Diagnostic)` line(s) 1446 | 3 | rejected-no-effect: The tests pass on the code as it is, but they still pass when the line is altered as described. | proposed-tests/R2.rejected.cs |
| R3 | unnoticed break: a && b → b in 'diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)' | `TypeCollector.ReportIfLocatedInCompilation(Action<Diagnostic>, Diagnostic)` line(s) 1446 | 3 | rejected-compile: It does not compile: | proposed-tests/R3.rejected.cs |

Old code against new code. A probe written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) records what the code does on a set of inputs; it is run twice on the old code, to discard what is unstable, and once on the new:

| Probe | Unit | Inputs compared | Same | Differ | Unstable | Tries | Outcome | Saved as |
|---|---|---:|---:|---:|---:|---:|---|---|
| P1 | `TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)` | 10 | 10 | 0 | 0 | 2 | 10 case(s) give the same result on the old and the new code | probes/P1.cs |
| P2 | `TypeCollector.TypeCollector(Compilation, AnalyzerOptions, ReferenceSymbols, ITypeSymbol, Action<Diagnostic>?, CancellationToken)` | 10 | 9 | 1 | 0 | 1 | 1 of 10 case(s) differ: 1 changed | probes/P2.cs |
| S1 | `the change as a whole, over 2542 inputs built from the existing tests` | 2542 | 2542 | 0 | 0 | 1 | 2542 case(s) give the same result on the old and the new code | probes/S1.cs |

P2: inputs whose result differs, the same alteration grouped together:

- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): MetadataReference_ValidClosedGenericRecordStruct
  ```
  -   [Warning] CS8785 (None): Generator 'MessagePackGenerator' failed to generate source. It will not contribute to the output and compilation errors may occur as a result. Exception was of type 'Index…
  - System.IndexOutOfRangeException: Index was outside the bounds of the array.
  - -----
  + --- MessagePack.SourceGenerator/MessagePack.SourceGenerator.MessagePackGenerator/App.AppResolver.g.cs ---
  + // <auto-generated />   (×3)
  + #pragma warning disable 618, 612, 414, 168, CS1591, SA1129, SA1309, SA1312, SA1403, SA1649   (×3)
  + using MsgPack = global::MessagePack;   (×3)
  + [assembly: MsgPack::Internal.GeneratedAssemblyMessagePackResolverAttribute(typeof(App.AppResolver), 3, 0)]
  + namespace App {   (×3)
  + /// <summary>A MessagePack resolver that uses generated formatters for types in this assembly.</summary>
  + partial class AppResolver : MsgPack::IFormatterResolver
  + {   (×27)
  ```
  Model's reading, not a measurement: intended. The requirement asks to eliminate the CS8785 IndexOutOfRangeException crash and generate the MessagePack resolver for a closed generic record struct from a referenced assembly. It quotes: "A clean build, with a generated resolver in `app`."

Sweep over inputs nobody chose (S1 above). AYA found 151 input(s) in the tests of `tests/MessagePack.SourceGenerator.Tests/MessagePack.SourceGenerator.Tests.csproj` as they were before the change, took 151 as they are and altered 2391 mechanically. No model chose or wrote any input; a model wrote only the harness that feeds them to the code and records what comes out. Run on the new code, the inputs executed 10 of the change's 10 executable line(s). The inputs are in `sweep-inputs.tsv`.

Seen by the sweep: on 40 input(s) the code under test crashes on the old and on the new code alike (for example i032+a3 GenerationTests.cs:785 + an attribute argument made to repeat its sibling's).

Inputs that came with the change. 72 of them (the sources its own tests hold, as they are and altered) were run on the new code alone, since the old code has nothing to say about them. They are held to what must be true of any input: the code under test does not crash, and what it produces compiles unless it reported an error itself. 2 break one of the two. The inputs are in `sweep-new-inputs.tsv`.

- **output does not compile, no error reported**: taken from a test that came with the change (GenericsFormatterTests.cs:523). `ERROR-GEN CS0246: The type or namespace name 'MyGenericStruct<>' could not be found (are you missing a using directive or an assembly reference?)`
- **output does not compile, no error reported**: taken from a test that came with the change (GenericsFormatterTests.cs:562). `ERROR-GEN CS0246: The type or namespace name 'Item<>' could not be found (are you missing a using directive or an assembly reference?)`

- 58 input(s): the types other types use moved to a referenced library — a fixed alteration
- 151 input(s): every type nested in an outer class — a fixed alteration
- 30 input(s): an attribute argument made to repeat its sibling's — a fixed alteration
- 30 input(s): an attribute argument changed to the other kind of literal — a fixed alteration
- 30 input(s): an attribute argument made null — a fixed alteration
- 31 input(s): the attributes removed from every type another type uses — a fixed alteration
- 131 input(s): every attribute removed from every type — a fixed alteration
- 91 input(s): every attribute removed from every member — a fixed alteration
- 11 input(s): `partial` removed wherever it appears — a fixed alteration
- 2 input(s): `sealed` removed wherever it appears — a fixed alteration
- 7 input(s): `static` removed wherever it appears — a fixed alteration
- 1 input(s): `abstract` removed wherever it appears — a fixed alteration
- 2 input(s): `readonly` removed wherever it appears — a fixed alteration
- 86 input(s): the first member removed from every type — a fixed alteration
- 86 input(s): the last member removed from every type — a fixed alteration
- 34 input(s): the base types removed from every type — a fixed alteration
- 90 input(s): the setter removed from every property — a fixed alteration
- 140 input(s): every class made a record — a fixed alteration
- 139 input(s): every class made a struct — a fixed alteration
- 9 input(s): every struct made a class — a fixed alteration
- 13 input(s): every record made a class — a fixed alteration
- 130 input(s): every `public` type made `internal` — a fixed alteration
- 32 input(s): every type that is not `public` made `public` — a fixed alteration
- 121 input(s): every `public` member made `private` — a fixed alteration
- 121 input(s): every `public` member made `internal` — a fixed alteration
- 10 input(s): every `private` member made `public` — a fixed alteration
- 92 input(s): every auto-property made a field — a fixed alteration
- 26 input(s): every field made an auto-property — a fixed alteration
- 90 input(s): every setter made `init` — a fixed alteration
- 89 input(s): every setter made `private` — a fixed alteration
- 139 input(s): every class that can be made `sealed` — a fixed alteration
- 139 input(s): every class that can be made `abstract` — a fixed alteration
- 113 input(s): the type of every member made nullable — a fixed alteration
- 117 input(s): the type of every member made an array — a fixed alteration

A model's reading of what was meant (gemini-4-hydrogen-medium (Gemini, through the Antigravity command line)). This is an opinion and is marked as one wherever it appears. It was given the text of the requirement, supplied by the owner, the commit message(s) of the change, 6 line(s) of documentation the change added, 19 comment line(s) the change added to the code, and asked about the 1 group(s) of differences that the change's own text does not explain: 1 it reads as what the change set out to do, 0 as not meant, 0 as artefacts of the build or the run, 0 as unclear. A reading of "not meant" moves an item up inside its tier; no reading moves anything down.

The requirement, read against the tests. The reading is a model's and is an opinion. Two things in it were checked here: the words in the first column are in the requirement, and a test named in the third is one that fails on the old code.

| The requirement asks | The model's reading | Test that shows it | Why |
|---|---|---|---|
| Both `MessagePackGenerator` (CS8785) and `MsgPack00xMessagePackAnalyzer` (AD0001) throw `IndexOutOfRangeException` when a `[MessagePackObject]` type has a member whose type is a **closed generic value type defined in a referenced assembly**. | demonstrated | `GenericsFormatterTests.ClosedGenericStruct_FromMetadataReference` | The test compiles a generic struct to a metadata reference, uses a closed instantiation as a member in a separate compilation, and runs both the analyzer and generator expecting no diagnostics. |
| A clean build, with a generated resolver in `app`. | demonstrated | `GenericsFormatterTests.ClosedGenericRecordStruct_FromMetadataReference` | The test asserts zero diagnostics from the analyzer and generator and verifies that the expected generated resolver file (App.AppResolver.g.cs) is present in the generator output. |
| record struct Item<T> | demonstrated | `GenericsFormatterTests.ClosedGenericRecordStruct_FromMetadataReference` | The test reproduces the exact `record struct Item<T>` declaration from a metadata reference and fails on the old code. |
| struct Item<T> | demonstrated | `GenericsFormatterTests.ClosedGenericStruct_FromMetadataReference` | The test reproduces a generic `struct` (`MyGenericStruct<T>`) from a metadata reference used as a closed generic member and fails on the old code. |

The change undone one part at a time. With the old lines of a part back in the new code, the tests were run again:

| Part | Lines | Outcome |
|---|---|---|
| V1 | `src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs` 239–239 | needed: 1 test(s) fail without it, 0 of which existed before (e.g. `GenericsFormatterTests.ClosedGenericStruct_FromMetadataReference_UnkeyedMember`) |
| V2 | `src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs` 985–985 | **no test needs it**: every test passes with the old lines back |
| V3 | `src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs` 1077–1077 | needed: 3 test(s) fail without it, 0 of which existed before (e.g. `GenericsFormatterTests.ClosedGenericRecordStruct_FromMetadataReference`) |
| V4 | `src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs` 1429–1451 | does not compile without the rest of the change |

Tests written to break the change, by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line). What their writer says about them is not used: each file is built and run here, and a test that fails on the new code is run on the old code, which decides whether the change caused it:

| Attack | Unit | Tests | Outcome | Saved as |
|---|---|---:|---|---|
| A1 | `TypeCollector.TypeCollector(Compilation, AnalyzerOptions, ReferenceSymbols, ITypeSymbol, Action<Diagnostic>?, CancellationToken)` | 5 | fails-on-both: 4 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 1 other(s) pass | attacks/A1.cs |
| A2 | `TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)` | 5 | fails-on-both: 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass | attacks/A2.cs |
| A3 | `TypeCollector.ReportIfLocatedInCompilation(Action<Diagnostic>, Diagnostic)` | 4 | fails-on-both: 3 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 1 other(s) pass | attacks/A3.cs |
| A4 | `TypeCollector.GetPropertyIdentifierLocation(IPropertySymbol)` | 4 | fails-on-both: 4 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass | attacks/A4.cs |
| K1 | `TypeCollector.TypeCollector(Compilation, AnalyzerOptions, ReferenceSymbols, ITypeSymbol, Action<Diagnostic>?, CancellationToken)` | 4 | regression-demonstrated: 3 test(s) fail on the new code and pass on the old code: AyaK1Attack.CrossCompilationReference_WithBrokenGenericTypeMember_ReportsDiagnostic, AyaK1Attack.Collect_DirectlyOnBrokenClosedGenericTypeFromReferencedCompilation_ReportsDiagnostic, AyaK1Attack.InvalidGenericTypeInMetadataReference_UsedAsMember_ReportsDiagnostic | attacks/K1.cs |
| K2 | `TypeCollector.GetPropertyIdentifierLocation(IPropertySymbol)` | 4 | fails-on-both: 3 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 1 other(s) pass | attacks/K2.cs |

A1: `AyaA1Attack.InSource_InaccessibleMessagePackObject_ReportsDiagnostic` fails on the new code; on the old code: Failed.
```
Assert.NotEmpty() Failure: Collection was empty
```

A1: `AyaA1Attack.MetadataReference_UnserializableMemberType_ReportsDiagnostic` fails on the new code; on the old code: Failed.
```
Assert.NotEmpty() Failure: Collection was empty
```

A1: `AyaA1Attack.MetadataReference_UnserializableArrayMember_ReportsDiagnostic` fails on the new code; on the old code: Failed.
```
Assert.NotEmpty() Failure: Collection was empty
```

A1: `AyaA1Attack.InSource_MissingKeyAttribute_ReportsDiagnostic` fails on the new code; on the old code: Failed.
```
Assert.NotEmpty() Failure: Collection was empty
```

A2: `AyaA2Attack.NonPublicPropertyMissingKey_WhenNonPublicMemberIsSerialized_ReportsDiagnosticInSource` fails on the new code; on the old code: Failed.
```
Assert.Single() Failure: The collection did not contain any matching items
Expected:   (predicate expression)
Collection: []
```

A2: `AyaA2Attack.PublicFieldMissingKey_ReportsDiagnosticAtFieldDeclarator` fails on the new code; on the old code: Failed.
```
Assert.Single() Failure: The collection did not contain any matching items
Expected:   (predicate expression)
Collection: []
```

A2: `AyaA2Attack.PublicPropertyMissingKey_ReportsDiagnosticAtPropertyDeclaration` fails on the new code; on the old code: Failed.
```
Assert.Single() Failure: The collection did not contain any matching items
Expected:   (predicate expression)
Collection: []
```

A2: `AyaA2Attack.GenericObjectPropertyWithNullStringKey_ReportsDiagnosticAtPropertyIdentifier` fails on the new code; on the old code: Failed.
```
Assert.Single() Failure: The collection did not contain any matching items
Expected:   (predicate expression)
Collection: []
```

A2: `AyaA2Attack.PropertyWithNullStringKey_ReportsDiagnosticAtPropertyIdentifier` fails on the new code; on the old code: Failed.
```
Assert.Single() Failure: The collection did not contain any matching items
Expected:   (predicate expression)
Collection: []
```

A3: `AyaA3Attack.PositionalRecordStruct_MissingKeyAttribute_ReportsDiagnostic` fails on the new code; on the old code: Failed.
```
Assert.Contains() Failure: Filter not matched in collection
Collection: []
```

A3: `AyaA3Attack.PositionalRecord_MissingKeyAttribute_ReportsDiagnostic` fails on the new code; on the old code: Failed.
```
Assert.Contains() Failure: Filter not matched in collection
Collection: []
```

A3: `AyaA3Attack.UnannotatedMetadataType_AsMember_ReportsDiagnostic` fails on the new code; on the old code: Failed.
```
Assert.Contains() Failure: Filter not matched in collection
Collection: []
```

A4: `AyaA4Attack.PositionalRecord_MissingKeyAttribute_ReportsDiagnosticInSource` fails on the new code; on the old code: Failed.
```
Assert.NotEmpty() Failure: Collection was empty
```

A4: `AyaA4Attack.PositionalRecord_ConflictingKeyAndIgnoreMember_ReportsDiagnosticInSource` fails on the new code; on the old code: Failed.
```
Assert.NotEmpty() Failure: Collection was empty
```

A4: `AyaA4Attack.PositionalRecordStruct_DuplicateKeyAttribute_ReportsDiagnosticInSource` fails on the new code; on the old code: Failed.
```
Assert.Contains() Failure: Filter not matched in collection
Collection: [warning CS8785: Generator 'MessagePackGenerator' failed to generate source. It will not contribute to the output and compilation errors may occur as a result. Exception was of type 'ArgumentException' with message 'An item with the same key has already been added. Key: 0'.
System.ArgumentException: An item with the same key has already been added. Key: 0
   at System.Collections.Generic.Dictionary`2.TryInsert(TKey key, T…
```

A4: `AyaA4Attack.PositionalRecord_UnserializablePropertyType_ReportsDiagnosticInSource` fails on the new code; on the old code: Failed.
```
Assert.NotEmpty() Failure: Collection was empty
```

K1: `AyaK1Attack.CrossCompilationReference_WithBrokenGenericTypeMember_ReportsDiagnostic` fails on the new code; on the old code: Passed.
```
Assert.NotEmpty() Failure: Collection was empty
```

K1: `AyaK1Attack.Collect_DirectlyOnBrokenClosedGenericTypeFromReferencedCompilation_ReportsDiagnostic` fails on the new code; on the old code: Passed.
```
Assert.NotEmpty() Failure: Collection was empty
```

K1: `AyaK1Attack.InvalidGenericTypeInMetadataReference_UsedAsMember_ReportsDiagnostic` fails on the new code; on the old code: Passed.
```
Assert.NotEmpty() Failure: Collection was empty
```

K2: `AyaK2Attack.PartialProperty_MissingKey_ReportsDiagnosticAtSourceLocation` fails on the new code; on the old code: Failed.
```
Assert.Single() Failure: The collection did not contain any matching items
Expected:   (predicate expression)
Collection: []
```

K2: `AyaK2Attack.PositionalRecordStruct_PublicPropertyWithoutKey_ReportsDiagnosticAtSourceLocation` fails on the new code; on the old code: Failed.
```
Assert.Single() Failure: The collection did not contain any matching items
Expected:   (predicate expression)
Collection: []
```

K2: `AyaK2Attack.PositionalRecord_PublicPropertyWithoutKey_ReportsDiagnosticAtSourceLocation` fails on the new code; on the old code: Failed.
```
Assert.Single() Failure: The collection did not contain any matching items
Expected:   (predicate expression)
Collection: []
```

Exhaustive mode. Every member that depends on the change and has executable code is run by at least one original test, so there was nothing to probe.

## 5. What was not checked

- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs:1446 — altering `diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)` (&& → ||) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs:1446 — altering `diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)` (a && b → a) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs:1446 — altering `diagnostic.Location.IsInSource && this.compilation.ContainsSyntaxTree(diagnostic.Location.SourceTree)` (a && b → b) makes no test fail. The line runs, but nothing checks what it does.
- **Documentation.** 2 file(s), 10 lines: no tool checked that this text is accurate (README.md, doc/analyzers/MsgPack004.md)

## 6. What AYA refuses to decide

- **Requirement.** Whether this change does what requirement MessagePack-CSharp#2283 asks. AYA measured what the code does under test; it has not interpreted the requirement.
- **Side effects.** Whether behaviour changes in already-existing members are intended. They are reachable from code the requirement does not mention.

## 7. Limits of this analysis

- Executed is not the same as correct: a covered line ran under a passing test, nothing more.
- Coverage does not separate pre-existing tests from tests written in the same change.
- Static reachability over-approximates: referring to a type is treated as possibly calling its constructors and framework entry points.
- Calls made through reflection, generated code or analyzers loaded by the compiler are not edges in the graph.
- Deleted code has no unit in a graph of the new code. It is listed as not analysed; what used it was not looked for.
- Old and new code are compared only on the inputs a probe chose: no difference found on them is not the same behaviour.
- The order of the list is a fixed rule over what was measured. It ranks where a mistake would matter and go unseen, not how probable a mistake is.

## 8. Sign-off

This page is advice with evidence. It is not an approval.

| | |
|---|---|
| Owner (named person) | |
| Decision | accept / reject / request more evidence |
| Commit | `9df83c38409c8bfad0517aebffe48211eda4a5b4` |
| Evidence | sha256 `6ac58bc7329e91d7454420b7951b03ab51ea7a1b52aa3373f5bb28ba550198eb` |
| Looked at by the owner | section 1, down to item no. ___ |
| Residual risk accepted | the items in sections 5 and 6 |
| Date | |
