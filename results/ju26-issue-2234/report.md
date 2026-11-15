# Review evidence — ju26-issue-2234

| | |
|---|---|
| Requirement | MessagePack-CSharp#2234 |
| Change | `9007cee1` Implementation for #2234 The analyzer does not work if SuppressSourceGeneration = true |
| Written by | Stelian Vultur, 2026-11-12T01:00:52+02:00 |
| Compared with | `32258fdb` |
| Built with | .NET SDK 9.0.321 (the repository selects 8.0.428) |
| Produced by | aya 0.1.0, 2026-11-15T02:06:00Z |
| Evidence file | `evidence.json`, sha256 `dfd37014a84d1498b78b3bc9959f407bd1e848755eaf352a63b6b65fcaac0d71` |

Every statement below comes from a tool run, and its source is named. Where a model wrote a probe or a test, what is stated is what running it showed, never what the model said.

## 1. Where to look, most deserving first

Everything the change touches is in this list; nothing was left out of it. The order says where a mistake is most likely to matter and least likely to be noticed. The last tier means no signal was found, not that the code is right. How far down to go is the owner's decision.

| # | Tier | What | Why it is here |
|---:|---|---|---|
| 1 | 1. A check that passed on the old code fails on the new | TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?) | a test written to break it fails on the new code and passes on the old (attacks/A1.cs) |
| 2 | 2. Changes a result that existed before | The change as a whole, over 2542 inputs nobody chose | 11 input(s) give a different result than on the old code, and the change's own text does not explain it |
| 3 | 4. Could not be measured | The requirement: 1 of the 2 thing(s) it asks for are not shown by any test | a model reads "The analyzer does not work if SuppressSourceGeneration = true." as contradicted: The change also alters analyzer behavior for generated MessagePackObject types without SuppressSourceGeneration = true, which goes beyond what the requirement asks for. (its opinion) |
| 4 | 5. A mistake here would go unnoticed | 6 of the 10 tests that came with the change | they were not shown to fail without the change, so they do not demonstrate it |
| 5 | 6. No signal found | README.md | documentation: no tool checks that the text is accurate, and it cannot break the code |

Rule used. Tier 1: an existing test now fails because of it, an input that gave a result now fails, or a test written to break it fails on the new code and passes on the old. Tier 2: an input gives a different result than on the old code. Tier 3: it modifies code that existed, other code depends on it, and a line is never executed or can be broken without any test failing. Tier 4: the analysis could not measure it (deleted code, modified declarations, no break could be tried, generated or conditionally compiled code, a failing test nothing can arbitrate). Tier 5: a line is never executed or can be broken unnoticed, in code nothing else depends on or that is new; or tests that came with the change were not shown to fail without it. Tier 6: none of the above was found. Inside a tier, a difference the change's own text does not explain, or that a model reads as not meant, comes before one made only of lines the change adds or removes; then the unit with more code depending on it comes first. A model's reading can only move an item up.

### 1. TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)

`src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs`, changed lines 1008, 1023–1026, 1039–1042, 1148, 1163–1166, 1179–1182

- **Why it is here**
  - a test written to break it fails on the new code and passes on the old (attacks/A1.cs)
  - a test written to break it fails on the new code and passes on the old (attacks/K1.cs, kept from the run of 2026-11-14 and run again now)
  - a test written to break it fails on the new code and passes on the old (attacks/K2.cs, kept from the run of 2026-11-15 and run again now)
  - a test written to break it fails on the new code and passes on the old (attacks/K3.cs, kept from the run of 2026-11-15 and run again now)
  - 11 input(s) give a different result than on the old code, and the change's own text does not explain it
  - a model reads 11 of those differences as not what the change set out to do (its opinion, not a measurement)
- **How likely it is to be wrong (indications, not a measurement)**
  - modifies code that already existed (18 line(s))
  - the changed lines touch a condition
  - old and new code differ on 11 of 14 input(s): 11 changed (probes/P1.cs)
- **How much breaks if it is wrong**
  - 29 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as TypeCollector.Collect(), TypeCollector.Collect(Compilation, AnalyzerOptions, ReferenceSymbols, Action<Diagnostic>?, ITypeSymbol, CancellationToken)
  - breaking line 1023 made 45 test(s) fail (measured)
  - 0 of the 29 dependents with executable code are run by no test, so a break reaching them would not be seen
- **Whether anything would notice**
  - 13 of 18 changed executable line(s) ran under the tests; never ran: 1148, 1179–1182
  - 7 of 7 deliberate break(s) made a test fail
  - 1 of those were noticed only by tests that came with the change, none by a test that existed before
  - with lines 1008–1008 put back as they were, 1 test(s) fail, all of them tests that came with the change
  - with lines 1023–1026 put back as they were, 2 test(s) fail, all of them tests that came with the change
  - with lines 1039–1042 put back as they were, 1 test(s) fail, all of them tests that came with the change
  - lines 1148–1148 can be put back as they were and every test still passes: no test needs this part of the change
  - lines 1163–1166 can be put back as they were and every test still passes: no test needs this part of the change
  - lines 1179–1182 can be put back as they were and every test still passes: no test needs this part of the change
  - 3 possible break(s) were not tried (budget, or on a line no test executes)
- **For the owner to check**
  - Something that held on the old code no longer does. Decide whether that was meant to change; if not, the change is wrong.
  - Go through the inputs whose result changed and confirm every change is one the requirement asks for.
  - Lines 1148–1148: with the old code back, no test fails. Find out what this part of the change is for, and whether the requirement asks for it.
  - Lines 1163–1166: with the old code back, no test fails. Find out what this part of the change is for, and whether the requirement asks for it.
  - Lines 1179–1182: with the old code back, no test fails. Find out what this part of the change is for, and whether the requirement asks for it.
  - Lines 1148, 1179–1182 never run under any test: read them, or have them exercised.

### 2. The change as a whole, over 2542 inputs nobody chose

`tests/MessagePack.SourceGenerator.Tests/MessagePack.SourceGenerator.Tests.csproj`

- **Why it is here**
  - 11 input(s) give a different result than on the old code, and the change's own text does not explain it
  - a model reads 12 of the differences as not what the change set out to do (its opinion, not a measurement)
  - on 1 input(s) the code under test crashes on the old and on the new code alike (for example i036+a5 GenerationTests.cs:905 + an attribute argument made null)
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
  - 2493 of 2542 give the same result on old and new code; 49 differ
- **How much breaks if it is wrong**
  - an input here is a whole program handed to the changed code, as a user of the library would write it
- **Whether anything would notice**
  - the inputs drove 18 of the change's 18 executable line(s), measured on the new code
- **What the analysis cannot see**
  - the harness that feeds the inputs and records the results was written by a model; it was accepted because it runs on both versions, gives the same results twice on the old code and executes the change, not because it was read
- **For the owner to check**
  - Go through the groups of differences in the report and confirm each is one the requirement asks for.

### 3. The requirement: 1 of the 2 thing(s) it asks for are not shown by any test

- **Why it is here**
  - a model reads "The analyzer does not work if SuppressSourceGeneration = true." as contradicted: The change also alters analyzer behavior for generated MessagePackObject types without SuppressSourceGeneration = true, which goes beyond what the requirement asks for. (its opinion)
- **How likely it is to be wrong (indications, not a measurement)**
  - 1 thing(s) the requirement asks for are shown by a test that fails without the change (the model named the test; that it fails on the old code was measured)
- **What the analysis cannot see**
  - splitting the requirement into things asked for is a model's reading; the words are checked to be in the requirement, their meaning is not
- **For the owner to check**
  - Read the requirement against this list. For each thing no test shows, decide whether the change does it.

### 4. 6 of the 10 tests that came with the change

- **Why it is here**
  - they were not shown to fail without the change, so they do not demonstrate it
- **Whether anything would notice**
  - 6 pass on the old code too: SuppressSourceGenerationAnalyzerTests.AllowPrivateRequiredIsReported, SuppressSourceGenerationAnalyzerTests.MemberTypeMustBeMessagePackObjectIsReported, SuppressSourceGenerationAnalyzerTests.MemberWithoutKeyIsReported, SuppressSourceGenerationAnalyzerTests.SourceGenerationOnlyDiagnosticsAreNotReportedWhenSuppressed, …
- **For the owner to check**
  - Read these tests: what they assert may hold whether or not the change is right.

## 2. What changed

| Role | Files | Lines added | Lines removed |
|---|---:|---:|---:|
| test | 2 | 230 | 0 |
| docs | 1 | 14 | 0 |
| source | 1 | 18 | 4 |

## 3. Changed symbols, from the bottom of the dependency graph up

Graph: 6335 nodes, 19693 edges, 37 layers, over 18 projects. Layer 0 depends on nothing else in the code; a higher layer depends on lower ones.

| Layer | Symbol | Change | Public API | Dependents | Tests that can reach it | Changed lines run / not run | Measured |
|---:|---|---|---|---:|---:|---|---|
| 13 | `TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)` | modified | no | 29 | 138 | 13 / 5 | partly-executed |

Plus 16 test methods and test helpers added or modified by the change (section 4 says what they prove).

## 4. What was checked

| ID | Result | Finding | Checked by |
|---|---|---|---|
| E1 | info | 4 changed files fall into 4 groups of similar edits | git diff; files grouped by the distinct lines they edit, then split by exact edit (deterministic) |
| E2 | info | 1 non-test symbols changed across layers 13–13; the most depended-on has 29 dependent member(s); 0 public API symbols touched | Roslyn semantic model over 18 projects (6335 nodes, 19693 edges) |
| E3 | info | Old and new code were both built with .NET SDK 9.0.321, not the 8.0.428 the repository selects, because its source generator needs a newer compiler | dotnet --version; compiler warning CS9057 |
| E4 | pass | 6 test project(s) and their dependencies compile with the change | dotnet build (new code) |
| E5 | pass | 1254 of 1260 pre-existing tests pass on both old and new code; 0 regressed | dotnet test on old and new code, compared test by test |
| E6 | warn | 44 of those tests are fed random data, so their inputs differ on every run; they were compared per test method, not input by input | dotnet test on old and new code |
| E7 | pass | 10 new and 0 modified tests; 10 pass on the new code, 0 fail | dotnet test (new code) |
| E8 | warn | 4 of 10 of those tests fail on the old code (they demonstrate the change); 6 pass without it; 0 cannot compile without it | dotnet test on old source with the change's test files applied |
| E9 | warn | 13 of 18 changed executable source lines were executed by the test suite; 5 were never executed | Microsoft Code Coverage during dotnet test (new code) |
| E10 | warn | 7 of 7 deliberate breaks of changed lines were caught by a test; 0 went unnoticed; 3 more are on lines no test executes | each changed line altered in turn, then dotnet test (mutation testing) |
| E11 | warn | 6 part(s) of the change were put back as they were, one at a time: 3 are needed by at least one test (3 only by tests that came with the change), 3 can be undone with every test still passing | the old lines of one part restored in the new code, then dotnet test |
| E12 | info | 1 of 1 tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) were accepted: they compile, pass, and now execute 5 previously unexecuted line(s) and catch 0 of the 0 unnoticed break(s) | gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) wrote them; dotnet test and coverage checked each one, and each accepted test was run against every other unnoticed break |
| E13 | warn | 1 of 1 modified unit(s) were run on the old and the new code with the same 14 input(s): 3 give the same result, 11 differ (11 changed) | probes written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line); the same probe run twice on the old code and once on the new |
| E14 | fail | 1 unit(s) were attacked with tests written to break them: 0 resisted (0 tests), 1 with a demonstrated regression, 0 with a failing test nothing can arbitrate, 0 failing on the old code too. 3 test(s) kept from earlier runs because they showed a regression were run again: 3 still fail on the new code and pass on the old | tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line); each file is built and run here, and a failing test is run on the old code, which decides whether the change caused it |
| E15 | warn | 2542 input(s) nobody chose (151 taken from the existing tests, 2391 altered mechanically) were run on the old and the new code: 2493 give the same result, 49 differ (1 fails-differently, 38 now-works, 10 changed); 49 difference(s) show only once the input is altered. The inputs drove 18 of the change's 18 executable line(s). 156 input(s) that came with the change were run on the new code alone: 0 make the code under test crash, 0 get output that does not compile with no error reported. on 1 input(s) the code under test crashes on the old and on the new code alike (for example i036+a5 GenerationTests.cs:905 + an attribute argument made null) | inputs built by AYA from the tests of tests/MessagePack.SourceGenerator.Tests/MessagePack.SourceGenerator.Tests.csproj, with no model involved; the harness that feeds them was written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) and run twice on the old code and once on the new |
| E16 | info | OPINION, not a measurement. gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) was asked whether 17 group(s) of differences the change's own text does not explain were meant: it reads 0 as what the change set out to do, 17 as not meant, 0 as artefacts of the build or the run and 0 as unclear | 2 session(s); it was given the text of the requirement, supplied by the owner, the commit message(s) of the change, 10 line(s) of documentation the change added, 9 comment line(s) the change added to the code. A reading moves nothing down the list |
| E17 | warn | OPINION, bounded by measurements. A model read 2 thing(s) the requirement asks for: 1 are shown by a test that fails without the change, 0 the change appears to do with no such test, 0 it does not address, and in 1 the change does less than, or something other than, what is asked | the words of each thing asked for were checked to be in the requirement, and a test named as showing one was checked to be a test that fails on the old code; the reading itself is the model's |
| E18 | info | 0 member(s) depend on the change and are run by no original test; 0 were probed within a budget of 6 session(s) and 0 could be compared on old and new code | probes written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line); 0 session(s) used |

E8 detail:
```
Pass without the change:
SuppressSourceGenerationAnalyzerTests.AllowPrivateRequiredIsReported
SuppressSourceGenerationAnalyzerTests.MemberTypeMustBeMessagePackObjectIsReported
SuppressSourceGenerationAnalyzerTests.MemberWithoutKeyIsReported
SuppressSourceGenerationAnalyzerTests.SourceGenerationOnlyDiagnosticsAreNotReportedWhenSuppressed
SuppressedSourceGenerationTests.NoFormatterIsGeneratedForSuppressedType
SuppressedSourceGenerationTests.SuppressedTypeRoundTripsThroughDynamicResolver
```

E11 detail:
```
src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs:1148–1148  no test fails with the old lines back
src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs:1163–1166  no test fails with the old lines back
src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs:1179–1182  no test fails with the old lines back
```

E14 detail:
```
A1 TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?): regression-demonstrated — 4 test(s) fail on the new code and pass on the old code: AyaA1Attack.MixedStringThenIntKeyOnPropertyStillReportsGenericElementTypeMustBeMessagePackObject, AyaA1Attack.MixedStringThenIntKeyOnStructPropertyStillReportsMemberTypeMustBeMessagePackObject, AyaA1Attack.MixedStringThenIntKeyOnSuppressedObjectPropertyStillReportsMemberTypeMustBeMessagePackObject, AyaA1Attack.MixedStringThenIntKeyOnPropertyStillReportsMemberTypeMustBeMessagePackObject
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

E17 detail:
```
contradicted: "The analyzer does not work if SuppressSourceGeneration = true." — The change also alters analyzer behavior for generated MessagePackObject types without SuppressSourceGeneration = true, which goes beyond what the requirement asks for.
```

Tests that came with the change, run against the old code:

| Test | Origin | On new code | On old code | Meaning |
|---|---|---|---|---|
| `SuppressSourceGenerationAnalyzerTests.AllowPrivateRequiredIsReported` | new | Passed | Passed | passes without the change: it proves nothing about it |
| `SuppressSourceGenerationAnalyzerTests.DuplicateIntKeysAreReportedWithoutAnalyzerFailure` | new | Passed | Failed | fails without the change: it demonstrates it |
| `SuppressSourceGenerationAnalyzerTests.DuplicateKeysAreReportedWithoutAnalyzerFailureForGeneratedTypes` | new | Passed | Failed | fails without the change: it demonstrates it |
| `SuppressSourceGenerationAnalyzerTests.DuplicateStringKeysAreReportedWithoutAnalyzerFailure` | new | Passed | Failed | fails without the change: it demonstrates it |
| `SuppressSourceGenerationAnalyzerTests.MemberTypeMustBeMessagePackObjectIsReported` | new | Passed | Passed | passes without the change: it proves nothing about it |
| `SuppressSourceGenerationAnalyzerTests.MemberWithoutKeyIsReported` | new | Passed | Passed | passes without the change: it proves nothing about it |
| `SuppressSourceGenerationAnalyzerTests.MixedKeyKindsAreReportedWithoutAnalyzerFailure` | new | Passed | Failed | fails without the change: it demonstrates it |
| `SuppressSourceGenerationAnalyzerTests.SourceGenerationOnlyDiagnosticsAreNotReportedWhenSuppressed` | new | Passed | Passed | passes without the change: it proves nothing about it |
| `SuppressedSourceGenerationTests.NoFormatterIsGeneratedForSuppressedType` | new | Passed | Passed | passes without the change: it proves nothing about it |
| `SuppressedSourceGenerationTests.SuppressedTypeRoundTripsThroughDynamicResolver` | new | Passed | Passed | passes without the change: it proves nothing about it |

Test runs:

| Code under test | Project | Compiled | Passed | Failed | Skipped or not run | Seconds | Problem |
|---|---|---|---:|---:|---:|---:|---|
| new | MessagePack.AspNetCoreMvcFormatter.Tests | yes | 6 | 0 | 0 | 22.4 |  |
| new | MessagePack.Experimental.Tests | yes | 95 | 0 | 0 | 10.8 |  |
| new | MessagePack.GeneratedCode.Tests | yes | 11 | 0 | 0 | 13.2 |  |
| new | MessagePack.SourceGenerator.ExecutionTests | yes | 16 | 0 | 0 | 11.4 |  |
| new | MessagePack.SourceGenerator.Tests | yes | 175 | 0 | 0 | 25.6 |  |
| new | MessagePack.Tests | yes | 961 | 0 | 6 | 22.9 |  |
| old | MessagePack.AspNetCoreMvcFormatter.Tests | yes | 6 | 0 | 0 | 11.2 |  |
| old | MessagePack.Experimental.Tests | yes | 95 | 0 | 0 | 8.2 |  |
| old | MessagePack.GeneratedCode.Tests | yes | 11 | 0 | 0 | 7.5 |  |
| old | MessagePack.SourceGenerator.ExecutionTests | yes | 14 | 0 | 0 | 6.1 |  |
| old | MessagePack.SourceGenerator.Tests | yes | 167 | 0 | 0 | 20.2 |  |
| old | MessagePack.Tests | yes | 961 | 0 | 6 | 16 |  |
| old + new tests | MessagePack.SourceGenerator.Tests | yes | 4 | 4 | 0 | 9.2 |  |
| old + new tests | MessagePack.SourceGenerator.ExecutionTests | yes | 2 | 0 | 0 | 7.4 |  |

Deliberate breaks of the changed lines. Each row alters one line and re-runs the tests that execute it:

| Break | Where | Alteration | Original code | Result |
|---|---|---|---|---|
| M6 | TypeCollector.cs:1148 | continue removed | `continue;` | no test executes this line |
| M9 | TypeCollector.cs:1179 | !x → x | `!stringMembers.ContainsKey(member.StringKey)` | no test executes this line |
| M10 | TypeCollector.cs:1181 | statement removed | `stringMembers.Add(member.StringKey, (member, item.Type));` | no test executes this line |
| M1 | TypeCollector.cs:1008 | continue removed | `continue;` | caught by `SuppressSourceGenerationAnalyzerTests.MixedKeyKindsAreReportedWithoutAnalyzerFailure`; only by tests that came with the change |
| M2 | TypeCollector.cs:1023 | !x → x | `!intMembers.ContainsKey(member.IntKey)` | caught by `GenerationTests.AdditionalAllowTypes` and 44 more |
| M3 | TypeCollector.cs:1025 | statement removed | `intMembers.Add(member.IntKey, (member, item.Type));` | caught by `GenerationTests.AdditionalAllowTypes` and 44 more |
| M4 | TypeCollector.cs:1039 | !x → x | `!stringMembers.ContainsKey(member.StringKey)` | caught by `CustomStringKeyFormatterTests.RecordWithPrimaryConstructor` and 2 more |
| M5 | TypeCollector.cs:1041 | statement removed | `stringMembers.Add(member.StringKey, (member, item.Type));` | caught by `CustomStringKeyFormatterTests.RecordWithPrimaryConstructor` and 2 more |
| M7 | TypeCollector.cs:1163 | !x → x | `!intMembers.ContainsKey(member.IntKey)` | caught by `GenerationTests.CustomFormatterForGenericWithConstraints` and 10 more |
| M8 | TypeCollector.cs:1165 | statement removed | `intMembers.Add(member.IntKey, (member, item.Type));` | caught by `GenerationTests.CustomFormatterForGenericWithConstraints` and 10 more |

Tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) to fill the gaps. None is trusted on the model's word; each was built and run:

| Request | Gap | Target | Tries | Result | Saved as |
|---|---|---|---:|---|---|
| R1 | never executed | `TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)` line(s) 1148, 1179–1182 | 1 | **accepted**: passes and executes line(s) 1148, 1179–1182 | proposed-tests/R1.cs |

Old code against new code. A probe written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) records what the code does on a set of inputs; it is run twice on the old code, to discard what is unstable, and once on the new:

| Probe | Unit | Inputs compared | Same | Differ | Unstable | Tries | Outcome | Saved as |
|---|---|---:|---:|---:|---:|---:|---|---|
| P1 | `TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)` | 14 | 3 | 11 | 0 | 1 | 11 of 14 case(s) differ: 11 changed | probes/P1.cs |
| S1 | `the change as a whole, over 2542 inputs built from the existing tests` | 2542 | 2493 | 49 | 0 | 2 | 49 of 2542 case(s) differ: 1 fails-differently, 38 now-works, 10 changed | probes/S1.cs |

P1: inputs whose result differs, the same alteration grouped together:

- **gave a result, now gives another** (**not explained by the change's own text**) in 4 input(s): duplicate int keys on fields; duplicate int keys on properties; struct with duplicate int keys; suppressed source generation with duplicate int keys
  ```
  - AD0001 (1,1): Analyzer 'MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer' threw an exception of type 'System.ArgumentException' with message 'An item with the same key has already…
  ```
  Model's reading, not a measurement: not-intended. The requirement only asks to make the analyzer work when SuppressSourceGeneration = true, not to fix ArgumentException crashes on duplicate int keys. It quotes: "if (!intMembers.ContainsKey(member.IntKey))"
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): duplicate string keys on fields
  ```
  - AD0001 (1,1): Analyzer 'MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer' threw an exception of type 'System.ArgumentException' with message 'An item with the same key has already…
  ```
  Model's reading, not a measurement: not-intended. The requirement only asks to make the analyzer work when SuppressSourceGeneration = true, not to fix ArgumentException crashes on duplicate string keys on fields. It quotes: "if (!stringMembers.ContainsKey(member.StringKey))"
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): duplicate string keys on properties
  ```
  - AD0001 (1,1): Analyzer 'MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer' threw an exception of type 'System.ArgumentException' with message 'An item with the same key has already…
  ```
  Model's reading, not a measurement: not-intended. The requirement only asks to make the analyzer work when SuppressSourceGeneration = true, not to fix ArgumentException crashes on duplicate string keys on properties. It quotes: "if (!stringMembers.ContainsKey(member.StringKey))"
- **gave a result, now gives another** (**not explained by the change's own text**) in 4 input(s): mixed int then string keys on fields; mixed int then string keys on properties; mixed property int key and field string key; suppressed source generation with mixed keys
  ```
  - AD0001 (1,1): Analyzer 'MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer' threw an exception of type 'System.InvalidOperationException' with message 'Nullable object must have a v…
  ```
  Model's reading, not a measurement: not-intended. The requirement only asks to make the analyzer work when SuppressSourceGeneration = true, not to fix InvalidOperationException crashes when mixing int and string keys. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): mixed string then int keys on fields
  ```
  - AD0001 (1,1): Analyzer 'MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer' threw an exception of type 'System.ArgumentNullException' with message 'Value cannot be null. (Parameter …
  ```
  Model's reading, not a measurement: not-intended. The requirement only asks to make the analyzer work when SuppressSourceGeneration = true, not to fix ArgumentNullException crashes when mixing string and int keys. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."

S1: inputs whose result differs, the same alteration grouped together:

- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): i006+a5 GenerationTests.cs:47 + an attribute argument made null
  ```
  - OUT Formatters.MessagePack.GeneratedMessagePackResolver.NS2.MyEnumFormatter.g.cs
  - internal partial class NS2 {
  - 	internal sealed class MyEnumFormatter : MsgPack::Formatters.IMessagePackFormatter<global::NS2.MyEnum>
  - 		public void Serialize(ref MsgPack::MessagePackWriter writer, global::NS2.MyEnum value, MsgPack::MessagePackSerializerOptions options)
  - 		public global::NS2.MyEnum Deserialize(ref MsgPack::MessagePackReader reader, MsgPack::MessagePackSerializerOptions options)
  - 			return (global::NS2.MyEnum)reader.ReadInt32();
  - 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(3)
  - 			{ typeof(global::NS2.MyEnum), 1 },
  - 			{ typeof(global::MyMessagePackObject), 2 },
  - 					case 1: return new global::MessagePack.GeneratedMessagePackResolver.NS2.MyEnumFormatter();
  - 					case 2: return new global::MessagePack.GeneratedMessagePackResolver.MyMessagePackObjectFormatter();
  + 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(2)
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (GenerationTests.cs:47), altered: an attribute argument made null.
  Model's reading, not a measurement: not-intended. Skipping the rest of the member loop on a null/mismatched key drops member types (such as NS2.MyEnum) from formatter generation and closedTypeLookup, which is unrelated to fixing the analyzer for SuppressSourceGeneration = true. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): i007+a5 GenerationTests.cs:81 + an attribute argument made null
  ```
  - 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(3)
  - 			{ typeof(global::NS2.MyType<int>), 1 },
  - 			{ typeof(global::MyMessagePackObject), 2 },
  - 					case 1: return new global::MessagePack.GeneratedMessagePackResolver.NS2.MyTypeFormatter<int>();
  - 					case 2: return new global::MessagePack.GeneratedMessagePackResolver.MyMessagePackObjectFormatter();
  + 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(2)
  + 			{ typeof(global::MyMessagePackObject), 1 },
  + 					case 1: return new global::MessagePack.GeneratedMessagePackResolver.MyMessagePackObjectFormatter();
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (GenerationTests.cs:81), altered: an attribute argument made null.
  Model's reading, not a measurement: not-intended. Skipping the rest of the member loop on a null/mismatched key drops NS2.MyType<int> from closedTypeLookup, which is an unrequested side effect outside the stated SuppressSourceGeneration fix. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): i009+a5 GenerationTests.cs:157 + an attribute argument made null
  ```
  - 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(4)
  - 			{ typeof(global::NS3.MyType<int>), 0 },
  - 			{ typeof(global::NS1.MyType), 1 },
  - 			{ typeof(global::MyMessagePackObject), 2 },
  - 			{ typeof(global::NS2.MyType), 3 },
  - 					case 0: return new global::MessagePack.GeneratedMessagePackResolver.NS3.MyTypeFormatter<int>();
  - 					case 1: return new global::MessagePack.GeneratedMessagePackResolver.NS1.MyTypeFormatter();
  - 					case 2: return new global::MessagePack.GeneratedMessagePackResolver.MyMessagePackObjectFormatter();
  - 					case 3: return new global::MessagePack.GeneratedMessagePackResolver.NS2.MyTypeFormatter();
  + 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(3)
  + 			{ typeof(global::NS1.MyType), 0 },
  + 			{ typeof(global::MyMessagePackObject), 1 },
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (GenerationTests.cs:157), altered: an attribute argument made null.
  Model's reading, not a measurement: not-intended. Skipping the rest of the member loop on a null/mismatched key drops NS3.MyType<int> from closedTypeLookup, which is an unrequested side effect outside the stated SuppressSourceGeneration fix. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): i013+a5 GenerationTests.cs:303 + an attribute argument made null
  ```
  - 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(5)
  - 			{ typeof(global::System.Collections.Generic.List<global::SubObject>[]), 1 },
  - 			{ typeof(global::System.Collections.Generic.List<global::SubObject>), 2 },
  - 			{ typeof(global::ContainerObject), 3 },
  - 			{ typeof(global::SubObject), 4 },
  - 					case 1: return new MsgPack::Formatters.ArrayFormatter<global::System.Collections.Generic.List<global::SubObject>>();
  - 					case 2: return new MsgPack::Formatters.ListFormatter<global::SubObject>();
  - 					case 3: return new global::MessagePack.GeneratedMessagePackResolver.ContainerObjectFormatter();
  - 					case 4: return new global::MessagePack.GeneratedMessagePackResolver.SubObjectFormatter();
  + 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(3)
  + 			{ typeof(global::ContainerObject), 1 },
  + 			{ typeof(global::SubObject), 2 },
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (GenerationTests.cs:303), altered: an attribute argument made null.
  Model's reading, not a measurement: not-intended. Skipping the rest of the member loop on a null/mismatched key drops List<SubObject>[] and List<SubObject> from closedTypeLookup, which is not asked for by the SuppressSourceGeneration requirement. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): i040+a5 GenerationTests.cs:1126 + an attribute argument made null
  ```
  - 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(4)
  - 			{ typeof(global::System.ReadOnlyMemory<int>), 1 },
  - 			{ typeof(global::System.Buffers.ReadOnlySequence<int>), 2 },
  - 			{ typeof(global::A), 3 },
  - 					case 1: return new MsgPack::Formatters.ReadOnlyMemoryFormatter<int>();
  - 					case 2: return new MsgPack::Formatters.ReadOnlySequenceFormatter<int>();
  - 					case 3: return new global::MessagePack.GeneratedMessagePackResolver.AFormatter();
  + 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(2)
  + 			{ typeof(global::A), 1 },
  + 					case 1: return new global::MessagePack.GeneratedMessagePackResolver.AFormatter();
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (GenerationTests.cs:1126), altered: an attribute argument made null.
  Model's reading, not a measurement: not-intended. Skipping the rest of the member loop on a null/mismatched key drops ReadOnlyMemory<int> and ReadOnlySequence<int> from closedTypeLookup, which is not asked for by the SuppressSourceGeneration requirement. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): i041+a5 GenericsFormatterTests.cs:19 + an attribute argument made null
  ```
  - OUT Formatters.MessagePack.GeneratedMessagePackResolver.TempProject.MyEnumFormatter.g.cs
  - 	internal sealed class MyEnumFormatter : MsgPack::Formatters.IMessagePackFormatter<global::TempProject.MyEnum>
  - 		public void Serialize(ref MsgPack::MessagePackWriter writer, global::TempProject.MyEnum value, MsgPack::MessagePackSerializerOptions options)
  - 			writer.Write((int)value);
  - 		public global::TempProject.MyEnum Deserialize(ref MsgPack::MessagePackReader reader, MsgPack::MessagePackSerializerOptions options)
  - 			return (global::TempProject.MyEnum)reader.ReadInt32();
  - 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(5)
  - 			{ typeof((int, long)?), 0 },
  - 			{ typeof(global::TempProject.MyEnum?), 1 },
  - 			{ typeof((int, long)), 2 },
  - 			{ typeof(global::TempProject.MyEnum), 3 },
  - 			{ typeof(global::TempProject.MyObject), 4 },
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (GenericsFormatterTests.cs:19), altered: an attribute argument made null.
  Model's reading, not a measurement: not-intended. Skipping the rest of the member loop on a null/mismatched key drops member types and MyEnumFormatter from generation and closedTypeLookup, which is unrelated to the SuppressSourceGeneration requirement. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): i042+a5 GenericsFormatterTests.cs:49 + an attribute argument made null
  ```
  - 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(3)
  - 			{ typeof((int, string, long)), 0 },
  - 			{ typeof(global::System.Collections.Generic.List<global::System.Collections.Generic.List<int>>), 1 },
  - 			{ typeof(global::TempProject.MyObject), 2 },
  - 					case 0: return new MsgPack::Formatters.ValueTupleFormatter<int, string, long>();
  - 					case 1: return new MsgPack::Formatters.ListFormatter<global::System.Collections.Generic.List<int>>();
  - 					case 2: return new global::MessagePack.GeneratedMessagePackResolver.TempProject.MyObjectFormatter();
  + 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(1)
  + 			{ typeof(global::TempProject.MyObject), 0 },
  + 					case 0: return new global::MessagePack.GeneratedMessagePackResolver.TempProject.MyObjectFormatter();
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (GenericsFormatterTests.cs:49), altered: an attribute argument made null.
  Model's reading, not a measurement: not-intended. Skipping the rest of the member loop on a null/mismatched key drops the tuple and nested list types from closedTypeLookup, which is not asked for by the SuppressSourceGeneration requirement. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): i044+a5 GenericsFormatterTests.cs:98 + an attribute argument made null
  ```
  - 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(12)
  - 			{ typeof(global::TempProject.MyGenericObject<int[]>), 3 },
  - 			{ typeof(global::TempProject.MyGenericObject<string>), 4 },
  - 			{ typeof(global::TempProject.MyGenericObject<global::System.Collections.Generic.IEnumerable<global::System.Guid>>), 5 },
  - 			{ typeof(global::TempProject.MyInnerGenericObject<int[]>), 6 },
  - 			{ typeof(global::TempProject.MyInnerGenericObject<string>), 7 },
  - 			{ typeof(global::TempProject.MyInnerGenericObject<global::System.Collections.Generic.IEnumerable<global::System.Guid>>), 8 },
  - 			{ typeof(global::TempProject.Wrapper<int[]>), 9 },
  - 			{ typeof(global::TempProject.Wrapper<string>), 10 },
  - 			{ typeof(global::TempProject.Wrapper<global::System.Collections.Generic.IEnumerable<global::System.Guid>>), 11 },
  - 					case 3: return new global::MessagePack.GeneratedMessagePackResolver.TempProject.MyGenericObjectFormatter<int[]>();
  - 					case 4: return new global::MessagePack.GeneratedMessagePackResolver.TempProject.MyGenericObjectFormatter<string>();
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (GenericsFormatterTests.cs:98), altered: an attribute argument made null.
  Model's reading, not a measurement: not-intended. Skipping the rest of the member loop on a null/mismatched key drops generic member types from closedTypeLookup, which is an unrequested side effect outside the SuppressSourceGeneration requirement. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): i045+a5 GenericsFormatterTests.cs:136 + an attribute argument made null
  ```
  - 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(6)
  - 			{ typeof(global::TempProject.MyObject2[]), 0 },
  - 			{ typeof(global::System.Collections.Generic.List<global::TempProject.MyObject2>), 1 },
  - 			{ typeof(global::TempProject.MyGenericObject<global::TempProject.MyObject2>), 2 },
  - 			{ typeof(global::TempProject.MyInnerGenericObject<global::TempProject.MyObject2>), 3 },
  - 			{ typeof(global::TempProject.MyObject), 4 },
  - 			{ typeof(global::TempProject.MyObject2), 5 },
  - 					case 0: return new MsgPack::Formatters.ArrayFormatter<global::TempProject.MyObject2>();
  - 					case 1: return new MsgPack::Formatters.ListFormatter<global::TempProject.MyObject2>();
  - 					case 2: return new global::MessagePack.GeneratedMessagePackResolver.TempProject.MyGenericObjectFormatter<global::TempProject.MyObject2>();
  - 					case 3: return new global::MessagePack.GeneratedMessagePackResolver.TempProject.MyInnerGenericObjectFormatter<global::TempProject.MyObject2>();
  - 					case 4: return new global::MessagePack.GeneratedMessagePackResolver.TempProject.MyObjectFormatter();
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (GenericsFormatterTests.cs:136), altered: an attribute argument made null.
  Model's reading, not a measurement: not-intended. Skipping the rest of the member loop on a null/mismatched key drops array, list, and generic member types from closedTypeLookup, which is not asked for by the SuppressSourceGeneration requirement. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): i046+a5 GenericsFormatterTests.cs:182 + an attribute argument made null
  ```
  - 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(4)
  - 			{ typeof(global::TempProject.MyObject2[]), 0 },
  - 			{ typeof(global::System.Collections.Generic.List<global::TempProject.MyObject2>), 1 },
  - 			{ typeof(global::TempProject.MyObject), 2 },
  - 			{ typeof(global::TempProject.MyObject2), 3 },
  - 					case 0: return new MsgPack::Formatters.ArrayFormatter<global::TempProject.MyObject2>();
  - 					case 1: return new MsgPack::Formatters.ListFormatter<global::TempProject.MyObject2>();
  - 					case 2: return new global::MessagePack.GeneratedMessagePackResolver.TempProject.MyObjectFormatter();
  - 					case 3: return new global::MessagePack.GeneratedMessagePackResolver.TempProject.MyObject2Formatter();
  + 		private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> closedTypeLookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(3)
  + 			{ typeof(global::System.Collections.Generic.List<global::TempProject.MyObject2>), 0 },
  + 			{ typeof(global::TempProject.MyObject), 1 },
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (GenericsFormatterTests.cs:182), altered: an attribute argument made null.
  Model's reading, not a measurement: not-intended. Skipping the rest of the member loop on a null/mismatched key drops MyObject2[] from closedTypeLookup, which is an unrequested side effect outside the SuppressSourceGeneration requirement. It quotes: "continue; // The key kind does not match the other members; the diagnostic is reported, so skip this member."
- **failed, now fails differently** in 1 input(s): i004+a3 CustomStringKeyFormatterTests.cs:36 + an attribute argument made to repeat its sibling's
  ```
  - CRASH AD0001: Analyzer 'MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer' threw an exception of type 'System.ArgumentException' with message 'An item with the same key has already…
  - CRASH CS8785: Generator 'MessagePackGenerator' failed to generate source. It will not contribute to the output and compilation errors may occur as a result. Exception was of type 'ArgumentException'…
  + DIAG MsgPack007 Error: Cannot find a public constructor
  + OUT Formatters.MessagePack.GeneratedMessagePackResolver.TempProject.MyMessagePackObjectFormatter.g.cs
  + // <auto-generated />   (×2)
  + #pragma warning disable 618, 612, 414, 168, CS1591, SA1129, SA1309, SA1312, SA1403, SA1649   (×2)
  + using MsgPack = global::MessagePack;   (×2)
  + namespace MessagePack {   (×2)
  + partial class GeneratedMessagePackResolver {
  + internal partial class TempProject {
  + 	internal sealed class MyMessagePackObjectFormatter : global::MessagePack.Formatters.IMessagePackFormatter<global::TempProject.MyMessagePackObject>
  + 	{   (×18)
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (CustomStringKeyFormatterTests.cs:36), altered: an attribute argument made to repeat its sibling's.
  Model's reading, not a measurement: not-intended. Guarding against duplicate string keys prevents the ArgumentException crash and causes the generator to emit a formatter and MsgPack007 diagnostic, which is not asked for by the SuppressSourceGeneration requirement. It quotes: "if (!stringMembers.ContainsKey(member.StringKey))"
- **failed, now gives a result** in 1 input(s): i006+a3 GenerationTests.cs:47 + an attribute argument made to repeat its sibling's
  ```
  - CRASH AD0001: Analyzer 'MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer' threw an exception of type 'System.ArgumentException' with message 'An item with the same key has already…
  - CRASH CS8785: Generator 'MessagePackGenerator' failed to generate source. It will not contribute to the output and compilation errors may occur as a result. Exception was of type 'ArgumentException'…
  + OUT Formatters.MessagePack.GeneratedMessagePackResolver.MyMessagePackObjectFormatter.g.cs
  + // <auto-generated />   (×4)
  + #pragma warning disable 618, 612, 414, 168, CS1591, SA1129, SA1309, SA1312, SA1403, SA1649   (×4)
  + #pragma warning disable CS8669 // We may leak nullable annotations into generated code.
  + using MsgPack = global::MessagePack;   (×4)
  + namespace MessagePack {   (×4)
  + partial class GeneratedMessagePackResolver {   (×3)
  + 	internal sealed class MyMessagePackObjectFormatter : MsgPack::Formatters.IMessagePackFormatter<global::MyMessagePackObject>
  + 	{   (×24)
  + 		public void Serialize(ref MsgPack::MessagePackWriter writer, global::MyMessagePackObject value, MsgPack::MessagePackSerializerOptions options)
  ```
  Where the inputs came from: 0 taken from an existing test as it is, 1 altered mechanically; in 1 the difference shows only once the input is altered. First one: the input of an existing test (GenerationTests.cs:47), altered: an attribute argument made to repeat its sibling's.
  Model's reading, not a measurement: not-intended. Guarding against duplicate integer keys prevents the ArgumentException crash and causes the generator to emit a formatter, which is not asked for by the SuppressSourceGeneration requirement. It quotes: "if (!intMembers.ContainsKey(member.IntKey))"

Sweep over inputs nobody chose (S1 above). AYA found 151 input(s) in the tests of `tests/MessagePack.SourceGenerator.Tests/MessagePack.SourceGenerator.Tests.csproj` as they were before the change, took 151 as they are and altered 2391 mechanically. No model chose or wrote any input; a model wrote only the harness that feeds them to the code and records what comes out. Run on the new code, the inputs executed 18 of the change's 18 executable line(s). The inputs are in `sweep-inputs.tsv`.

Seen by the sweep: on 1 input(s) the code under test crashes on the old and on the new code alike (for example i036+a5 GenerationTests.cs:905 + an attribute argument made null).

Inputs that came with the change. 156 of them (the sources its own tests hold, as they are and altered) were run on the new code alone, since the old code has nothing to say about them. They are held to what must be true of any input: the code under test does not crash, and what it produces compiles unless it reported an error itself. 0 break one of the two. The inputs are in `sweep-new-inputs.tsv`.

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

A model's reading of what was meant (gemini-4-hydrogen-medium (Gemini, through the Antigravity command line)). This is an opinion and is marked as one wherever it appears. It was given the text of the requirement, supplied by the owner, the commit message(s) of the change, 10 line(s) of documentation the change added, 9 comment line(s) the change added to the code, and asked about the 17 group(s) of differences that the change's own text does not explain: 0 it reads as what the change set out to do, 17 as not meant, 0 as artefacts of the build or the run, 0 as unclear. A reading of "not meant" moves an item up inside its tier; no reading moves anything down.

The requirement, read against the tests. The reading is a model's and is an opinion. Two things in it were checked here: the words in the first column are in the requirement, and a test named in the third is one that fails on the old code.

| The requirement asks | The model's reading | Test that shows it | Why |
|---|---|---|---|
| The analyzer does not work if SuppressSourceGeneration = true. | demonstrated | `SuppressSourceGenerationAnalyzerTests.DuplicateIntKeysAreReportedWithoutAnalyzerFailure` | DuplicateIntKeysAreReportedWithoutAnalyzerFailure fails on the old code and passes with the change, showing the analyzer now reports diagnostics without failing for SuppressSourceGeneration = true on duplicate or mixed keys. |
| The analyzer does not work if SuppressSourceGeneration = true. | contradicted | — | The change also alters analyzer behavior for generated MessagePackObject types without SuppressSourceGeneration = true, which goes beyond what the requirement asks for. |

The change undone one part at a time. With the old lines of a part back in the new code, the tests were run again:

| Part | Lines | Outcome |
|---|---|---|
| V1 | `src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs` 1008–1008 | needed: 1 test(s) fail without it, 0 of which existed before (e.g. `SuppressSourceGenerationAnalyzerTests.MixedKeyKindsAreReportedWithoutAnalyzerFailure`) |
| V2 | `src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs` 1023–1026 | needed: 2 test(s) fail without it, 0 of which existed before (e.g. `SuppressSourceGenerationAnalyzerTests.DuplicateIntKeysAreReportedWithoutAnalyzerFailure`) |
| V3 | `src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs` 1039–1042 | needed: 1 test(s) fail without it, 0 of which existed before (e.g. `SuppressSourceGenerationAnalyzerTests.DuplicateStringKeysAreReportedWithoutAnalyzerFailure`) |
| V4 | `src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs` 1148–1148 | **no test needs it**: every test passes with the old lines back |
| V5 | `src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs` 1163–1166 | **no test needs it**: every test passes with the old lines back |
| V6 | `src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs` 1179–1182 | **no test needs it**: every test passes with the old lines back |

Tests written to break the change, by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line). What their writer says about them is not used: each file is built and run here, and a test that fails on the new code is run on the old code, which decides whether the change caused it:

| Attack | Unit | Tests | Outcome | Saved as |
|---|---|---:|---|---|
| A1 | `TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)` | 4 | regression-demonstrated: 4 test(s) fail on the new code and pass on the old code: AyaA1Attack.MixedStringThenIntKeyOnPropertyStillReportsGenericElementTypeMustBeMessagePackObject, AyaA1Attack.MixedStringThenIntKeyOnStructPropertyStillReportsMemberTypeMustBeMessagePackObject, AyaA1Attack.MixedStringThenIntKeyOnSuppressedObjectPropertyStillReportsMemberTypeMustBeMessagePackObject, AyaA1Attack.MixedStringThenIntKeyOnPropertyStillReportsMemberTypeMustBeMessagePackObject | attacks/A1.cs |
| K1 | `TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)` | 5 | regression-demonstrated: 2 test(s) fail on the new code and pass on the old code: AyaK1Attack.StringThenIntKeyPropertyStillHasItsTypeValidatedForGeneratedTypes, AyaK1Attack.StringThenIntKeyPropertyStillHasItsTypeValidated | attacks/K1.cs |
| K2 | `TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)` | 4 | regression-demonstrated: 2 test(s) fail on the new code and pass on the old code: AyaK2Attack.MixedKeyKindPropertyStillReportsUnserializableType_Generated, AyaK2Attack.MixedKeyKindPropertyStillReportsUnserializableType_Suppressed | attacks/K2.cs |
| K3 | `TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?)` | 4 | regression-demonstrated: 1 test(s) fail on the new code and pass on the old code: AyaK3Attack.MixedIntKeyPropertyAfterStringKeyWithUnserializableTypeReportsBothDiagnostics | attacks/K3.cs |

A1: `AyaA1Attack.MixedStringThenIntKeyOnPropertyStillReportsGenericElementTypeMustBeMessagePackObject` fails on the new code; on the old code: Passed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Mismatch between number of diagnostics returned, expected "2" actual "1"

Diagnostics:
// /0/Test0.cs(14,5): error MsgPack005: All KeyAttribute arguments must be of the same type (either string or int)
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(14, 5, 15, 72),


```

A1: `AyaA1Attack.MixedStringThenIntKeyOnStructPropertyStillReportsMemberTypeMustBeMessagePackObject` fails on the new code; on the old code: Passed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Mismatch between number of diagnostics returned, expected "2" actual "1"

Diagnostics:
// /0/Test0.cs(14,5): error MsgPack005: All KeyAttribute arguments must be of the same type (either string or int)
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(14, 5, 15, 39),


```

A1: `AyaA1Attack.MixedStringThenIntKeyOnSuppressedObjectPropertyStillReportsMemberTypeMustBeMessagePackObject` fails on the new code; on the old code: Passed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Mismatch between number of diagnostics returned, expected "2" actual "1"

Diagnostics:
// /0/Test0.cs(14,5): error MsgPack005: All KeyAttribute arguments must be of the same type (either string or int)
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(14, 5, 15, 39),


```

A1: `AyaA1Attack.MixedStringThenIntKeyOnPropertyStillReportsMemberTypeMustBeMessagePackObject` fails on the new code; on the old code: Passed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Mismatch between number of diagnostics returned, expected "2" actual "1"

Diagnostics:
// /0/Test0.cs(14,5): error MsgPack005: All KeyAttribute arguments must be of the same type (either string or int)
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(14, 5, 15, 39),


```

K1: `AyaK1Attack.StringThenIntKeyPropertyStillHasItsTypeValidatedForGeneratedTypes` fails on the new code; on the old code: Passed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Mismatch between number of diagnostics returned, expected "2" actual "1"

Diagnostics:
// /0/Test0.cs(14,5): error MsgPack005: All KeyAttribute arguments must be of the same type (either string or int)
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(14, 5, 15, 39),


```

K1: `AyaK1Attack.StringThenIntKeyPropertyStillHasItsTypeValidated` fails on the new code; on the old code: Passed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Mismatch between number of diagnostics returned, expected "2" actual "1"

Diagnostics:
// /0/Test0.cs(14,5): error MsgPack005: All KeyAttribute arguments must be of the same type (either string or int)
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(14, 5, 15, 39),


```

K2: `AyaK2Attack.MixedKeyKindPropertyStillReportsUnserializableType_Generated` fails on the new code; on the old code: Passed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Mismatch between number of diagnostics returned, expected "2" actual "1"

Diagnostics:
// /0/Test0.cs(14,5): error MsgPack005: All KeyAttribute arguments must be of the same type (either string or int)
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(14, 5, 15, 39),


```

K2: `AyaK2Attack.MixedKeyKindPropertyStillReportsUnserializableType_Suppressed` fails on the new code; on the old code: Passed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Mismatch between number of diagnostics returned, expected "2" actual "1"

Diagnostics:
// /0/Test0.cs(14,5): error MsgPack005: All KeyAttribute arguments must be of the same type (either string or int)
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(14, 5, 15, 39),


```

K2: `AyaK2Attack.MixedKeyKindFieldStillReportsUnserializableType_Generated` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Mismatch between number of diagnostics returned, expected "2" actual "1"

Diagnostics:
// /0/Test0.cs(15,18): error MsgPack005: All KeyAttribute arguments must be of the same type (either string or int)
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(15, 18, 15, 24),


```

K3: `AyaK3Attack.MixedIntKeyPropertyAfterStringKeyWithUnserializableTypeReportsBothDiagnostics` fails on the new code; on the old code: Passed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Mismatch between number of diagnostics returned, expected "2" actual "1"

Diagnostics:
// /0/Test0.cs(14,5): error MsgPack005: All KeyAttribute arguments must be of the same type (either string or int)
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(14, 5, 15, 37),


```

K3: `AyaK3Attack.DuplicateStringKeyFieldReportsKeysMustBeUniqueWithoutAnalyzerFailure` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Expected diagnostic to start on line "10" was actually on line "11"

Expected diagnostic:
    // /0/Test0.cs(10,5,11,26): error MsgPack005
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(10, 5, 11, 26),

Actual diagnostic:
    // /0/Test0.cs(11,19): error MsgPack005: All KeyAttribute arguments must be unique
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(11, 19, 11, 25),
…
```

K3: `AyaK3Attack.MixedIntKeyFieldAfterStringKeyWithUnserializableTypeReportsBothDiagnostics` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Diagnostics of test state
Mismatch between number of diagnostics returned, expected "2" actual "1"

Diagnostics:
// /0/Test0.cs(15,18): error MsgPack005: All KeyAttribute arguments must be of the same type (either string or int)
VerifyCS.Diagnostic(MsgPack00xMessagePackAnalyzer.MsgPack005).WithSpan(15, 18, 15, 24),


```

Exhaustive mode. Every member that depends on the change and has executable code is run by at least one original test, so there was nothing to probe.

## 5. What was not checked

- **Unexecuted code.** TypeCollector.GetObjectInfo(INamedTypeSymbol, ISymbol?) — line(s) 1148, 1179–1182 of src/MessagePack.SourceGenerator/CodeAnalysis/TypeCollector.cs were never executed by any test; 29 members depend on this symbol and inherit the doubt
- **Documentation.** 1 file(s), 14 lines: no tool checked that this text is accurate (README.md)

## 6. What AYA refuses to decide

- **Requirement.** Whether this change does what requirement MessagePack-CSharp#2234 asks. AYA measured what the code does under test; it has not interpreted the requirement.
- **Test value.** Whether SuppressSourceGenerationAnalyzerTests.AllowPrivateRequiredIsReported is evidence for this change: it passes on the old code too, so it does not demonstrate the new behaviour.
- **Test value.** Whether SuppressSourceGenerationAnalyzerTests.MemberTypeMustBeMessagePackObjectIsReported is evidence for this change: it passes on the old code too, so it does not demonstrate the new behaviour.
- **Test value.** Whether SuppressSourceGenerationAnalyzerTests.MemberWithoutKeyIsReported is evidence for this change: it passes on the old code too, so it does not demonstrate the new behaviour.
- **Test value.** Whether SuppressSourceGenerationAnalyzerTests.SourceGenerationOnlyDiagnosticsAreNotReportedWhenSuppressed is evidence for this change: it passes on the old code too, so it does not demonstrate the new behaviour.
- **Test value.** Whether SuppressedSourceGenerationTests.NoFormatterIsGeneratedForSuppressedType is evidence for this change: it passes on the old code too, so it does not demonstrate the new behaviour.
- **Test value.** Whether SuppressedSourceGenerationTests.SuppressedTypeRoundTripsThroughDynamicResolver is evidence for this change: it passes on the old code too, so it does not demonstrate the new behaviour.
- **Side effects.** Whether behaviour changes in already-existing members are intended. They are reachable from code the requirement does not mention.
- **Proposed tests.** Whether the 1 accepted test(s) written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) assert the right behaviour. AYA checked that they compile, pass and reach their target; a model chose what to assert. They are proposals in the proposed-tests folder, not part of the change.

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
| Commit | `9007cee1163c4a274fd60c3cc20c21ae63622786` |
| Evidence | sha256 `dfd37014a84d1498b78b3bc9959f407bd1e848755eaf352a63b6b65fcaac0d71` |
| Looked at by the owner | section 1, down to item no. ___ |
| Residual risk accepted | the items in sections 5 and 6 |
| Date | |
