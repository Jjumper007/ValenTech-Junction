# Review evidence — ju26-issue-2129

| | |
|---|---|
| Requirement | MessagePack-CSharp#2129 |
| Change | `083afd99` Agentic implementation of #2129 Feature Request: Allow generation of Formatter for a type in a referenced assembly |
| Written by | Stelian Vultur, 2026-11-12T00:39:37+02:00 |
| Compared with | `32258fdb` |
| Built with | .NET SDK 9.0.321 (the repository selects 8.0.428) |
| Produced by | aya 0.1.0, 2026-11-15T04:12:29Z |
| Evidence file | `evidence.json`, sha256 `68825c75b46edfb34186c1c8ea84fcc7b15ed768bd10472558aedd6cb4a2ada8` |

Every statement below comes from a tool run, and its source is named. Where a model wrote a probe or a test, what is stated is what running it showed, never what the model said.

## 1. Where to look, most deserving first

Everything the change touches is in this list; nothing was left out of it. The order says where a mistake is most likely to matter and least likely to be noticed. The last tier means no signal was found, not that the code is right. How far down to go is the owner's decision.

| # | Tier | What | Why it is here |
|---:|---|---|---|
| 1 | 2. Changes a result that existed before | MsgPack00xMessagePackAnalyzer.SupportedDiagnostics | 6 input(s) give a different result than on the old code, and the change's own text does not explain it |
| 2 | 3. Existing code others depend on, where a mistake would go unnoticed | ObjectSerializationInfo.Equals(ObjectSerializationInfo?) | it modifies code that existed, 57 member(s) depend on it, and a mistake in it would go unnoticed |
| 3 | 3. Existing code others depend on, where a mistake would go unnoticed | FormatterTemplate.TransformText() | it modifies code that existed, 3 member(s) depend on it, and a mistake in it would go unnoticed |
| 4 | 3. Existing code others depend on, where a mistake would go unnoticed | GeneratorUtilities.GetAnalyzerOption(IncrementalGeneratorInitializationContext) | it modifies code that existed, 2 member(s) depend on it, and a mistake in it would go unnoticed |
| 5 | 4. Could not be measured | The requirement: 5 of the 5 thing(s) it asks for are not shown by any test | a model reads "Allow the generator and analyzers to work with user-defined versions of the MessagePack attributes." as not addressed: The change uses contractless mode or a SurrogateType in the consuming assembly instead of recognizing user-defined copies of MessagePack attributes. (its opinion) |
| 6 | 4. Could not be measured | ObjectSerializationInfo.IsUserDeclaredFormatter | none of its changed lines can be broken by the alterations AYA knows |
| 7 | 4. Could not be measured | MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial | a test written to break it fails on the new code, and the old code cannot say who is wrong (attacks/A12.cs) |
| 8 | 4. Could not be measured | GeneratedFormatterCollector.BinaryWriteTypeFormat | none of its changed lines can be broken by the alterations AYA knows |
| 9 | 4. Could not be measured | StringKeyFormatterTemplate.TransformText() | the analysis cannot see all of it: it is generated code: what produces it was not re-run, and the graph does not link it to its generator |
| 10 | 4. Could not be measured | GeneratedFormatterCollector.CreateContractlessMembers(INamedTypeSymbol) | no break of its lines was tried (budget, or no test executes them) |
| 11 | 4. Could not be measured | GeneratedFormatterRequest | none of its changed lines can be broken by the alterations AYA knows |
| 12 | 4. Could not be measured | GeneratedMessagePackFormatterAttribute.SurrogateType | none of its changed lines can be broken by the alterations AYA knows |
| 13 | 4. Could not be measured | src/MessagePack.SourceGenerator/Transforms/FormatterTemplate.tt | a template that generates code: it was not re-run to confirm the committed output is what it produces |
| 14 | 4. Could not be measured | src/MessagePack.SourceGenerator/Transforms/StringKey/StringKeyFormatterTemplate.tt | a template that generates code: it was not re-run to confirm the committed output is what it produces |
| 15 | 4. Could not be measured | tests/MessagePack.SourceGenerator.ExecutionTests/MessagePack.SourceGenerator.ExecutionTests.csproj | build configuration: it can change how everything compiles, and no check here covers it |
| 16 | 4. Could not be measured | tests/MessagePack.SourceGenerator.ExternalTypes/MessagePack.SourceGenerator.ExternalTypes.csproj | build configuration: it can change how everything compiles, and no check here covers it |
| 17 | 4. Could not be measured | tests/MessagePack.SourceGenerator.MapModeExecutionTests/MessagePack.SourceGenerator.MapModeExecutionTests.csproj | build configuration: it can change how everything compiles, and no check here covers it |
| 18 | 5. A mistake here would go unnoticed | MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported | a mistake in it would go unnoticed, and nothing else was found against it |
| 19 | 5. A mistake here would go unnoticed | MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustImplementFormatter | a mistake in it would go unnoticed, and nothing else was found against it |
| 20 | 5. A mistake here would go unnoticed | MsgPack00xMessagePackAnalyzer.SurrogateMemberNotFound | a mistake in it would go unnoticed, and nothing else was found against it |
| 21 | 5. A mistake here would go unnoticed | GeneratedFormatterCollector.GetMemberType(ISymbol) | a mistake in it would go unnoticed, and nothing else was found against it |
| 22 | 5. A mistake here would go unnoticed | GeneratedFormatterCollector.CreateMember(ISymbol, int, string) | a mistake in it would go unnoticed, and nothing else was found against it |
| 23 | 5. A mistake here would go unnoticed | GeneratedFormatterCollector.GetInstanceMembers(INamedTypeSymbol) | a mistake in it would go unnoticed, and nothing else was found against it |
| 24 | 5. A mistake here would go unnoticed | GeneratedFormatterCollector.IsPublicReadWrite(ISymbol) | a mistake in it would go unnoticed, and nothing else was found against it |
| 25 | 5. A mistake here would go unnoticed | GeneratedFormatterCollector.CreateSurrogateMembers(INamedTypeSymbol, INamedTypeSymbol, ReferenceSymbols, Action<Diagnostic>?, Location, out bool) | a mistake in it would go unnoticed, and nothing else was found against it |
| 26 | 5. A mistake here would go unnoticed | GeneratedFormatterCollector.Collect(Compilation, AnalyzerOptions, ReferenceSymbols, Action<Diagnostic>?, INamedTypeSymbol, INamedTypeSymbol?, CancellationToken) | a mistake in it would go unnoticed, and nothing else was found against it |
| 27 | 5. A mistake here would go unnoticed | 7 of the 13 tests that came with the change | they were not shown to fail without the change, so they do not demonstrate it |
| 28 | 5. A mistake here would go unnoticed | GeneratedMessagePackFormatterAttribute | a mistake in it would go unnoticed, and nothing else was found against it |
| 29 | 5. A mistake here would go unnoticed | MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext) | a mistake in it would go unnoticed, and nothing else was found against it |
| 30 | 6. No signal found | The change as a whole, over 2542 inputs nobody chose | on 40 input(s) the code under test crashes on the old and on the new code alike (for example i032+a3 GenerationTests.cs:785 + an attribute argument made to repeat its sibling's) |
| 31 | 6. No signal found | MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupportedId | a new declaration with nothing to execute: no test run can say anything about it, and nothing was found against it |
| 32 | 6. No signal found | MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartialId | a new declaration with nothing to execute: no test run can say anything about it, and nothing was found against it |
| 33 | 6. No signal found | MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustImplementFormatterId | a new declaration with nothing to execute: no test run can say anything about it, and nothing was found against it |
| 34 | 6. No signal found | MsgPack00xMessagePackAnalyzer.SurrogateMemberNotFoundId | a new declaration with nothing to execute: no test run can say anything about it, and nothing was found against it |
| 35 | 6. No signal found | Constants.GeneratedMessagePackFormatterAttributeName | a new declaration with nothing to execute: no test run can say anything about it, and nothing was found against it |
| 36 | 6. No signal found | GeneratedFormatterCollector.IsPartial(INamedTypeSymbol, CancellationToken) | nothing was found against it; that is not a statement that it is correct |
| 37 | 6. No signal found | Constants.GeneratedMessagePackFormatterSurrogatePropertyName | a new declaration with nothing to execute: no test run can say anything about it, and nothing was found against it |
| 38 | 6. No signal found | GeneratedFormatterCollector | a new declaration with nothing to execute: no test run can say anything about it, and nothing was found against it |
| 39 | 6. No signal found | 2 generated files with a similar edit (e.g. src/MessagePack.SourceGenerator/Transforms/FormatterTemplate.cs) | all 2 files received exactly the same edit (4 line(s)); nothing was found against them |
| 40 | 6. No signal found | README.md | documentation: no tool checks that the text is accurate, and it cannot break the code |
| 41 | 6. No signal found | doc/analyzers/MsgPack019.md | documentation: no tool checks that the text is accurate, and it cannot break the code |
| 42 | 6. No signal found | doc/analyzers/MsgPack020.md | documentation: no tool checks that the text is accurate, and it cannot break the code |
| 43 | 6. No signal found | doc/analyzers/MsgPack021.md | documentation: no tool checks that the text is accurate, and it cannot break the code |
| 44 | 6. No signal found | doc/analyzers/MsgPack022.md | documentation: no tool checks that the text is accurate, and it cannot break the code |
| 45 | 6. No signal found | doc/analyzers/index.md | documentation: no tool checks that the text is accurate, and it cannot break the code |
| 46 | 6. No signal found | src/MessagePack.SourceGenerator/AnalyzerReleases.Unshipped.md | documentation: no tool checks that the text is accurate, and it cannot break the code |

Rule used. Tier 1: an existing test now fails because of it, an input that gave a result now fails, or a test written to break it fails on the new code and passes on the old. Tier 2: an input gives a different result than on the old code. Tier 3: it modifies code that existed, other code depends on it, and a line is never executed or can be broken without any test failing. Tier 4: the analysis could not measure it (deleted code, modified declarations, no break could be tried, generated or conditionally compiled code, a failing test nothing can arbitrate). Tier 5: a line is never executed or can be broken unnoticed, in code nothing else depends on or that is new; or tests that came with the change were not shown to fail without it. Tier 6: none of the above was found. Inside a tier, a difference the change's own text does not explain, or that a model reads as not meant, comes before one made only of lines the change adds or removes; then the unit with more code depending on it comes first. A model's reading can only move an item up.

### 1. MsgPack00xMessagePackAnalyzer.SupportedDiagnostics

`src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs`, changed lines 381–385

- **Why it is here**
  - 6 input(s) give a different result than on the old code, and the change's own text does not explain it
  - a model reads 6 difference(s) as the new functionality (its opinion; they keep their place)
  - none of its changed lines can be broken by the alterations AYA knows
- **How likely it is to be wrong (indications, not a measurement)**
  - modifies code that already existed (5 line(s))
  - old and new code differ on 6 of 12 input(s): 6 changed (probes/P2.cs)
  - a test written to break it fails on the old code as well, so the change is not the cause (attacks/A1.cs)
- **How much breaks if it is wrong**
  - 32 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as FormatterDescriptor.TryCreate(INamedTypeSymbol, out FormatterDescriptor?), FormatterCodeFixProvider.RegisterCodeFixesAsync(CodeFixContext)
  - 0 of the 28 dependents with executable code are run by no test, so a break reaching them would not be seen
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 5 of 5 changed executable line(s) ran under the tests
- **For the owner to check**
  - Go through the inputs whose result changed and confirm every change is one the requirement asks for.
  - Read the changed lines: no measurement stands in for it.

### 2. ObjectSerializationInfo.Equals(ObjectSerializationInfo?)

`src/MessagePack.SourceGenerator/CodeAnalysis/ObjectSerializationInfo.cs`, changed lines 168

- **Why it is here**
  - it modifies code that existed, 57 member(s) depend on it, and a mistake in it would go unnoticed
  - no break of its lines was tried (budget, or no test executes them)
- **How likely it is to be wrong (indications, not a measurement)**
  - modifies code that already existed (1 line(s))
  - old and new code give the same result on 11 input(s) (probes/P1.cs)
  - 4 test(s) a model wrote to break it all pass (attacks/A2.cs)
- **How much breaks if it is wrong**
  - 57 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as FullModel.Empty, FullModel.Combine(ImmutableArray<FullModel>)
  - 1 of the 46 dependents with executable code are run by no test, so a break reaching them would not be seen (e.g. TransformUtilities.EmitClassesForNamespace(IFormatterTemplate, out string, Action<string>))
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 0 of 1 changed executable line(s) ran under the tests; never ran: 168
  - 4 possible break(s) were not tried (budget, or on a line no test executes)
- **For the owner to check**
  - Lines 168 never run under any test: read them, or have them exercised.
  - 1 dependent(s) are run by no test: check the ones that rely on what changed here.
  - Read the changed lines: no measurement stands in for it.

### 3. FormatterTemplate.TransformText()

`src/MessagePack.SourceGenerator/Transforms/FormatterTemplate.cs`, changed lines 33–35

- **Why it is here**
  - it modifies code that existed, 3 member(s) depend on it, and a mistake in it would go unnoticed
- **How likely it is to be wrong (indications, not a measurement)**
  - modifies code that already existed (3 line(s))
  - the changed lines touch a condition
  - old and new code give the same result on 11 input(s) (probes/P3.cs)
  - a test written to break it fails on the old code as well, so the change is not the cause (attacks/A3.cs)
- **How much breaks if it is wrong**
  - 3 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as IFormatterTemplate.TransformText(), MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - breaking line 33 made 54 test(s) fail (measured)
  - 0 of the 1 dependents with executable code are run by no test, so a break reaching them would not be seen
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 3 of 3 changed executable line(s) ran under the tests
  - 5 of 5 deliberate break(s) made a test fail
  - lines 33–35 can be put back as they were and every test still passes: no test needs this part of the change
- **What the analysis cannot see**
  - it is generated code: what produces it was not re-run, and the graph does not link it to its generator
- **For the owner to check**
  - Lines 33–35: with the old code back, no test fails. Find out what this part of the change is for, and whether the requirement asks for it.

### 4. GeneratorUtilities.GetAnalyzerOption(IncrementalGeneratorInitializationContext)

`src/MessagePack.SourceGenerator/GeneratorUtilities.cs`, changed lines 35–36, 38

- **Why it is here**
  - it modifies code that existed, 2 member(s) depend on it, and a mistake in it would go unnoticed
- **How likely it is to be wrong (indications, not a measurement)**
  - modifies code that already existed (3 line(s))
  - the changed lines touch null handling
  - old and new code give the same result on 10 input(s) (probes/P5.cs)
  - a test written to break it fails on the old code as well, so the change is not the cause (attacks/A4.cs)
- **How much breaks if it is wrong**
  - 2 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as CompositeResolverGenerator.Initialize(IncrementalGeneratorInitializationContext), MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - breaking line 38 made 15 test(s) fail (measured)
  - 0 of the 1 dependents with executable code are run by no test, so a break reaching them would not be seen
- **Whether anything would notice**
  - 3 of 3 changed executable line(s) ran under the tests
  - 3 of 3 deliberate break(s) made a test fail
  - 2 of those were noticed only by tests that came with the change, none by a test that existed before
  - lines 35–36 can be put back as they were and every test still passes: no test needs this part of the change
- **For the owner to check**
  - Lines 35–36: with the old code back, no test fails. Find out what this part of the change is for, and whether the requirement asks for it.

### 5. The requirement: 5 of the 5 thing(s) it asks for are not shown by any test

- **Why it is here**
  - a model reads "Allow the generator and analyzers to work with user-defined versions of the MessagePack attributes." as not addressed: The change uses contractless mode or a SurrogateType in the consuming assembly instead of recognizing user-defined copies of MessagePack attributes. (its opinion)
  - a model reads "Source-generate the attributes themselves using [`IncrementalGeneratorInitializationContext.RegisterPostInitializationOutput`](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md#outputting-values) instead of them existing in the `MessagePack.Annotations` assembly." as not addressed: The new GeneratedMessagePackFormatterAttribute is added to MessagePack.Annotations rather than being emitted via RegisterPostInitializationOutput. (its opinion)
  - a model reads "I need to have a source-generated `IMessagePackFormatter` for type `ProjectA.MyType` generated **inside Project B** instead of Project A, because the latter has no (and cannot have) reference to MessagePack nuget package." as not demonstrated: The change adds GeneratedFormatterCollector to generate formatters for external types, but all tests in GeneratedFormatterForExternalTypesTests could not be compiled against the old code and the diagnostic tests that fail on the old code only use same-assembly types with hand-written formatter members. (its opinion)
  - a model reads "Add a `[GeneratedMessagePackFormatter]` attribute that will work for partial classes implementing `IMessagePackFormatter<T>`." as not demonstrated: The change adds GeneratedMessagePackFormatterAttribute and collector support, but the tests that fail on the old code only verify error diagnostics rather than successful generation for a valid partial formatter class. (its opinion)
  - a model reads "The class does not even need to declare a body (no `{` and `}`), because the actual implementation is source-generated by `MessagePack.SourceGenerator.MessagePackGenerator` as usual." as not demonstrated: None of the listed tests declare a bodyless partial class without braces, and the generation tests could not be compiled against the old code. (its opinion)
- **How likely it is to be wrong (indications, not a measurement)**
  - 0 thing(s) the requirement asks for are shown by a test that fails without the change (the model named the test; that it fails on the old code was measured)
- **What the analysis cannot see**
  - splitting the requirement into things asked for is a model's reading; the words are checked to be in the requirement, their meaning is not
- **For the owner to check**
  - Read the requirement against this list. For each thing no test shows, decide whether the change does it.

### 6. ObjectSerializationInfo.IsUserDeclaredFormatter

`src/MessagePack.SourceGenerator/CodeAnalysis/ObjectSerializationInfo.cs`, changed lines 13–17

- **Why it is here**
  - none of its changed lines can be broken by the alterations AYA knows
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (5 line(s))
  - a test written to break it fails on the old code as well, so the change is not the cause (attacks/A5.cs)
- **How much breaks if it is wrong**
  - 58 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as ObjectSerializationInfo.Equals(ObjectSerializationInfo?), FormatterTemplate.TransformText()
  - 1 of the 46 dependents with executable code are run by no test, so a break reaching them would not be seen (e.g. TransformUtilities.EmitClassesForNamespace(IFormatterTemplate, out string, Action<string>))
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 1 of 1 changed executable line(s) ran under the tests
  - lines 13–18 could not be undone on their own: the code does not compile without the rest of the change
- **For the owner to check**
  - 1 dependent(s) are run by no test: check the ones that rely on what changed here.
  - Read the changed lines: no measurement stands in for it.

### 7. MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial

`src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs`, changed lines 318–325

- **Why it is here**
  - a test written to break it fails on the new code, and the old code cannot say who is wrong (attacks/A12.cs)
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (8 line(s))
- **How much breaks if it is wrong**
  - 33 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MsgPack00xMessagePackAnalyzer.SupportedDiagnostics, MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - no break of it made any test fail, so how much it would take down is unknown
  - 0 of the 28 dependents with executable code are run by no test, so a break reaching them would not be seen
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 8 of 8 changed executable line(s) ran under the tests
  - 0 of 2 deliberate break(s) made a test fail; unnoticed: line 324 (true → false); line 325 (call passed as argument → default)
  - lines 318–353 could not be undone on their own: the code does not compile without the rest of the change
- **For the owner to check**
  - Line 324: decide what `true` must do. Altering it (true → false) makes no test fail.
  - Line 325: decide what `AnalyzerUtilities.GetHelpLink(GeneratedFormatterMustBePartialId)` must do. Altering it (call passed as argument → default) makes no test fail.
  - Read the failing test and decide whether the test or the code is wrong.

### 8. GeneratedFormatterCollector.BinaryWriteTypeFormat

`src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs`, changed lines 19–22

- **Why it is here**
  - none of its changed lines can be broken by the alterations AYA knows
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (4 line(s))
  - a test written to break it fails on the old code as well, so the change is not the cause (attacks/A6.cs)
- **How much breaks if it is wrong**
  - 5 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
- **Whether anything would notice**
  - 4 of 4 changed executable line(s) ran under the tests
  - lines 1–300 could not be undone on their own: the code does not compile without the rest of the change
- **For the owner to check**
  - Read the changed lines: no measurement stands in for it.

### 9. StringKeyFormatterTemplate.TransformText()

`src/MessagePack.SourceGenerator/Transforms/StringKey/StringKeyFormatterTemplate.cs`, changed lines 37–39

- **Why it is here**
  - the analysis cannot see all of it: it is generated code: what produces it was not re-run, and the graph does not link it to its generator
- **How likely it is to be wrong (indications, not a measurement)**
  - modifies code that already existed (3 line(s))
  - the changed lines touch a condition
  - old and new code give the same result on 10 input(s) (probes/P4.cs)
  - a test written to break it fails on the old code as well, so the change is not the cause (attacks/A7.cs)
- **How much breaks if it is wrong**
  - 3 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as IFormatterTemplate.TransformText(), MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - breaking line 37 made 21 test(s) fail (measured)
  - 0 of the 1 dependents with executable code are run by no test, so a break reaching them would not be seen
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 3 of 3 changed executable line(s) ran under the tests
  - 5 of 5 deliberate break(s) made a test fail
- **What the analysis cannot see**
  - it is generated code: what produces it was not re-run, and the graph does not link it to its generator

### 10. GeneratedFormatterCollector.CreateContractlessMembers(INamedTypeSymbol)

`src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs`, changed lines 128–138, 140–141

- **Why it is here**
  - no break of its lines was tried (budget, or no test executes them)
  - a test written to break it fails on the new code, and the old code cannot say who is wrong (attacks/A8.cs)
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (13 line(s))
  - the changed lines touch a loop
- **How much breaks if it is wrong**
  - 2 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
- **Whether anything would notice**
  - 0 of 9 changed executable line(s) ran under the tests; never ran: 132–138, 140–141
  - lines 1–300 could not be undone on their own: the code does not compile without the rest of the change
  - 5 possible break(s) were not tried (budget, or on a line no test executes)
- **For the owner to check**
  - Lines 132–138, 140–141 never run under any test: read them, or have them exercised.
  - Read the changed lines: no measurement stands in for it.
  - Read the failing test and decide whether the test or the code is wrong.

### 11. GeneratedFormatterRequest

`src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterRequest.cs`, changed lines 8–13

- **Why it is here**
  - none of its changed lines can be broken by the alterations AYA knows
  - a test written to break it fails on the new code, and the old code cannot say who is wrong (attacks/K1.cs)
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (6 line(s))
  - a test written to break it fails on the old code as well, so the change is not the cause (attacks/A9.cs)
- **How much breaks if it is wrong**
  - 1 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 1 of 1 changed executable line(s) ran under the tests
  - lines 1–13 could not be undone on their own: the code does not compile without the rest of the change
- **For the owner to check**
  - Read the changed lines: no measurement stands in for it.
  - Read the failing test and decide whether the test or the code is wrong.

### 12. GeneratedMessagePackFormatterAttribute.SurrogateType

`src/MessagePack.Annotations/Attributes.cs`, changed lines 168–176

- **Why it is here**
  - none of its changed lines can be broken by the alterations AYA knows
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (9 line(s))
  - 5 test(s) a model wrote to break it all pass (attacks/A10.cs)
- **How much breaks if it is wrong**
  - nothing in the analysed code depends on it
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 0 of 1 changed executable line(s) ran under the tests; never ran: 176
  - lines 142–177 can be put back as they were and every test still passes: no test needs this part of the change
- **For the owner to check**
  - Lines 142–177: with the old code back, no test fails. Find out what this part of the change is for, and whether the requirement asks for it.
  - Lines 176 never run under any test: read them, or have them exercised.
  - Read the changed lines: no measurement stands in for it.

### 13. src/MessagePack.SourceGenerator/Transforms/FormatterTemplate.tt

`src/MessagePack.SourceGenerator/Transforms/FormatterTemplate.tt`, 2 changed line(s)

- **Why it is here**
  - a template that generates code: it was not re-run to confirm the committed output is what it produces

### 14. src/MessagePack.SourceGenerator/Transforms/StringKey/StringKeyFormatterTemplate.tt

`src/MessagePack.SourceGenerator/Transforms/StringKey/StringKeyFormatterTemplate.tt`, 2 changed line(s)

- **Why it is here**
  - a template that generates code: it was not re-run to confirm the committed output is what it produces

### 15. tests/MessagePack.SourceGenerator.ExecutionTests/MessagePack.SourceGenerator.ExecutionTests.csproj

`tests/MessagePack.SourceGenerator.ExecutionTests/MessagePack.SourceGenerator.ExecutionTests.csproj`, 5 changed line(s)

- **Why it is here**
  - build configuration: it can change how everything compiles, and no check here covers it

### 16. tests/MessagePack.SourceGenerator.ExternalTypes/MessagePack.SourceGenerator.ExternalTypes.csproj

`tests/MessagePack.SourceGenerator.ExternalTypes/MessagePack.SourceGenerator.ExternalTypes.csproj`, 17 changed line(s)

- **Why it is here**
  - build configuration: it can change how everything compiles, and no check here covers it

### 17. tests/MessagePack.SourceGenerator.MapModeExecutionTests/MessagePack.SourceGenerator.MapModeExecutionTests.csproj

`tests/MessagePack.SourceGenerator.MapModeExecutionTests/MessagePack.SourceGenerator.MapModeExecutionTests.csproj`, 3 changed line(s)

- **Why it is here**
  - build configuration: it can change how everything compiles, and no check here covers it

### 18. MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported

`src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs`, changed lines 336–343

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (8 line(s))
  - 5 test(s) a model wrote to break it all pass (attacks/A11.cs)
- **How much breaks if it is wrong**
  - 33 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MsgPack00xMessagePackAnalyzer.SupportedDiagnostics, MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - no break of it made any test fail, so how much it would take down is unknown
  - 0 of the 28 dependents with executable code are run by no test, so a break reaching them would not be seen
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 8 of 8 changed executable line(s) ran under the tests
  - 0 of 2 deliberate break(s) made a test fail; unnoticed: line 342 (true → false); line 343 (call passed as argument → default)
  - lines 318–353 could not be undone on their own: the code does not compile without the rest of the change
- **For the owner to check**
  - Line 342: decide what `true` must do. Altering it (true → false) makes no test fail.
  - Line 343: decide what `AnalyzerUtilities.GetHelpLink(GeneratedFormatterDataTypeNotSupportedId)` must do. Altering it (call passed as argument → default) makes no test fail.

### 19. MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustImplementFormatter

`src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs`, changed lines 327–334

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (8 line(s))
- **How much breaks if it is wrong**
  - 33 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MsgPack00xMessagePackAnalyzer.SupportedDiagnostics, MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - no break of it made any test fail, so how much it would take down is unknown
  - 0 of the 28 dependents with executable code are run by no test, so a break reaching them would not be seen
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 8 of 8 changed executable line(s) ran under the tests
  - 0 of 2 deliberate break(s) made a test fail; unnoticed: line 333 (true → false); line 334 (call passed as argument → default)
  - lines 318–353 could not be undone on their own: the code does not compile without the rest of the change
- **For the owner to check**
  - Line 333: decide what `true` must do. Altering it (true → false) makes no test fail.
  - Line 334: decide what `AnalyzerUtilities.GetHelpLink(GeneratedFormatterMustImplementFormatterId)` must do. Altering it (call passed as argument → default) makes no test fail.

### 20. MsgPack00xMessagePackAnalyzer.SurrogateMemberNotFound

`src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs`, changed lines 345–352

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (8 line(s))
- **How much breaks if it is wrong**
  - 33 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MsgPack00xMessagePackAnalyzer.SupportedDiagnostics, FormatterDescriptor.TryCreate(INamedTypeSymbol, out FormatterDescriptor?)
  - no break of it made any test fail, so how much it would take down is unknown
  - 0 of the 28 dependents with executable code are run by no test, so a break reaching them would not be seen
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 8 of 8 changed executable line(s) ran under the tests
  - 0 of 2 deliberate break(s) made a test fail; unnoticed: line 351 (true → false); line 352 (call passed as argument → default)
  - lines 318–353 could not be undone on their own: the code does not compile without the rest of the change
- **For the owner to check**
  - Line 351: decide what `true` must do. Altering it (true → false) makes no test fail.
  - Line 352: decide what `AnalyzerUtilities.GetHelpLink(SurrogateMemberNotFoundId)` must do. Altering it (call passed as argument → default) makes no test fail.

### 21. GeneratedFormatterCollector.GetMemberType(ISymbol)

`src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs`, changed lines 286–291

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (6 line(s))
  - the changed lines touch a condition, error handling
- **How much breaks if it is wrong**
  - 5 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - breaking line 286 made 2 test(s) fail (measured)
- **Whether anything would notice**
  - 4 of 6 changed executable line(s) ran under the tests; never ran: 289–290
  - 1 of 1 deliberate break(s) made a test fail
  - 1 of those were noticed only by tests that came with the change, none by a test that existed before
  - lines 1–300 could not be undone on their own: the code does not compile without the rest of the change
- **For the owner to check**
  - Lines 289–290 never run under any test: read them, or have them exercised.

### 22. GeneratedFormatterCollector.CreateMember(ISymbol, int, string)

`src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs`, changed lines 215–225, 227–244

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (29 line(s))
  - the changed lines touch a condition
- **How much breaks if it is wrong**
  - 4 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - no break of it made any test fail, so how much it would take down is unknown
- **Whether anything would notice**
  - 26 of 28 changed executable line(s) ran under the tests; never ran: 223–224
  - 0 of 6 deliberate break(s) made a test fail; unnoticed: line 219 (is X → is not X); line 227 (return x → return default); line 229 (true → false)
  - lines 1–300 could not be undone on their own: the code does not compile without the rest of the change
  - 2 possible break(s) were not tried (budget, or on a line no test executes)
- **For the owner to check**
  - Line 219: decide what `target is IPropertySymbol { SetMethod.IsInitOnly: true }` must do. Altering it (is X → is not X) makes no test fail.
  - Line 227: decide what `new MemberSerializationInfo( IsProperty: isProperty, IsWritable: true, IsReadable: true, IsInitOnly: isInitOnl…` must do. Altering it (return x → return default) makes no test fail.
  - Line 229: decide what `true` must do. Altering it (true → false) makes no test fail.
  - Lines 223–224 never run under any test: read them, or have them exercised.

### 23. GeneratedFormatterCollector.GetInstanceMembers(INamedTypeSymbol)

`src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs`, changed lines 246–260, 262–265, 267–272, 274–275

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (27 line(s))
  - the changed lines touch a condition, a loop
- **How much breaks if it is wrong**
  - 4 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - breaking line 253 made 3 test(s) fail (measured)
- **Whether anything would notice**
  - 19 of 21 changed executable line(s) ran under the tests; never ran: 263–264
  - 6 of 6 deliberate break(s) made a test fail
  - 6 of those were noticed only by tests that came with the change, none by a test that existed before
  - lines 1–300 could not be undone on their own: the code does not compile without the rest of the change
  - 13 possible break(s) were not tried (budget, or on a line no test executes)
- **For the owner to check**
  - Lines 263–264 never run under any test: read them, or have them exercised.

### 24. GeneratedFormatterCollector.IsPublicReadWrite(ISymbol)

`src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs`, changed lines 277–284

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (8 line(s))
  - the changed lines touch a condition
- **How much breaks if it is wrong**
  - 4 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - breaking line 277 made 1 test(s) fail (measured)
- **Whether anything would notice**
  - 6 of 8 changed executable line(s) ran under the tests; never ran: 282–283
  - 2 of 6 deliberate break(s) made a test fail; unnoticed: line 281 (&& → ||); line 281 (a && b → a); line 280 (&& → ||)
  - 2 of those were noticed only by tests that came with the change, none by a test that existed before
  - lines 1–300 could not be undone on their own: the code does not compile without the rest of the change
  - 14 possible break(s) were not tried (budget, or on a line no test executes)
- **For the owner to check**
  - Line 281: decide what `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` must do. Altering it (&& → ||) makes no test fail.
  - Line 281: decide what `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` must do. Altering it (a && b → a) makes no test fail.
  - Line 280: decide what `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` must do. Altering it (&& → ||) makes no test fail.
  - Lines 282–283 never run under any test: read them, or have them exercised.

### 25. GeneratedFormatterCollector.CreateSurrogateMembers(INamedTypeSymbol, INamedTypeSymbol, ReferenceSymbols, Action<Diagnostic>?, Location, out bool)

`src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs`, changed lines 143–159, 161–167, 169–179, 181–187, 189–198, 200–204, 206–209, 211–213

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (64 line(s))
  - the changed lines touch a condition, a loop, null handling
- **How much breaks if it is wrong**
  - 2 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - breaking line 164 made 3 test(s) fail (measured)
- **Whether anything would notice**
  - 34 of 48 changed executable line(s) ran under the tests; never ran: 165–166, 172–178, 201–203, 211–212
  - 5 of 5 deliberate break(s) made a test fail
  - 5 of those were noticed only by tests that came with the change, none by a test that existed before
  - lines 1–300 could not be undone on their own: the code does not compile without the rest of the change
  - 31 possible break(s) were not tried (budget, or on a line no test executes)
- **For the owner to check**
  - Lines 165–166, 172–178, 201–203, 211–212 never run under any test: read them, or have them exercised.

### 26. GeneratedFormatterCollector.Collect(Compilation, AnalyzerOptions, ReferenceSymbols, Action<Diagnostic>?, INamedTypeSymbol, INamedTypeSymbol?, CancellationToken)

`src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs`, changed lines 24–45, 47–51, 53–61, 63–74, 76–81, 83–85, 87–97, 99–102, 104–119, 121–126

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (94 line(s))
  - the changed lines touch a condition, null handling
- **How much breaks if it is wrong**
  - 1 member(s) depend on it, by the compiler's account (an upper bound), among them public API such as MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)
  - breaking line 47 made 6 test(s) fail (measured)
- **Whether anything would notice**
  - 38 of 67 changed executable line(s) ran under the tests; never ran: 78–80, 85, 90–93, 104–119, 121–125
  - 5 of 6 deliberate break(s) made a test fail; unnoticed: line 55 (&& → ||)
  - 5 of those were noticed only by tests that came with the change, none by a test that existed before
  - lines 1–300 could not be undone on their own: the code does not compile without the rest of the change
  - 57 possible break(s) were not tried (budget, or on a line no test executes)
- **For the owner to check**
  - Line 55: decide what `i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, openFormatterInterface)` must do. Altering it (&& → ||) makes no test fail.
  - Lines 78–80, 85, 90–93, 104–119, 121–125 never run under any test: read them, or have them exercised.

### 27. 7 of the 13 tests that came with the change

- **Why it is here**
  - they were not shown to fail without the change, so they do not demonstrate it
- **Whether anything would notice**
  - 7 cannot be compiled against the old code, so they could not be run there: GeneratedFormatterForExternalTypesTests.ContractlessClassNullRoundTrips, GeneratedFormatterForExternalTypesTests.ContractlessClassRoundTrips, GeneratedFormatterForExternalTypesTests.ContractlessClassUsesStringKeysForPublicReadWriteMembers, GeneratedFormatterForExternalTypesTests.ContractlessStructRoundTrips, …
- **For the owner to check**
  - Read these tests: what they assert may hold whether or not the change is right.

### 28. GeneratedMessagePackFormatterAttribute

`src/MessagePack.Annotations/Attributes.cs`, changed lines 143–167, 177

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - new code (26 line(s))
- **How much breaks if it is wrong**
  - nothing in the analysed code depends on it
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - lines 142–177 can be put back as they were and every test still passes: no test needs this part of the change
- **For the owner to check**
  - Lines 142–177: with the old code back, no test fails. Find out what this part of the change is for, and whether the requirement asks for it.
  - Read the changed lines: no measurement stands in for it.

### 29. MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)

`src/MessagePack.SourceGenerator/MessagePackGenerator.cs`, changed lines 29–34, 37, 60, 65, 70–76, 115–126, 128–129, 131

- **Why it is here**
  - a mistake in it would go unnoticed, and nothing else was found against it
- **How likely it is to be wrong (indications, not a measurement)**
  - modifies code that already existed (31 line(s))
  - the changed lines touch a condition, a loop
  - old and new code give the same result on 12 input(s) (probes/P6.cs)
- **How much breaks if it is wrong**
  - nothing in the analysed code depends on it
  - breaking line 34 made 6 test(s) fail (measured)
  - it is public API: code outside this repository may depend on it
- **Whether anything would notice**
  - 26 of 29 changed executable line(s) ran under the tests; never ran: 73–75
  - 3 of 3 deliberate break(s) made a test fail
  - 3 of those were noticed only by tests that came with the change, none by a test that existed before
  - lines 29–35 could not be undone on their own: the code does not compile without the rest of the change
  - 5 possible break(s) were not tried (budget, or on a line no test executes)
- **For the owner to check**
  - Lines 73–75 never run under any test: read them, or have them exercised.

## 2. What changed

| Role | Files | Lines added | Lines removed |
|---|---:|---:|---:|
| source | 8 | 442 | 4 |
| docs | 7 | 163 | 0 |
| test | 6 | 397 | 0 |
| build-config | 3 | 24 | 1 |
| generated | 2 | 6 | 2 |
| template | 2 | 2 | 2 |

Groups of files that use the same edit. Similar is not identical: each group is split into the variants that are exactly the same edit.

- **2 generated files**, group `12d48c953f`, e.g. `src/MessagePack.SourceGenerator/Transforms/FormatterTemplate.cs`: all exactly the same edit
  - `+this.Write(" ");`
  - `+this.Write("\t");`
  - `+this.Write(this.ToStringHelper.ToStringWithCulture(Info.IsUserDeclaredFormatter ? "partial class" : "internal sealed class"));`
  - `-this.Write("\tinternal sealed class ");`

## 3. Changed symbols, from the bottom of the dependency graph up

Graph: 6529 nodes, 20238 edges, 37 layers, over 20 projects. Layer 0 depends on nothing else in the code; a higher layer depends on lower ones.

| Layer | Symbol | Change | Public API | Dependents | Tests that can reach it | Changed lines run / not run | Measured |
|---:|---|---|---|---:|---:|---|---|
| 0 | `Constants.GeneratedMessagePackFormatterAttributeName` | added | no | 3 | 92 | 0 / 0 | not-measured |
| 0 | `Constants.GeneratedMessagePackFormatterSurrogatePropertyName` | added | no | 1 | 90 | 0 / 0 | not-measured |
| 0 | `GeneratedFormatterCollector` | added | no | 1 | 90 | 0 / 0 | not-measured |
| 0 | `GeneratedFormatterCollector.BinaryWriteTypeFormat` | added | no | 5 | 90 | 4 / 0 | executed |
| 0 | `GeneratedFormatterCollector.GetInstanceMembers(INamedTypeSymbol)` | added | no | 4 | 90 | 19 / 2 | partly-executed |
| 0 | `GeneratedFormatterCollector.GetMemberType(ISymbol)` | added | no | 5 | 90 | 4 / 2 | partly-executed |
| 0 | `GeneratedFormatterCollector.IsPartial(INamedTypeSymbol, CancellationToken)` | added | no | 2 | 90 | 6 / 0 | executed |
| 0 | `GeneratedFormatterCollector.IsPublicReadWrite(ISymbol)` | added | no | 4 | 90 | 6 / 2 | partly-executed |
| 0 | `GeneratedFormatterRequest` | added | yes | 1 | 90 | 1 / 0 | executed |
| 0 | `GeneratedMessagePackFormatterAttribute` | added | yes | 0 | 1 | 0 / 0 | not-measured |
| 0 | `GeneratedMessagePackFormatterAttribute.SurrogateType` | added | yes | 0 | 1 | 0 / 1 | not-executed |
| 0 | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupportedId` | added | yes | 34 | 136 | 0 / 0 | not-measured |
| 0 | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartialId` | added | yes | 34 | 136 | 0 / 0 | not-measured |
| 0 | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustImplementFormatterId` | added | yes | 34 | 136 | 0 / 0 | not-measured |
| 0 | `MsgPack00xMessagePackAnalyzer.SurrogateMemberNotFoundId` | added | yes | 34 | 136 | 0 / 0 | not-measured |
| 0 | `ObjectSerializationInfo.IsUserDeclaredFormatter` | added | yes | 58 | 138 | 1 / 0 | executed |
| 1 | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported` | added | yes | 33 | 136 | 8 / 0 | executed |
| 1 | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial` | added | yes | 33 | 136 | 8 / 0 | executed |
| 1 | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustImplementFormatter` | added | yes | 33 | 136 | 8 / 0 | executed |
| 1 | `MsgPack00xMessagePackAnalyzer.SurrogateMemberNotFound` | added | yes | 33 | 136 | 8 / 0 | executed |
| 2 | `MsgPack00xMessagePackAnalyzer.SupportedDiagnostics` | modified | yes | 32 | 136 | 5 / 0 | executed |
| 6 | `GeneratedFormatterCollector.CreateMember(ISymbol, int, string)` | added | no | 4 | 90 | 26 / 2 | partly-executed |
| 7 | `GeneratedFormatterCollector.CreateContractlessMembers(INamedTypeSymbol)` | added | no | 2 | 90 | 0 / 9 | not-executed |
| 8 | `ObjectSerializationInfo.Equals(ObjectSerializationInfo?)` | modified | yes | 57 | 138 | 0 / 1 | not-executed |
| 13 | `FormatterTemplate.TransformText()` | modified | yes | 3 | 90 | 3 / 0 | executed |
| 13 | `StringKeyFormatterTemplate.TransformText()` | modified | yes | 3 | 90 | 3 / 0 | executed |
| 14 | `GeneratedFormatterCollector.CreateSurrogateMembers(INamedTypeSymbol, INamedTypeSymbol, ReferenceSymbols, Action<Diagnostic>?, Location, out bool)` | added | no | 2 | 90 | 34 / 14 | partly-executed |
| 14 | `GeneratorUtilities.GetAnalyzerOption(IncrementalGeneratorInitializationContext)` | modified | no | 2 | 92 | 3 / 0 | executed |
| 15 | `GeneratedFormatterCollector.Collect(Compilation, AnalyzerOptions, ReferenceSymbols, Action<Diagnostic>?, INamedTypeSymbol, INamedTypeSymbol?, CancellationToken)` | added | no | 1 | 90 | 38 / 29 | partly-executed |
| 16 | `MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)` | modified | yes | 0 | 90 | 26 / 3 | partly-executed |

Plus 40 test methods and test helpers added or modified by the change (section 4 says what they prove).

## 4. What was checked

| ID | Result | Finding | Checked by |
|---|---|---|---|
| E1 | info | 28 changed files fall into 27 groups of similar edits; the largest has 2 files in 1 exact variant(s) | git diff; files grouped by the distinct lines they edit, then split by exact edit (deterministic) |
| E2 | info | 30 non-test symbols changed across layers 0–16; the most depended-on has 58 dependent member(s); 17 public API symbols touched | Roslyn semantic model over 20 projects (6529 nodes, 20238 edges) |
| E3 | info | Old and new code were both built with .NET SDK 9.0.321, not the 8.0.428 the repository selects, because its source generator needs a newer compiler | dotnet --version; compiler warning CS9057 |
| E4 | pass | 7 test project(s) and their dependencies compile with the change | dotnet build (new code) |
| E5 | pass | 1274 of 1280 pre-existing tests pass on both old and new code; 0 regressed | dotnet test on old and new code, compared test by test |
| E6 | warn | 44 of those tests are fed random data, so their inputs differ on every run; they were compared per test method, not input by input | dotnet test on old and new code |
| E7 | pass | 13 new and 0 modified tests; 13 pass on the new code, 0 fail | dotnet test (new code) |
| E8 | warn | 6 of 13 of those tests fail on the old code (they demonstrate the change); 0 pass without it; 7 cannot compile without it | dotnet test on old source with the change's test files applied |
| E9 | warn | 211 of 276 changed executable source lines were executed by the test suite; 65 were never executed | Microsoft Code Coverage during dotnet test (new code) |
| E10 | warn | 36 of 55 deliberate breaks of changed lines were caught by a test; 19 went unnoticed; 51 more are on lines no test executes; 80 were not tried (budget) | each changed line altered in turn, then dotnet test (mutation testing) |
| E11 | warn | 8 part(s) of the change were put back as they were, one at a time: 0 are needed by at least one test, 3 can be undone with every test still passing, 5 do not compile without the rest of the change | the old lines of one part restored in the new code, then dotnet test |
| E12 | info | 8 of 8 tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) were accepted: they compile, pass, and now execute 5 previously unexecuted line(s) and catch 4 of the 19 unnoticed break(s); 21 gap(s) were not asked for (budget) | gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) wrote them; dotnet test and coverage checked each one, and each accepted test was run against every other unnoticed break |
| E13 | warn | 6 of 6 modified unit(s) were run on the old and the new code with the same 66 input(s): 60 give the same result, 6 differ (6 changed) | probes written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line); the same probe run twice on the old code and once on the new |
| E14 | warn | 12 unit(s) were attacked with tests written to break them: 3 resisted (14 tests), 0 with a demonstrated regression, 2 with a failing test nothing can arbitrate, 7 failing on the old code too. 1 test(s) kept from earlier runs because they showed a regression were run again: 0 still fail on the new code and pass on the old | tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line); each file is built and run here, and a failing test is run on the old code, which decides whether the change caused it |
| E15 | warn | 2542 input(s) nobody chose (151 taken from the existing tests, 2391 altered mechanically) were run on the old and the new code: 2542 give the same result, 0 differ. The inputs drove 58 of the change's 276 executable line(s), less than half: what they do not reach, they say nothing about. 9 input(s) that came with the change were run on the new code alone: 0 make the code under test crash, 0 get output that does not compile with no error reported. on 40 input(s) the code under test crashes on the old and on the new code alike (for example i032+a3 GenerationTests.cs:785 + an attribute argument made to repeat its sibling's) | inputs built by AYA from the tests of tests/MessagePack.SourceGenerator.Tests/MessagePack.SourceGenerator.Tests.csproj, with no model involved; the harness that feeds them was written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) and run twice on the old code and once on the new |
| E16 | info | OPINION, not a measurement. gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) was asked whether 5 group(s) of differences the change's own text does not explain were meant: it reads 5 as what the change set out to do, 0 as not meant, 0 as artefacts of the build or the run and 0 as unclear | 1 session(s); it was given the text of the requirement, supplied by the owner, the commit message(s) of the change, 123 line(s) of documentation the change added, 57 comment line(s) the change added to the code. A reading moves nothing down the list |
| E17 | warn | OPINION, bounded by measurements. A model read 5 thing(s) the requirement asks for: 0 are shown by a test that fails without the change, 3 the change appears to do with no such test, 2 it does not address | the words of each thing asked for were checked to be in the requirement, and a test named as showing one was checked to be a test that fails on the old code; the reading itself is the model's |
| E18 | info | 1 member(s) depend on the change and are run by no original test; 0 were probed within a budget of 12 session(s) and 0 could be compared on old and new code | probes written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line); 0 session(s) used |

E10 detail:
```
src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:55  && → ||  in  i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, openFormatterInterface)
src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:219  is X → is not X  in  target is IPropertySymbol { SetMethod.IsInitOnly: true }
src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:227  return x → return default  in  new MemberSerializationInfo( IsProperty: isProperty, IsWritable: true, IsReadable: true, IsInitOnly: isInitOnl…
src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:229  true → false  in  true
src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:230  true → false  in  true
src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:236  call passed as argument → default  in  memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:237  call passed as argument → default  in  memberType.ToDisplayString(BinaryWriteTypeFormat)
src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:281  && → ||  in  property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…
src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:281  a && b → a  in  property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…
src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:280  && → ||  in  property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…
src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:280  a && b → a  in  property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…
src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs:324  true → false  in  true
```

E11 detail:
```
src/MessagePack.Annotations/Attributes.cs:142–177  no test fails with the old lines back
src/MessagePack.SourceGenerator/GeneratorUtilities.cs:35–36  no test fails with the old lines back
src/MessagePack.SourceGenerator/Transforms/FormatterTemplate.cs:33–35  no test fails with the old lines back
```

E14 detail:
```
A1 MsgPack00xMessagePackAnalyzer.SupportedDiagnostics: fails-on-both — 2 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 2 other(s) pass
A3 FormatterTemplate.TransformText(): fails-on-both — 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass
A4 GeneratorUtilities.GetAnalyzerOption(IncrementalGeneratorInitializationContext): fails-on-both — 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass
A5 ObjectSerializationInfo.IsUserDeclaredFormatter: fails-on-both — 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 1 other(s) pass
A6 GeneratedFormatterCollector.BinaryWriteTypeFormat: fails-on-both — 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass
A7 StringKeyFormatterTemplate.TransformText(): fails-on-both — 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass
A8 GeneratedFormatterCollector.CreateContractlessMembers(INamedTypeSymbol): possible-defect — 5 test(s) fail on the new code and the old code cannot judge them (they need code the change added): AyaA8Attack.Contractless_RequiredProperty_CompilesCleanly, AyaA8Attack.Contractless_IndexerIsIgnored_CompilesCleanly, AyaA8Attack.Contractless_StructWithMixedMembers_CompilesCleanly, AyaA8Attack.Contractless_InitOnlyProperty_CompilesCleanly, AyaA8Attack.Contractless_OverriddenAndShadowedBaseMembers_CompilesCleanly
A9 GeneratedFormatterRequest: fails-on-both — 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass
A12 MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial: possible-defect — 1 test(s) fail on the new code and the old code cannot judge them (they need code the change added): AyaA12Attack.GeneratedFormatterMustBePartial_IsInSupportedDiagnosticsWithoutDuplicateIds
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
not-demonstrated: "I need to have a source-generated `IMessagePackFormatter` for type `ProjectA.MyType` generated **inside Project B** instead of Project A, because the latter has no (and cannot have) reference to MessagePack nuget package." — The change adds GeneratedFormatterCollector to generate formatters for external types, but all tests in GeneratedFormatterForExternalTypesTests could not be compiled against the old code and the diagnostic tests that fail on the old code only use same-assembly types with hand-written formatter members.
not-demonstrated: "Add a `[GeneratedMessagePackFormatter]` attribute that will work for partial classes implementing `IMessagePackFormatter<T>`." — The change adds GeneratedMessagePackFormatterAttribute and collector support, but the tests that fail on the old code only verify error diagnostics rather than successful generation for a valid partial formatter class.
not-demonstrated: "The class does not even need to declare a body (no `{` and `}`), because the actual implementation is source-generated by `MessagePack.SourceGenerator.MessagePackGenerator` as usual." — None of the listed tests declare a bodyless partial class without braces, and the generation tests could not be compiled against the old code.
not-addressed: "Allow the generator and analyzers to work with user-defined versions of the MessagePack attributes." — The change uses contractless mode or a SurrogateType in the consuming assembly instead of recognizing user-defined copies of MessagePack attributes.
not-addressed: "Source-generate the attributes themselves using [`IncrementalGeneratorInitializationContext.RegisterPostInitializationOutput`](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md#outputting-values) instead of them existing in the `MessagePack.Annotations` assembly." — The new GeneratedMessagePackFormatterAttribute is added to MessagePack.Annotations rather than being emitted via RegisterPostInitializationOutput.
```

Tests that came with the change, run against the old code:

| Test | Origin | On new code | On old code | Meaning |
|---|---|---|---|---|
| `GeneratedFormatterForExternalTypesTests.ContractlessClassNullRoundTrips` | new | Passed | build-failed | does not compile without the change |
| `GeneratedFormatterForExternalTypesTests.ContractlessClassRoundTrips` | new | Passed | build-failed | does not compile without the change |
| `GeneratedFormatterForExternalTypesTests.ContractlessClassUsesStringKeysForPublicReadWriteMembers` | new | Passed | build-failed | does not compile without the change |
| `GeneratedFormatterForExternalTypesTests.ContractlessStructRoundTrips` | new | Passed | build-failed | does not compile without the change |
| `GeneratedFormatterForExternalTypesTests.GeneratedFormattersAreRegisteredInGeneratedResolver` | new | Passed | build-failed | does not compile without the change |
| `GeneratedFormatterForExternalTypesTests.MessagePackObjectCanContainExternalTypes` | new | Passed | build-failed | does not compile without the change |
| `GeneratedFormatterForExternalTypesTests.SurrogateControlsKeysAndMembership` | new | Passed | build-failed | does not compile without the change |
| `GeneratedMessagePackFormatterTests.FormattedTypeMustNotBeAbstract` | new | Passed | Failed | fails without the change: it demonstrates it |
| `GeneratedMessagePackFormatterTests.FormatterMustBePartial` | new | Passed | Failed | fails without the change: it demonstrates it |
| `GeneratedMessagePackFormatterTests.FormatterMustImplementFormatterOfT` | new | Passed | Failed | fails without the change: it demonstrates it |
| `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` | new | Passed | Failed | fails without the change: it demonstrates it |
| `GeneratedMessagePackFormatterTests.SurrogateMemberMustHaveMatchingType` | new | Passed | Failed | fails without the change: it demonstrates it |
| `GeneratedMessagePackFormatterTests.SurrogateMemberMustMatchFormattedType` | new | Passed | Failed | fails without the change: it demonstrates it |

Test runs:

| Code under test | Project | Compiled | Passed | Failed | Skipped or not run | Seconds | Problem |
|---|---|---|---:|---:|---:|---:|---|
| new | MessagePack.AspNetCoreMvcFormatter.Tests | yes | 6 | 0 | 0 | 16 |  |
| new | MessagePack.Experimental.Tests | yes | 95 | 0 | 0 | 7.1 |  |
| new | MessagePack.GeneratedCode.Tests | yes | 11 | 0 | 0 | 7.2 |  |
| new | MessagePack.SourceGenerator.ExecutionTests | yes | 21 | 0 | 0 | 6.5 |  |
| new | MessagePack.SourceGenerator.MapModeExecutionTests | yes | 20 | 0 | 0 | 5.4 |  |
| new | MessagePack.SourceGenerator.Tests | yes | 173 | 0 | 0 | 12.4 |  |
| new | MessagePack.Tests | yes | 961 | 0 | 6 | 12.6 |  |
| old | MessagePack.AspNetCoreMvcFormatter.Tests | yes | 6 | 0 | 0 | 7 |  |
| old | MessagePack.Experimental.Tests | yes | 95 | 0 | 0 | 5 |  |
| old | MessagePack.GeneratedCode.Tests | yes | 11 | 0 | 0 | 5.4 |  |
| old | MessagePack.SourceGenerator.ExecutionTests | yes | 14 | 0 | 0 | 4.1 |  |
| old | MessagePack.SourceGenerator.MapModeExecutionTests | yes | 20 | 0 | 0 | 4.2 |  |
| old | MessagePack.SourceGenerator.Tests | yes | 167 | 0 | 0 | 9.8 |  |
| old | MessagePack.Tests | yes | 961 | 0 | 6 | 8.9 |  |
| old + new tests | MessagePack.SourceGenerator.ExecutionTests | no | 0 | 0 | 0 | 4.2 | did not compile |
| old + new tests | MessagePack.SourceGenerator.Tests | yes | 0 | 6 | 0 | 5.8 |  |

Deliberate breaks of the changed lines. Each row alters one line and re-runs the tests that execute it:

| Break | Where | Alteration | Original code | Result |
|---|---|---|---|---|
| M4 | GeneratedFormatterCollector.cs:55 | && → \|\| | `i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, openFormatterInterface)` | **not noticed by any test** |
| M106 | GeneratedFormatterCollector.cs:219 | is X → is not X | `target is IPropertySymbol { SetMethod.IsInitOnly: true }` | **not noticed by any test** |
| M109 | GeneratedFormatterCollector.cs:227 | return x → return default | `new MemberSerializationInfo( IsProperty: isProperty, IsWritable: true, IsReadable: true, IsInitOnly: isInitOnl…` | **not noticed by any test** |
| M110 | GeneratedFormatterCollector.cs:229 | true → false | `true` | **not noticed by any test** |
| M111 | GeneratedFormatterCollector.cs:230 | true → false | `true` | **not noticed by any test** |
| M112 | GeneratedFormatterCollector.cs:236 | call passed as argument → default | `memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)` | **not noticed by any test** |
| M113 | GeneratedFormatterCollector.cs:237 | call passed as argument → default | `memberType.ToDisplayString(BinaryWriteTypeFormat)` | **not noticed by any test** |
| M137 | GeneratedFormatterCollector.cs:280 | && → \|\| | `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` | **not noticed by any test** |
| M138 | GeneratedFormatterCollector.cs:280 | a && b → a | `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` | **not noticed by any test** |
| M134 | GeneratedFormatterCollector.cs:281 | && → \|\| | `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` | **not noticed by any test** |
| M135 | GeneratedFormatterCollector.cs:281 | a && b → a | `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` | **not noticed by any test** |
| M155 | MsgPack00xMessagePackAnalyzer.cs:324 | true → false | `true` | **not noticed by any test** |
| M156 | MsgPack00xMessagePackAnalyzer.cs:325 | call passed as argument → default | `AnalyzerUtilities.GetHelpLink(GeneratedFormatterMustBePartialId)` | **not noticed by any test** |
| M157 | MsgPack00xMessagePackAnalyzer.cs:333 | true → false | `true` | **not noticed by any test** |
| M158 | MsgPack00xMessagePackAnalyzer.cs:334 | call passed as argument → default | `AnalyzerUtilities.GetHelpLink(GeneratedFormatterMustImplementFormatterId)` | **not noticed by any test** |
| M159 | MsgPack00xMessagePackAnalyzer.cs:342 | true → false | `true` | **not noticed by any test** |
| M160 | MsgPack00xMessagePackAnalyzer.cs:343 | call passed as argument → default | `AnalyzerUtilities.GetHelpLink(GeneratedFormatterDataTypeNotSupportedId)` | **not noticed by any test** |
| M161 | MsgPack00xMessagePackAnalyzer.cs:351 | true → false | `true` | **not noticed by any test** |
| M162 | MsgPack00xMessagePackAnalyzer.cs:352 | call passed as argument → default | `AnalyzerUtilities.GetHelpLink(SurrogateMemberNotFoundId)` | **not noticed by any test** |
| M187 | MessagePackGenerator.cs:74 | statement removed | `modelPerType.Add(generatedModel);` | no test executes this line |
| M37 | GeneratedFormatterCollector.cs:79 | statement removed | `reportDiagnostic?.Invoke(Diagnostic.Create(MsgPack00xMessagePackAnalyzer.NoDeserializingConstructor, location)…` | no test executes this line |
| M38 | GeneratedFormatterCollector.cs:79 | call passed as argument → default | `Diagnostic.Create(MsgPack00xMessagePackAnalyzer.NoDeserializingConstructor, location)` | no test executes this line |
| M42 | GeneratedFormatterCollector.cs:85 | && → \|\| | `m.ExplicitInterfaceImplementations.Length > 0 && (m.Name == "OnBeforeSerialize" \|\| m.Name == "OnAfterDeseriali…` | no test executes this line |
| M43 | GeneratedFormatterCollector.cs:85 | a && b → a | `m.ExplicitInterfaceImplementations.Length > 0 && (m.Name == "OnBeforeSerialize" \|\| m.Name == "OnAfterDeseriali…` | no test executes this line |
| M44 | GeneratedFormatterCollector.cs:85 | a && b → b | `m.ExplicitInterfaceImplementations.Length > 0 && (m.Name == "OnBeforeSerialize" \|\| m.Name == "OnAfterDeseriali…` | no test executes this line |
| M45 | GeneratedFormatterCollector.cs:85 | > → <= | `m.ExplicitInterfaceImplementations.Length > 0` | no test executes this line |
| M46 | GeneratedFormatterCollector.cs:85 | 0 → 1 | `0` | no test executes this line |
| M47 | GeneratedFormatterCollector.cs:85 | \|\| → && | `m.Name == "OnBeforeSerialize" \|\| m.Name == "OnAfterDeserialize"` | no test executes this line |
| M48 | GeneratedFormatterCollector.cs:85 | a \|\| b → a | `m.Name == "OnBeforeSerialize" \|\| m.Name == "OnAfterDeserialize"` | no test executes this line |
| M49 | GeneratedFormatterCollector.cs:85 | a \|\| b → b | `m.Name == "OnBeforeSerialize" \|\| m.Name == "OnAfterDeserialize"` | no test executes this line |
| M50 | GeneratedFormatterCollector.cs:85 | == → != | `m.Name == "OnBeforeSerialize"` | no test executes this line |
| M51 | GeneratedFormatterCollector.cs:85 | == → != | `m.Name == "OnAfterDeserialize"` | no test executes this line |
| M53 | GeneratedFormatterCollector.cs:91 | statement removed | `members = CreateContractlessMembers(type);` | no test executes this line |
| M54 | GeneratedFormatterCollector.cs:92 | statement removed | `isIntKey = false;` | no test executes this line |
| M55 | GeneratedFormatterCollector.cs:92 | false → true | `false` | no test executes this line |
| M58 | GeneratedFormatterCollector.cs:107 | false → true | `false` | no test executes this line |
| M59 | GeneratedFormatterCollector.cs:108 | call passed as argument → default | `Array.Empty<GenericTypeParameterInfo>()` | no test executes this line |
| M60 | GeneratedFormatterCollector.cs:109 | call passed as argument → default | `Array.Empty<MemberSerializationInfo>()` | no test executes this line |
| M61 | GeneratedFormatterCollector.cs:111 | call passed as argument → default | `members.ToArray()` | no test executes this line |
| M62 | GeneratedFormatterCollector.cs:118 | true → false | `true` | no test executes this line |
| M63 | GeneratedFormatterCollector.cs:121 | return x → return default | `FullModel.Empty with { ObjectInfos = ImmutableSortedSet.Create(info), Options = options, }` | no test executes this line |
| M64 | GeneratedFormatterCollector.cs:135 | 0 → 1 | `0` | no test executes this line |
| M65 | GeneratedFormatterCollector.cs:135 | < → >= | `i < targets.Count` | no test executes this line |
| M66 | GeneratedFormatterCollector.cs:137 | statement removed | `members.Add(CreateMember(targets[i], intKey: i, stringKey: targets[i].Name));` | no test executes this line |
| M67 | GeneratedFormatterCollector.cs:137 | call passed as argument → default | `CreateMember(targets[i], intKey: i, stringKey: targets[i].Name)` | no test executes this line |
| M68 | GeneratedFormatterCollector.cs:140 | return x → return default | `members` | no test executes this line |
| M72 | GeneratedFormatterCollector.cs:166 | continue removed | `continue;` | no test executes this line |
| M163 | ObjectSerializationInfo.cs:168 | && → \|\| | `base.Equals(other) && IsClass == other.IsClass && IsUserDeclaredFormatter == other.IsUserDeclaredFormatter` | no test executes this line |
| M164 | ObjectSerializationInfo.cs:168 | a && b → a | `base.Equals(other) && IsClass == other.IsClass && IsUserDeclaredFormatter == other.IsUserDeclaredFormatter` | no test executes this line |
| M165 | ObjectSerializationInfo.cs:168 | a && b → b | `base.Equals(other) && IsClass == other.IsClass && IsUserDeclaredFormatter == other.IsUserDeclaredFormatter` | no test executes this line |
| M166 | ObjectSerializationInfo.cs:168 | == → != | `IsUserDeclaredFormatter == other.IsUserDeclaredFormatter` | no test executes this line |
| M77 | GeneratedFormatterCollector.cs:173 | statement removed | `reportDiagnostic?.Invoke(Diagnostic.Create( MsgPack00xMessagePackAnalyzer.MemberNeedsKey, surrogateMember.Loca…` | no test executes this line |
| M78 | GeneratedFormatterCollector.cs:173 | call passed as argument → default | `Diagnostic.Create( MsgPack00xMessagePackAnalyzer.MemberNeedsKey, surrogateMember.Locations.FirstOrDefault() ??…` | no test executes this line |
| M99 | GeneratedFormatterCollector.cs:202 | statement removed | `reportDiagnostic?.Invoke(Diagnostic.Create(MsgPack00xMessagePackAnalyzer.KeysMustBeUnique, surrogateMember.Loc…` | no test executes this line |
| M100 | GeneratedFormatterCollector.cs:202 | call passed as argument → default | `Diagnostic.Create(MsgPack00xMessagePackAnalyzer.KeysMustBeUnique, surrogateMember.Locations.FirstOrDefault() ?…` | no test executes this line |
| M103 | GeneratedFormatterCollector.cs:211 | statement removed | `isIntKey = keyKindIsInt ?? false;` | no test executes this line |
| M104 | GeneratedFormatterCollector.cs:211 | false → true | `false` | no test executes this line |
| M105 | GeneratedFormatterCollector.cs:212 | return x → return default | `members` | no test executes this line |
| M108 | GeneratedFormatterCollector.cs:224 | false → true | `false` | no test executes this line |
| M129 | GeneratedFormatterCollector.cs:264 | continue removed | `continue;` | no test executes this line |
| M143 | GeneratedFormatterCollector.cs:282 | && → \|\| | `field.DeclaredAccessibility == Accessibility.Public && !field.IsReadOnly && !field.IsConst` | no test executes this line |
| M144 | GeneratedFormatterCollector.cs:282 | a && b → a | `field.DeclaredAccessibility == Accessibility.Public && !field.IsReadOnly && !field.IsConst` | no test executes this line |
| M145 | GeneratedFormatterCollector.cs:282 | a && b → b | `field.DeclaredAccessibility == Accessibility.Public && !field.IsReadOnly && !field.IsConst` | no test executes this line |
| M146 | GeneratedFormatterCollector.cs:282 | && → \|\| | `field.DeclaredAccessibility == Accessibility.Public && !field.IsReadOnly` | no test executes this line |
| M147 | GeneratedFormatterCollector.cs:282 | a && b → a | `field.DeclaredAccessibility == Accessibility.Public && !field.IsReadOnly` | no test executes this line |
| M148 | GeneratedFormatterCollector.cs:282 | a && b → b | `field.DeclaredAccessibility == Accessibility.Public && !field.IsReadOnly` | no test executes this line |
| M149 | GeneratedFormatterCollector.cs:282 | == → != | `field.DeclaredAccessibility == Accessibility.Public` | no test executes this line |
| M150 | GeneratedFormatterCollector.cs:282 | !x → x | `!field.IsReadOnly` | no test executes this line |
| M151 | GeneratedFormatterCollector.cs:282 | !x → x | `!field.IsConst` | no test executes this line |
| M152 | GeneratedFormatterCollector.cs:283 | false → true | `false` | no test executes this line |
| M167 | FormatterTemplate.cs:33 | statement removed | `this.Write("\t");` | caught by `GenerationTests.AdditionalAllowTypes` and 53 more |
| M168 | FormatterTemplate.cs:34 | statement removed | `this.Write(this.ToStringHelper.ToStringWithCulture(Info.IsUserDeclaredFormatter ? "partial class" : "internal …` | caught by `GenerationTests.AdditionalAllowTypes` and 53 more |
| M169 | FormatterTemplate.cs:34 | call passed as argument → default | `this.ToStringHelper.ToStringWithCulture(Info.IsUserDeclaredFormatter ? "partial class" : "internal sealed clas…` | caught by `GenerationTests.AdditionalAllowTypes` and 53 more |
| M170 | FormatterTemplate.cs:34 | c ? a : b → !c ? a : b | `Info.IsUserDeclaredFormatter` | caught by `GenerationTests.AdditionalAllowTypes` and 53 more |
| M182 | MessagePackGenerator.cs:34 | 0 → 1 | `0` | caught by `GeneratedMessagePackFormatterTests.FormattedTypeMustNotBeAbstract` and 5 more; only by tests that came with the change |
| M171 | FormatterTemplate.cs:35 | statement removed | `this.Write(" ");` | caught by `GenerationTests.AdditionalAllowTypes` and 53 more |
| M172 | StringKeyFormatterTemplate.cs:37 | statement removed | `this.Write("\t");` | caught by `CustomStringKeyFormatterTests.RecordWithPrimaryConstructor` and 20 more |
| M173 | StringKeyFormatterTemplate.cs:38 | statement removed | `this.Write(this.ToStringHelper.ToStringWithCulture(Info.IsUserDeclaredFormatter ? "partial class" : "internal …` | caught by `CustomStringKeyFormatterTests.RecordWithPrimaryConstructor` and 20 more |
| M174 | StringKeyFormatterTemplate.cs:38 | call passed as argument → default | `this.ToStringHelper.ToStringWithCulture(Info.IsUserDeclaredFormatter ? "partial class" : "internal sealed clas…` | caught by `CustomStringKeyFormatterTests.RecordWithPrimaryConstructor` and 20 more |
| M175 | StringKeyFormatterTemplate.cs:38 | c ? a : b → !c ? a : b | `Info.IsUserDeclaredFormatter` | caught by `CustomStringKeyFormatterTests.RecordWithPrimaryConstructor` and 20 more |
| M178 | GeneratorUtilities.cs:38 | a && b → a | `ctxt.SemanticModel.GetDeclaredSymbol(ctxt.Node, ct) is INamedTypeSymbol symbol && !symbol.GetAttributes().Any(…` | caught by `GeneratedMessagePackFormatterTests.FormattedTypeMustNotBeAbstract` and 4 more; only by tests that came with the change |
| M180 | GeneratorUtilities.cs:38 | !x → x | `!symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == $"{AttributeNamespace}.{GeneratedMessa…` | caught by `GeneratedMessagePackFormatterTests.FormattedTypeMustNotBeAbstract` and 14 more |
| M181 | GeneratorUtilities.cs:38 | == → != | `a.AttributeClass?.ToDisplayString() == $"{AttributeNamespace}.{GeneratedMessagePackFormatterAttributeName}"` | caught by `GeneratedMessagePackFormatterTests.FormattedTypeMustNotBeAbstract` and 4 more; only by tests that came with the change |
| M176 | StringKeyFormatterTemplate.cs:39 | statement removed | `this.Write(" ");` | caught by `CustomStringKeyFormatterTests.RecordWithPrimaryConstructor` and 20 more |
| M1 | GeneratedFormatterCollector.cs:47 | !x → x | `!IsPartial(formatter, cancellationToken)` | caught by `GeneratedMessagePackFormatterTests.FormattedTypeMustNotBeAbstract` and 5 more; only by tests that came with the change |
| M2 | GeneratedFormatterCollector.cs:49 | statement removed | `reportDiagnostic?.Invoke(Diagnostic.Create(MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial, loca…` | caught by `GeneratedMessagePackFormatterTests.FormatterMustBePartial`; only by tests that came with the change |
| M7 | GeneratedFormatterCollector.cs:57 | != → == | `formatterInterfaces.Length != 1` | caught by `GeneratedMessagePackFormatterTests.FormattedTypeMustNotBeAbstract` and 4 more; only by tests that came with the change |
| M9 | GeneratedFormatterCollector.cs:59 | statement removed | `reportDiagnostic?.Invoke(Diagnostic.Create(MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustImplementFormat…` | caught by `GeneratedMessagePackFormatterTests.FormatterMustImplementFormatterOfT`; only by tests that came with the change |
| M11 | GeneratedFormatterCollector.cs:63 | 0 → 1 | `0` | caught by `GeneratedMessagePackFormatterTests.FormattedTypeMustNotBeAbstract` and 3 more; only by tests that came with the change |
| M188 | MessagePackGenerator.cs:123 | if (c) → if (!c) | `ReferenceSymbols.TryCreate(s.Left.Right, out ReferenceSymbols? referenceSymbols)` | caught by `GeneratedMessagePackFormatterTests.FormattedTypeMustNotBeAbstract` and 5 more; only by tests that came with the change |
| M189 | MessagePackGenerator.cs:125 | statement removed | `GeneratedFormatterCollector.Collect(s.Left.Right, s.Right, referenceSymbols, diagnostics.Add, s.Left.Left.Form…` | caught by `GeneratedMessagePackFormatterTests.FormattedTypeMustNotBeAbstract` and 5 more; only by tests that came with the change |
| M71 | GeneratedFormatterCollector.cs:164 | if (c) → if (!c) | `attributes.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, referenceSymbols.IgnoreAttribute))` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 2 more; only by tests that came with the change |
| M73 | GeneratedFormatterCollector.cs:170 | is X → is not X | `keyAttribute is { ConstructorArguments.Length: 1 }` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 2 more; only by tests that came with the change |
| M76 | GeneratedFormatterCollector.cs:171 | is not X → is X | `key is not (int or string)` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 2 more; only by tests that came with the change |
| M79 | GeneratedFormatterCollector.cs:182 | statement removed | `keyKindIsInt ??= memberIsIntKey;` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 2 more; only by tests that came with the change |
| M80 | GeneratedFormatterCollector.cs:183 | != → == | `keyKindIsInt != memberIsIntKey` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 2 more; only by tests that came with the change |
| M114 | GeneratedFormatterCollector.cs:253 | && → \|\| | `current is not null && current.SpecialType != SpecialType.System_Object` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 2 more; only by tests that came with the change |
| M119 | GeneratedFormatterCollector.cs:257 | \|\| → && | `member.IsStatic \|\| member.IsImplicitlyDeclared \|\| member is not (IPropertySymbol or IFieldSymbol)` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 2 more; only by tests that came with the change |
| M126 | GeneratedFormatterCollector.cs:259 | continue removed | `continue;` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 2 more; only by tests that came with the change |
| M127 | GeneratedFormatterCollector.cs:262 | is X → is not X | `member is IPropertySymbol { IsIndexer: true }` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 2 more; only by tests that came with the change |
| M130 | GeneratedFormatterCollector.cs:267 | if (c) → if (!c) | `seen.Add(member.Name)` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 2 more; only by tests that came with the change |
| M131 | GeneratedFormatterCollector.cs:269 | statement removed | `result.Add(member);` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 2 more; only by tests that came with the change |
| M133 | GeneratedFormatterCollector.cs:277 | => x → => default | `member switch { IPropertySymbol property => property.DeclaredAccessibility == Accessibility.Public && property…` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt`; only by tests that came with the change |
| M140 | GeneratedFormatterCollector.cs:279 | == → != | `property.DeclaredAccessibility == Accessibility.Public` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt`; only by tests that came with the change |
| M153 | GeneratedFormatterCollector.cs:286 | => x → => default | `member switch { IPropertySymbol property => property.Type, IFieldSymbol field => field.Type, _ => throw new Ar…` | caught by `GeneratedMessagePackFormatterTests.SurrogateKeysMustNotMixStringAndInt` and 1 more; only by tests that came with the change |
| M154 | GeneratedFormatterCollector.cs:295 | return x → return default | `symbol.DeclaringSyntaxReferences .Select(r => r.GetSyntax(cancellationToken)) .OfType<ClassDeclarationSyntax>(…` | caught by `GeneratedMessagePackFormatterTests.FormattedTypeMustNotBeAbstract` and 4 more; only by tests that came with the change |
| M183 | MessagePackGenerator.cs:34 | == → != | `kvp.Key == GeneratedMessagePackFormatterSurrogatePropertyName` | not tried (budget) |
| M184 | MessagePackGenerator.cs:37 | call passed as argument → default | `unionTypes.Collect()` | does not compile; discarded |
| M185 | MessagePackGenerator.cs:37 | call passed as argument → default | `generatedFormatters.Collect()` | not tried (budget) |
| M177 | GeneratorUtilities.cs:38 | && → \|\| | `ctxt.SemanticModel.GetDeclaredSymbol(ctxt.Node, ct) is INamedTypeSymbol symbol && !symbol.GetAttributes().Any(…` | does not compile; discarded |
| M179 | GeneratorUtilities.cs:38 | a && b → b | `ctxt.SemanticModel.GetDeclaredSymbol(ctxt.Node, ct) is INamedTypeSymbol symbol && !symbol.GetAttributes().Any(…` | does not compile; discarded |
| M3 | GeneratedFormatterCollector.cs:49 | call passed as argument → default | `Diagnostic.Create(MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial, location, formatterName)` | not tried (budget) |
| M5 | GeneratedFormatterCollector.cs:55 | a && b → a | `i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, openFormatterInterface)` | not tried (budget) |
| M6 | GeneratedFormatterCollector.cs:55 | a && b → b | `i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, openFormatterInterface)` | not tried (budget) |
| M8 | GeneratedFormatterCollector.cs:57 | 1 → 2 | `1` | not tried (budget) |
| M10 | GeneratedFormatterCollector.cs:59 | call passed as argument → default | `Diagnostic.Create(MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustImplementFormatter, location, formatterN…` | not tried (budget) |
| M12 | GeneratedFormatterCollector.cs:63 | 0 → 1 | `0` | not tried (budget) |
| M19 | GeneratedFormatterCollector.cs:64 | is not X → is X | `dataTypeSymbol is not INamedTypeSymbol { IsGenericType: false, IsAbstract: false } type` | not tried (budget) |
| M20 | GeneratedFormatterCollector.cs:64 | false → true | `false` | not tried (budget) |
| M21 | GeneratedFormatterCollector.cs:64 | false → true | `false` | not tried (budget) |
| M16 | GeneratedFormatterCollector.cs:65 | \|\| → && | `dataTypeSymbol is not INamedTypeSymbol { IsGenericType: false, IsAbstract: false } type \|\| type.TypeKind is no…` | not tried (budget) |
| M17 | GeneratedFormatterCollector.cs:65 | a \|\| b → a | `dataTypeSymbol is not INamedTypeSymbol { IsGenericType: false, IsAbstract: false } type \|\| type.TypeKind is no…` | not tried (budget) |
| M18 | GeneratedFormatterCollector.cs:65 | a \|\| b → b | `dataTypeSymbol is not INamedTypeSymbol { IsGenericType: false, IsAbstract: false } type \|\| type.TypeKind is no…` | not tried (budget) |
| M22 | GeneratedFormatterCollector.cs:65 | is not X → is X | `type.TypeKind is not (TypeKind.Class or TypeKind.Struct)` | not tried (budget) |
| M13 | GeneratedFormatterCollector.cs:66 | \|\| → && | `dataTypeSymbol is not INamedTypeSymbol { IsGenericType: false, IsAbstract: false } type \|\| type.TypeKind is no…` | not tried (budget) |
| M14 | GeneratedFormatterCollector.cs:66 | a \|\| b → a | `dataTypeSymbol is not INamedTypeSymbol { IsGenericType: false, IsAbstract: false } type \|\| type.TypeKind is no…` | not tried (budget) |
| M15 | GeneratedFormatterCollector.cs:66 | a \|\| b → b | `dataTypeSymbol is not INamedTypeSymbol { IsGenericType: false, IsAbstract: false } type \|\| type.TypeKind is no…` | not tried (budget) |
| M23 | GeneratedFormatterCollector.cs:66 | !x → x | `!compilation.IsSymbolAccessibleWithin(type, formatter)` | not tried (budget) |
| M24 | GeneratedFormatterCollector.cs:68 | statement removed | `reportDiagnostic?.Invoke(Diagnostic.Create( MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupport…` | not tried (budget) |
| M25 | GeneratedFormatterCollector.cs:68 | call passed as argument → default | `Diagnostic.Create( MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported, location, formatterNa…` | not tried (budget) |
| M26 | GeneratedFormatterCollector.cs:72 | call passed as argument → default | `dataTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)` | not tried (budget) |
| M186 | MessagePackGenerator.cs:72 | is X → is not X | `GeneratedFormatterCollector.Collect(s.Left.Right, options, referenceSymbols, reportDiagnostic: null, request.F…` | does not compile; discarded |
| M27 | GeneratedFormatterCollector.cs:76 | == → != | `type.TypeKind == TypeKind.Class` | not tried (budget) |
| M28 | GeneratedFormatterCollector.cs:77 | && → \|\| | `isClass && !type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithi…` | not tried (budget) |
| M29 | GeneratedFormatterCollector.cs:77 | a && b → a | `isClass && !type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithi…` | not tried (budget) |
| M30 | GeneratedFormatterCollector.cs:77 | a && b → b | `isClass && !type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithi…` | not tried (budget) |
| M31 | GeneratedFormatterCollector.cs:77 | !x → x | `!type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithin(c, format…` | not tried (budget) |
| M32 | GeneratedFormatterCollector.cs:77 | && → \|\| | `c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithin(c, formatter)` | not tried (budget) |
| M33 | GeneratedFormatterCollector.cs:77 | a && b → a | `c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithin(c, formatter)` | not tried (budget) |
| M34 | GeneratedFormatterCollector.cs:77 | a && b → b | `c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithin(c, formatter)` | not tried (budget) |
| M35 | GeneratedFormatterCollector.cs:77 | == → != | `c.Parameters.Length == 0` | not tried (budget) |
| M36 | GeneratedFormatterCollector.cs:77 | 0 → 1 | `0` | not tried (budget) |
| M39 | GeneratedFormatterCollector.cs:84 | && → \|\| | `hasCallbackReceiver && type.GetMembers().OfType<IMethodSymbol>().Any(m => m.ExplicitInterfaceImplementations.L…` | not tried (budget) |
| M40 | GeneratedFormatterCollector.cs:84 | a && b → a | `hasCallbackReceiver && type.GetMembers().OfType<IMethodSymbol>().Any(m => m.ExplicitInterfaceImplementations.L…` | not tried (budget) |
| M41 | GeneratedFormatterCollector.cs:84 | a && b → b | `hasCallbackReceiver && type.GetMembers().OfType<IMethodSymbol>().Any(m => m.ExplicitInterfaceImplementations.L…` | not tried (budget) |
| M52 | GeneratedFormatterCollector.cs:89 | is X → is not X | `surrogate is null` | not tried (budget) |
| M56 | GeneratedFormatterCollector.cs:96 | statement removed | `members = CreateSurrogateMembers(type, surrogate, referenceSymbols, reportDiagnostic, location, out isIntKey);` | not tried (budget) |
| M57 | GeneratedFormatterCollector.cs:99 | is X → is not X | `members is null` | not tried (budget) |
| M190 | MessagePackGenerator.cs:128 | return x → return default | `diagnostics.ToImmutableArray()` | not tried (budget) |
| M191 | MessagePackGenerator.cs:131 | statement removed | `context.RegisterSourceOutput(generatedFormatterDiagnostics, static (context, diagnostic) => context.ReportDiag…` | not tried (budget) |
| M69 | GeneratedFormatterCollector.cs:155 | statement removed | `isIntKey = false;` | does not compile; discarded |
| M70 | GeneratedFormatterCollector.cs:155 | false → true | `false` | not tried (budget) |
| M74 | GeneratedFormatterCollector.cs:170 | 1 → 2 | `1` | not tried (budget) |
| M75 | GeneratedFormatterCollector.cs:170 | 0 → 1 | `0` | not tried (budget) |
| M81 | GeneratedFormatterCollector.cs:185 | statement removed | `reportDiagnostic?.Invoke(Diagnostic.Create(MsgPack00xMessagePackAnalyzer.DoNotMixStringAndIntKeys, surrogateMe…` | not tried (budget) |
| M82 | GeneratedFormatterCollector.cs:185 | call passed as argument → default | `Diagnostic.Create(MsgPack00xMessagePackAnalyzer.DoNotMixStringAndIntKeys, surrogateMember.Locations.FirstOrDef…` | not tried (budget) |
| M83 | GeneratedFormatterCollector.cs:189 | == → != | `m.Name == surrogateMember.Name` | not tried (budget) |
| M84 | GeneratedFormatterCollector.cs:190 | \|\| → && | `target is null \|\| !IsPublicReadWrite(target) \|\| !SymbolEqualityComparer.Default.Equals(GetMemberType(target), …` | not tried (budget) |
| M85 | GeneratedFormatterCollector.cs:190 | a \|\| b → a | `target is null \|\| !IsPublicReadWrite(target) \|\| !SymbolEqualityComparer.Default.Equals(GetMemberType(target), …` | not tried (budget) |
| M86 | GeneratedFormatterCollector.cs:190 | a \|\| b → b | `target is null \|\| !IsPublicReadWrite(target) \|\| !SymbolEqualityComparer.Default.Equals(GetMemberType(target), …` | not tried (budget) |
| M87 | GeneratedFormatterCollector.cs:190 | \|\| → && | `target is null \|\| !IsPublicReadWrite(target)` | not tried (budget) |
| M88 | GeneratedFormatterCollector.cs:190 | a \|\| b → a | `target is null \|\| !IsPublicReadWrite(target)` | not tried (budget) |
| M89 | GeneratedFormatterCollector.cs:190 | a \|\| b → b | `target is null \|\| !IsPublicReadWrite(target)` | not tried (budget) |
| M90 | GeneratedFormatterCollector.cs:190 | is X → is not X | `target is null` | not tried (budget) |
| M91 | GeneratedFormatterCollector.cs:190 | !x → x | `!IsPublicReadWrite(target)` | not tried (budget) |
| M92 | GeneratedFormatterCollector.cs:190 | !x → x | `!SymbolEqualityComparer.Default.Equals(GetMemberType(target), GetMemberType(surrogateMember))` | not tried (budget) |
| M93 | GeneratedFormatterCollector.cs:190 | call passed as argument → default | `GetMemberType(target)` | not tried (budget) |
| M94 | GeneratedFormatterCollector.cs:190 | call passed as argument → default | `GetMemberType(surrogateMember)` | not tried (budget) |
| M95 | GeneratedFormatterCollector.cs:192 | statement removed | `reportDiagnostic?.Invoke(Diagnostic.Create( MsgPack00xMessagePackAnalyzer.SurrogateMemberNotFound, surrogateMe…` | not tried (budget) |
| M96 | GeneratedFormatterCollector.cs:192 | call passed as argument → default | `Diagnostic.Create( MsgPack00xMessagePackAnalyzer.SurrogateMemberNotFound, surrogateMember.Locations.FirstOrDef…` | not tried (budget) |
| M97 | GeneratedFormatterCollector.cs:196 | call passed as argument → default | `type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)` | not tried (budget) |
| M98 | GeneratedFormatterCollector.cs:200 | !x → x | `!claimedKeys.Add(key)` | not tried (budget) |
| M101 | GeneratedFormatterCollector.cs:206 | statement removed | `members.Add(key is int intKey ? CreateMember(target, intKey: intKey, stringKey: target.Name) : CreateMember(ta…` | not tried (budget) |
| M102 | GeneratedFormatterCollector.cs:206 | is X → is not X | `key is int intKey` | not tried (budget) |
| M107 | GeneratedFormatterCollector.cs:219 | true → false | `true` | not tried (budget) |
| M115 | GeneratedFormatterCollector.cs:253 | a && b → a | `current is not null && current.SpecialType != SpecialType.System_Object` | not tried (budget) |
| M116 | GeneratedFormatterCollector.cs:253 | a && b → b | `current is not null && current.SpecialType != SpecialType.System_Object` | not tried (budget) |
| M117 | GeneratedFormatterCollector.cs:253 | is not X → is X | `current is not null` | not tried (budget) |
| M118 | GeneratedFormatterCollector.cs:253 | != → == | `current.SpecialType != SpecialType.System_Object` | not tried (budget) |
| M120 | GeneratedFormatterCollector.cs:257 | a \|\| b → a | `member.IsStatic \|\| member.IsImplicitlyDeclared \|\| member is not (IPropertySymbol or IFieldSymbol)` | not tried (budget) |
| M121 | GeneratedFormatterCollector.cs:257 | a \|\| b → b | `member.IsStatic \|\| member.IsImplicitlyDeclared \|\| member is not (IPropertySymbol or IFieldSymbol)` | not tried (budget) |
| M122 | GeneratedFormatterCollector.cs:257 | \|\| → && | `member.IsStatic \|\| member.IsImplicitlyDeclared` | not tried (budget) |
| M123 | GeneratedFormatterCollector.cs:257 | a \|\| b → a | `member.IsStatic \|\| member.IsImplicitlyDeclared` | not tried (budget) |
| M124 | GeneratedFormatterCollector.cs:257 | a \|\| b → b | `member.IsStatic \|\| member.IsImplicitlyDeclared` | not tried (budget) |
| M125 | GeneratedFormatterCollector.cs:257 | is not X → is X | `member is not (IPropertySymbol or IFieldSymbol)` | not tried (budget) |
| M128 | GeneratedFormatterCollector.cs:262 | true → false | `true` | not tried (budget) |
| M132 | GeneratedFormatterCollector.cs:274 | return x → return default | `result` | not tried (budget) |
| M139 | GeneratedFormatterCollector.cs:280 | a && b → b | `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` | not tried (budget) |
| M141 | GeneratedFormatterCollector.cs:280 | is X → is not X | `property.GetMethod is { DeclaredAccessibility: Accessibility.Public }` | not tried (budget) |
| M136 | GeneratedFormatterCollector.cs:281 | a && b → b | `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` | not tried (budget) |
| M142 | GeneratedFormatterCollector.cs:281 | is X → is not X | `property.SetMethod is { DeclaredAccessibility: Accessibility.Public }` | not tried (budget) |

Tests written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) to fill the gaps. None is trusted on the model's word; each was built and run:

| Request | Gap | Target | Tries | Result | Saved as |
|---|---|---|---:|---|---|
| R1 | never executed | `GeneratedFormatterCollector.GetInstanceMembers(INamedTypeSymbol)` line(s) 263–264 | 1 | **accepted**: passes and executes line(s) 263–264 | proposed-tests/R1.cs |
| R2 | unnoticed break: && → \|\| in 'property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…' | `GeneratedFormatterCollector.IsPublicReadWrite(ISymbol)` line(s) 281 | 1 | **accepted**: passes on the code as it is and fails when the line is altered | proposed-tests/R2.cs |
| R3 | never executed | `GeneratedFormatterCollector.GetMemberType(ISymbol)` line(s) 289–290 | 1 | **accepted**: passes and executes line(s) 289; still not executed: 290 | proposed-tests/R3.cs |
| R4 | unnoticed break: a && b → a in 'property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…' | `GeneratedFormatterCollector.IsPublicReadWrite(ISymbol)` line(s) 281 | 1 | **accepted**: passes on the code as it is and fails when the line is altered | proposed-tests/R4.cs |
| R5 | never executed | `GeneratedFormatterCollector.IsPublicReadWrite(ISymbol)` line(s) 282–283 | 2 | **accepted**: passes and executes line(s) 282; still not executed: 283 | proposed-tests/R5.cs |
| R6 | unnoticed break: && → \|\| in 'property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…' | `GeneratedFormatterCollector.IsPublicReadWrite(ISymbol)` line(s) 280 | 1 | **accepted**: passes on the code as it is and fails when the line is altered | proposed-tests/R6.cs |
| R7 | never executed | `GeneratedMessagePackFormatterAttribute.SurrogateType` line(s) 176 | 1 | **accepted**: passes and executes line(s) 176 | proposed-tests/R7.cs |
| R8 | unnoticed break: a && b → a in 'property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…' | `GeneratedFormatterCollector.IsPublicReadWrite(ISymbol)` line(s) 280 | 1 | **accepted**: passes on the code as it is and fails when the line is altered | proposed-tests/R8.cs |
| R9 | never executed | `GeneratedFormatterCollector.CreateMember(ISymbol, int, string)` line(s) 223–224 | 0 | not-attempted | — |
| R10 | unnoticed break: true → false in 'true' | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported` line(s) 342 | 0 | not-attempted | — |
| R11 | never executed | `GeneratedFormatterCollector.CreateContractlessMembers(INamedTypeSymbol)` line(s) 132–138, 140–141 | 0 | not-attempted | — |
| R12 | unnoticed break: call passed as argument → default in 'AnalyzerUtilities.GetHelpLink(GeneratedFormatterDataTypeNotSupportedId)' | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported` line(s) 343 | 0 | not-attempted | — |
| R13 | never executed | `ObjectSerializationInfo.Equals(ObjectSerializationInfo?)` line(s) 168 | 0 | not-attempted | — |
| R14 | unnoticed break: true → false in 'true' | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial` line(s) 324 | 0 | not-attempted | — |
| R15 | never executed | `GeneratedFormatterCollector.CreateSurrogateMembers(INamedTypeSymbol, INamedTypeSymbol, ReferenceSymbols, Action<Diagnostic>?, Location, out bool)` line(s) 165–166, 172–178, 201–203, 211–212 | 0 | not-attempted | — |
| R16 | unnoticed break: call passed as argument → default in 'AnalyzerUtilities.GetHelpLink(GeneratedFormatterMustBePartialId)' | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial` line(s) 325 | 0 | not-attempted | — |
| R17 | never executed | `GeneratedFormatterCollector.Collect(Compilation, AnalyzerOptions, ReferenceSymbols, Action<Diagnostic>?, INamedTypeSymbol, INamedTypeSymbol?, CancellationToken)` line(s) 78–80, 85, 90–93, 104–119, 121–125 | 0 | not-attempted | — |
| R18 | unnoticed break: true → false in 'true' | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustImplementFormatter` line(s) 333 | 0 | not-attempted | — |
| R19 | never executed | `MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)` line(s) 73–75 | 0 | not-attempted | — |
| R20 | unnoticed break: call passed as argument → default in 'AnalyzerUtilities.GetHelpLink(GeneratedFormatterMustImplementFormatterId)' | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustImplementFormatter` line(s) 334 | 0 | not-attempted | — |
| R21 | unnoticed break: true → false in 'true' | `MsgPack00xMessagePackAnalyzer.SurrogateMemberNotFound` line(s) 351 | 0 | not-attempted | — |
| R22 | unnoticed break: call passed as argument → default in 'AnalyzerUtilities.GetHelpLink(SurrogateMemberNotFoundId)' | `MsgPack00xMessagePackAnalyzer.SurrogateMemberNotFound` line(s) 352 | 0 | not-attempted | — |
| R23 | unnoticed break: is X → is not X in 'target is IPropertySymbol { SetMethod.IsInitOnly: true }' | `GeneratedFormatterCollector.CreateMember(ISymbol, int, string)` line(s) 219 | 0 | not-attempted | — |
| R24 | unnoticed break: return x → return default in 'new MemberSerializationInfo( IsProperty: isProperty, IsWritable: true, IsReadable: true, IsInitOnly: isInitOnl…' | `GeneratedFormatterCollector.CreateMember(ISymbol, int, string)` line(s) 227 | 0 | not-attempted | — |
| R25 | unnoticed break: true → false in 'true' | `GeneratedFormatterCollector.CreateMember(ISymbol, int, string)` line(s) 229 | 0 | not-attempted | — |
| R26 | unnoticed break: true → false in 'true' | `GeneratedFormatterCollector.CreateMember(ISymbol, int, string)` line(s) 230 | 0 | not-attempted | — |
| R27 | unnoticed break: call passed as argument → default in 'memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)' | `GeneratedFormatterCollector.CreateMember(ISymbol, int, string)` line(s) 236 | 0 | not-attempted | — |
| R28 | unnoticed break: call passed as argument → default in 'memberType.ToDisplayString(BinaryWriteTypeFormat)' | `GeneratedFormatterCollector.CreateMember(ISymbol, int, string)` line(s) 237 | 0 | not-attempted | — |
| R29 | unnoticed break: && → \|\| in 'i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, openFormatterInterface)' | `GeneratedFormatterCollector.Collect(Compilation, AnalyzerOptions, ReferenceSymbols, Action<Diagnostic>?, INamedTypeSymbol, INamedTypeSymbol?, CancellationToken)` line(s) 55 | 0 | not-attempted | — |

Old code against new code. A probe written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) records what the code does on a set of inputs; it is run twice on the old code, to discard what is unstable, and once on the new:

| Probe | Unit | Inputs compared | Same | Differ | Unstable | Tries | Outcome | Saved as |
|---|---|---:|---:|---:|---:|---:|---|---|
| P1 | `ObjectSerializationInfo.Equals(ObjectSerializationInfo?)` | 11 | 11 | 0 | 0 | 2 | 11 case(s) give the same result on the old and the new code | probes/P1.cs |
| P2 | `MsgPack00xMessagePackAnalyzer.SupportedDiagnostics` | 12 | 6 | 6 | 0 | 1 | 6 of 12 case(s) differ: 6 changed | probes/P2.cs |
| P3 | `FormatterTemplate.TransformText()` | 11 | 11 | 0 | 0 | 3 | 11 case(s) give the same result on the old and the new code | probes/P3.cs |
| P4 | `StringKeyFormatterTemplate.TransformText()` | 10 | 10 | 0 | 0 | 1 | 10 case(s) give the same result on the old and the new code | probes/P4.cs |
| P5 | `GeneratorUtilities.GetAnalyzerOption(IncrementalGeneratorInitializationContext)` | 10 | 10 | 0 | 0 | 1 | 10 case(s) give the same result on the old and the new code | probes/P5.cs |
| P6 | `MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext)` | 12 | 12 | 0 | 0 | 1 | 12 case(s) give the same result on the old and the new code | probes/P6.cs |
| S1 | `the change as a whole, over 2542 inputs built from the existing tests` | 2542 | 2542 | 0 | 0 | 1 | 2542 case(s) give the same result on the old and the new code | probes/S1.cs |

P2: inputs whose result differs, the same alteration grouped together:

- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): Analyzer.MixedIntAndStringKeys
  ```
  - Suppress the following diagnostics to disable this analyzer: MsgPack003, MsgPack004, MsgPack005, MsgPack006, MsgPack007, MsgPack008, MsgPack009, MsgPack010, MsgPack011, MsgPack012, MsgPack013, MsgPa…
  + Suppress the following diagnostics to disable this analyzer: MsgPack003, MsgPack004, MsgPack005, MsgPack006, MsgPack007, MsgPack008, MsgPack009, MsgPack010, MsgPack011, MsgPack012, MsgPack013, MsgPa…
  ```
  Model's reading, not a measurement: intended. Intended by the author as documented, though not explicitly asked for in the requirement; adding MsgPack019-MsgPack022 to SupportedDiagnostics adds them to the analyzer suppression list. It quotes: "- Formatter diagnostics are reported as `MsgPack019` through `MsgPack022`, listed in the [analyzers](doc/analyzers/index.md)."
- **gave a result, now gives another** (**not explained by the change's own text**) in 2 input(s): SupportedDiagnostics.Count; SupportedDiagnostics.EnabledByDefaultCount
  ```
  - 27
  + 31
  ```
  Model's reading, not a measurement: intended. Intended by the author as documented, though not explicitly asked for in the requirement; adding the four diagnostics MsgPack019-MsgPack022 increases SupportedDiagnostics.Count from 27 to 31. It quotes: "- Formatter diagnostics are reported as `MsgPack019` through `MsgPack022`, listed in the [analyzers](doc/analyzers/index.md)."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): SupportedDiagnostics.Descriptors
  ```
  -  MsgPack018|Usage|Error|True|Unique names required in force map mode|https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/master/doc/analyzers/MsgPack018.md]
  +  MsgPack018|Usage|Error|True|Unique names required in force map mode|https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/master/doc/analyzers/MsgPack018.md
  +  MsgPack019|Usage|Error|True|Generated formatter must be partial|https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/master/doc/analyzers/MsgPack019.md
  +  MsgPack020|Usage|Error|True|Generated formatter must implement IMessagePackFormatter<T>|https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/master/doc/analyzers/MsgPack020.md
  +  MsgPack021|Usage|Error|True|Generated formatter data type not supported|https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/master/doc/analyzers/MsgPack021.md
  +  MsgPack022|Usage|Error|True|Surrogate member does not match the formatted type|https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/master/doc/analyzers/MsgPack022.md]
  ```
  Model's reading, not a measurement: intended. Intended by the author as documented, though not explicitly asked for in the requirement; SupportedDiagnostics.Descriptors now includes the four new MsgPack019-MsgPack022 descriptors. It quotes: "- Formatter diagnostics are reported as `MsgPack019` through `MsgPack022`, listed in the [analyzers](doc/analyzers/index.md)."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): SupportedDiagnostics.Ids
  ```
  -  MsgPack018]
  +  MsgPack018
  +  MsgPack019
  +  MsgPack020
  +  MsgPack021
  +  MsgPack022]
  ```
  Model's reading, not a measurement: intended. Intended by the author as documented, though not explicitly asked for in the requirement; SupportedDiagnostics.Ids now includes MsgPack019 through MsgPack022. It quotes: "- Formatter diagnostics are reported as `MsgPack019` through `MsgPack022`, listed in the [analyzers](doc/analyzers/index.md)."
- **gave a result, now gives another** (**not explained by the change's own text**) in 1 input(s): SupportedDiagnostics.SeverityCounts
  ```
  - [Error:22
  + [Error:26
  ```
  Model's reading, not a measurement: intended. Intended by the author as documented, though not explicitly asked for in the requirement; adding the four Error-severity diagnostics MsgPack019-MsgPack022 increases the Error count from 22 to 26. It quotes: "- Formatter diagnostics are reported as `MsgPack019` through `MsgPack022`, listed in the [analyzers](doc/analyzers/index.md)."

Sweep over inputs nobody chose (S1 above). AYA found 151 input(s) in the tests of `tests/MessagePack.SourceGenerator.Tests/MessagePack.SourceGenerator.Tests.csproj` as they were before the change, took 151 as they are and altered 2391 mechanically. No model chose or wrote any input; a model wrote only the harness that feeds them to the code and records what comes out. Run on the new code, the inputs executed 58 of the change's 276 executable line(s). The inputs are in `sweep-inputs.tsv`.

Seen by the sweep: on 40 input(s) the code under test crashes on the old and on the new code alike (for example i032+a3 GenerationTests.cs:785 + an attribute argument made to repeat its sibling's).

Inputs that came with the change. 9 of them (the sources its own tests hold, as they are and altered) were run on the new code alone, since the old code has nothing to say about them. They are held to what must be true of any input: the code under test does not crash, and what it produces compiles unless it reported an error itself. 0 break one of the two. The inputs are in `sweep-new-inputs.tsv`.

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

A model's reading of what was meant (gemini-4-hydrogen-medium (Gemini, through the Antigravity command line)). This is an opinion and is marked as one wherever it appears. It was given the text of the requirement, supplied by the owner, the commit message(s) of the change, 123 line(s) of documentation the change added, 57 comment line(s) the change added to the code, and asked about the 5 group(s) of differences that the change's own text does not explain: 5 it reads as what the change set out to do, 0 as not meant, 0 as artefacts of the build or the run, 0 as unclear. A reading of "not meant" moves an item up inside its tier; no reading moves anything down.

The requirement, read against the tests. The reading is a model's and is an opinion. Two things in it were checked here: the words in the first column are in the requirement, and a test named in the third is one that fails on the old code.

| The requirement asks | The model's reading | Test that shows it | Why |
|---|---|---|---|
| I need to have a source-generated `IMessagePackFormatter` for type `ProjectA.MyType` generated **inside Project B** instead of Project A, because the latter has no (and cannot have) reference to MessagePack nuget package. | not-demonstrated | — | The change adds GeneratedFormatterCollector to generate formatters for external types, but all tests in GeneratedFormatterForExternalTypesTests could not be compiled against the old code and the diagnostic tests that fail on the old code only use same-assembly types with hand-written formatter members. |
| Add a `[GeneratedMessagePackFormatter]` attribute that will work for partial classes implementing `IMessagePackFormatter<T>`. | not-demonstrated | — | The change adds GeneratedMessagePackFormatterAttribute and collector support, but the tests that fail on the old code only verify error diagnostics rather than successful generation for a valid partial formatter class. |
| The class does not even need to declare a body (no `{` and `}`), because the actual implementation is source-generated by `MessagePack.SourceGenerator.MessagePackGenerator` as usual. | not-demonstrated | — | None of the listed tests declare a bodyless partial class without braces, and the generation tests could not be compiled against the old code. |
| Allow the generator and analyzers to work with user-defined versions of the MessagePack attributes. | not-addressed | — | The change uses contractless mode or a SurrogateType in the consuming assembly instead of recognizing user-defined copies of MessagePack attributes. |
| Source-generate the attributes themselves using [`IncrementalGeneratorInitializationContext.RegisterPostInitializationOutput`](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md#outputting-values) instead of them existing in the `MessagePack.Annotations` assembly. | not-addressed | — | The new GeneratedMessagePackFormatterAttribute is added to MessagePack.Annotations rather than being emitted via RegisterPostInitializationOutput. |

The change undone one part at a time. With the old lines of a part back in the new code, the tests were run again:

| Part | Lines | Outcome |
|---|---|---|
| V1 | `src/MessagePack.Annotations/Attributes.cs` 142–177 | **no test needs it**: every test passes with the old lines back |
| V2 | `src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs` 318–353 | does not compile without the rest of the change |
| V3 | `src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs` 1–300 | does not compile without the rest of the change |
| V4 | `src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterRequest.cs` 1–13 | does not compile without the rest of the change |
| V5 | `src/MessagePack.SourceGenerator/CodeAnalysis/ObjectSerializationInfo.cs` 13–18 | does not compile without the rest of the change |
| V6 | `src/MessagePack.SourceGenerator/GeneratorUtilities.cs` 35–36 | **no test needs it**: every test passes with the old lines back |
| V7 | `src/MessagePack.SourceGenerator/MessagePackGenerator.cs` 29–35 | does not compile without the rest of the change |
| V8 | `src/MessagePack.SourceGenerator/Transforms/FormatterTemplate.cs` 33–35 | **no test needs it**: every test passes with the old lines back |

Tests written to break the change, by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line). What their writer says about them is not used: each file is built and run here, and a test that fails on the new code is run on the old code, which decides whether the change caused it:

| Attack | Unit | Tests | Outcome | Saved as |
|---|---|---:|---|---|
| A1 | `MsgPack00xMessagePackAnalyzer.SupportedDiagnostics` | 4 | fails-on-both: 2 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 2 other(s) pass | attacks/A1.cs |
| A2 | `ObjectSerializationInfo.Equals(ObjectSerializationInfo?)` | 4 | resisted: 4 test(s) written to break it pass on the new code | attacks/A2.cs |
| A3 | `FormatterTemplate.TransformText()` | 5 | fails-on-both: 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass | attacks/A3.cs |
| A4 | `GeneratorUtilities.GetAnalyzerOption(IncrementalGeneratorInitializationContext)` | 5 | fails-on-both: 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass | attacks/A4.cs |
| A5 | `ObjectSerializationInfo.IsUserDeclaredFormatter` | 6 | fails-on-both: 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 1 other(s) pass | attacks/A5.cs |
| A6 | `GeneratedFormatterCollector.BinaryWriteTypeFormat` | 5 | fails-on-both: 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass | attacks/A6.cs |
| A7 | `StringKeyFormatterTemplate.TransformText()` | 5 | fails-on-both: 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass | attacks/A7.cs |
| A8 | `GeneratedFormatterCollector.CreateContractlessMembers(INamedTypeSymbol)` | 6 | possible-defect: 5 test(s) fail on the new code and the old code cannot judge them (they need code the change added): AyaA8Attack.Contractless_RequiredProperty_CompilesCleanly, AyaA8Attack.Contractless_IndexerIsIgnored_CompilesCleanly, AyaA8Attack.Contractless_StructWithMixedMembers_CompilesCleanly, AyaA8Attack.Contractless_InitOnlyProperty_CompilesCleanly, AyaA8Attack.Contractless_OverriddenAndShadowedBaseMembers_CompilesCleanly | attacks/A8.cs |
| A9 | `GeneratedFormatterRequest` | 5 | fails-on-both: 5 test(s) fail on the new code and on the old code alike, so the change did not cause it or the tests are wrong; 0 other(s) pass | attacks/A9.cs |
| A10 | `GeneratedMessagePackFormatterAttribute.SurrogateType` | 5 | resisted: 5 test(s) written to break it pass on the new code | attacks/A10.cs |
| A11 | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported` | 5 | resisted: 5 test(s) written to break it pass on the new code | attacks/A11.cs |
| A12 | `MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial` | 5 | possible-defect: 1 test(s) fail on the new code and the old code cannot judge them (they need code the change added): AyaA12Attack.GeneratedFormatterMustBePartial_IsInSupportedDiagnosticsWithoutDuplicateIds | attacks/A12.cs |
| K1 | `GeneratedFormatterRequest` | 4 | possible-defect: 1 test(s) that fail on the new code pass on the old only because they use GeneratedMessagePackFormatter, which the change introduced: the old code has nothing there to be tested, so its passing says nothing. No test of this file is one the old code can judge. | attacks/K1.cs |

A1: `AyaA1Attack.SupportedDiagnostics_AllDescriptorsHaveUniqueIds` fails on the new code; on the old code: Failed.
```
Assert.Equal() Failure: Values differ
Expected: 31
Actual:   20
```

A1: `AyaA1Attack.MixedIntAndStringKeys_StillReportsMsgPack005` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Mismatch between number of diagnostics returned, expected "1" actual "3"

Diagnostics:
// error AD0001: Analyzer 'MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer' threw an exception of type 'System.InvalidOperationException' with message 'Nullable object must have a value.'.
Exception occurred with following context:
Compilation: TestProject
ISymbol: MyObject (NamedType)

System.InvalidOperationException: Nullable object must have a …
```

A3: `AyaA3Attack.UserDeclaredPartialStructFormatterCompiles` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Mismatch between number of diagnostics returned, expected "0" actual "3"

Diagnostics:
// /0/Test0.cs(15,2): error CS0592: Attribute 'GeneratedMessagePackFormatter' is not valid on this declaration type. It is only valid on 'class' declarations.
DiagnosticResult.CompilerError("CS0592").WithSpan(15, 2, 15, 31).WithArguments("GeneratedMessagePackFormatter", "class"),
// /0/Test0.cs(16,37): error CS0535: 'MyFormatter' does not implement interface member 'IMes…
```

A3: `AyaA3Attack.StandardMessagePackObjectFormatterCompiles` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MessagePack.GeneratedMessagePackResolver.TestNamespace.MyTypeFormatter.g.cs

```

A3: `AyaA3Attack.UserDeclaredPartialRecordFormatterCompiles` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Mismatch between number of diagnostics returned, expected "0" actual "12"

Diagnostics:
// /0/Test0.cs(15,1): error CS7014: Attributes are not valid in this context.
DiagnosticResult.CompilerError("CS7014").WithSpan(15, 1, 15, 69),
// /0/Test0.cs(15,1): error CS8370: Feature 'top-level statements' is not available in C# 7.3. Please use language version 9.0 or greater.
DiagnosticResult.CompilerError("CS8370").WithSpan(15, 1, 16, 35).WithArguments("top-leve…
```

A3: `AyaA3Attack.UserDeclaredSealedPartialClassFormatterCompiles` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs

```

A3: `AyaA3Attack.GenericMessagePackObjectWithConstraintsCompiles` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MessagePack.GeneratedMessagePackResolver.TestNamespace.MyGenericTypeFormatter`1.g.cs

```

A4: `AyaA4Attack.GeneratedFormatterSplitAcrossPartialDeclarations` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs

```

A4: `AyaA4Attack.GeneratedFormatterWithKnownFormatterAssemblyAttribute` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs

```

A4: `AyaA4Attack.GeneratedFormatterWithSurrogateUsedByMessagePackObject` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MessagePack.Gener…
```

A4: `AyaA4Attack.GeneratedFormatterUsedByMessagePackObject` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MessagePack.Gener…
```

A4: `AyaA4Attack.GeneratedFormatterAndHandWrittenFormatterInSameCompilation` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MessagePack.GeneratedMessagePackResolver.HolderFormatter.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.Mess…
```

A5: `AyaA5Attack.GenericPartialFormatterWithoutAttributeIsNotGeneratedInto` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs

```

A5: `AyaA5Attack.GenericPartialMessagePackObjectGeneratesFormatterNormally` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MessagePack.GeneratedMessagePackResolver.TestNamespace.GenericContainerFormatter`1.g.cs

```

A5: `AyaA5Attack.NestedPartialTypeAndPartialFormatterCoexistWithoutDiagnostics` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs

```

A5: `AyaA5Attack.PartialCustomFormatterUsedViaMessagePackFormatterAttributeIsNotGeneratedInto` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs

```

A5: `AyaA5Attack.PartialFormatterWithoutAttributeIsNotGeneratedInto` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs

```

A6: `AyaA6Attack.CustomTypeNamedStringInNamespace_DoesNotCollideWithSystemString` fails on the new code; on the old code: Failed.
```
Assert.Empty() Failure: Collection was not empty
Collection: [MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs(48,14): error CS0433: The type 'AutomataKeyGen' exists in both 'MessagePack.SourceGenerator, Version=3.0.288.2106, Culture=neutral, PublicKeyToken=b4a0369545f0a1be' and 'MessagePack, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b4a0369545f0a1be']
```

A6: `AyaA6Attack.CustomStructNamedInt32Nullable_DoesNotCollideWithSystemNullableInt32` fails on the new code; on the old code: Failed.
```
Assert.Empty() Failure: Collection was not empty
Collection: [MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs(48,14): error CS0433: The type 'AutomataKeyGen' exists in both 'MessagePack.SourceGenerator, Version=3.0.288.2106, Culture=neutral, PublicKeyToken=b4a0369545f0a1be' and 'MessagePack, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b4a0369545f0a1be']
```

A6: `AyaA6Attack.CustomGenericTypeNamedNullableInNamespace_DoesNotCollideWithSystemNullable` fails on the new code; on the old code: Failed.
```
Assert.Empty() Failure: Collection was not empty
Collection: [MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs(48,14): error CS0433: The type 'AutomataKeyGen' exists in both 'MessagePack.SourceGenerator, Version=3.0.288.2106, Culture=neutral, PublicKeyToken=b4a0369545f0a1be' and 'MessagePack, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b4a0369545f0a1be']
```

A6: `AyaA6Attack.CustomTypeNamedInt32InNamespace_DoesNotCollideWithSystemInt32` fails on the new code; on the old code: Failed.
```
Assert.Empty() Failure: Collection was not empty
Collection: [MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs(48,14): error CS0433: The type 'AutomataKeyGen' exists in both 'MessagePack.SourceGenerator, Version=3.0.288.2106, Culture=neutral, PublicKeyToken=b4a0369545f0a1be' and 'MessagePack, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b4a0369545f0a1be']
```

A6: `AyaA6Attack.CustomTypeNamedByteArrayInNamespace_DoesNotCollideWithSystemByteArray` fails on the new code; on the old code: Failed.
```
Assert.Empty() Failure: Collection was not empty
Collection: [MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs(48,14): error CS0433: The type 'AutomataKeyGen' exists in both 'MessagePack.SourceGenerator, Version=3.0.288.2106, Culture=neutral, PublicKeyToken=b4a0369545f0a1be' and 'MessagePack, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b4a0369545f0a1be']
```

A7: `AyaA7Attack.GeneratedFormatter_WithStringKeySurrogate_GeneratesPartialClass` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs

```

A7: `AyaA7Attack.GeneratedFormatter_PartialStructWithStringKeySurrogate_GeneratesValidFormatter` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Mismatch between number of diagnostics returned, expected "0" actual "3"

Diagnostics:
// /0/Test0.cs(15,2): error CS0592: Attribute 'GeneratedMessagePackFormatter' is not valid on this declaration type. It is only valid on 'class' declarations.
DiagnosticResult.CompilerError("CS0592").WithSpan(15, 2, 15, 31).WithArguments("GeneratedMessagePackFormatter", "class"),
// /0/Test0.cs(16,37): error CS0535: 'MyFormatter' does not implement interface member 'IMes…
```

A7: `AyaA7Attack.ContractlessNestedGenericMessagePackObject_GeneratesFormatter` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MessagePack.GeneratedMessagePackResolver.Outer.InnerFormatter`1.g.cs

```

A7: `AyaA7Attack.StringKeyMessagePackObject_GeneratesFormatter` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MessagePack.GeneratedMessagePackResolver.MyTypeFormatter.g.cs

```

A7: `AyaA7Attack.GeneratedFormatter_PartialRecordWithStringKeySurrogate_GeneratesValidFormatter` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Mismatch between number of diagnostics returned, expected "0" actual "12"

Diagnostics:
// /0/Test0.cs(15,1): error CS7014: Attributes are not valid in this context.
DiagnosticResult.CompilerError("CS7014").WithSpan(15, 1, 15, 69),
// /0/Test0.cs(15,1): error CS8370: Feature 'top-level statements' is not available in C# 7.3. Please use language version 9.0 or greater.
DiagnosticResult.CompilerError("CS8370").WithSpan(15, 1, 16, 35).WithArguments("top-leve…
```

A8: `AyaA8Attack.Contractless_RequiredProperty_CompilesCleanly` fails on the new code; on the old code: cannot-compile.
```
Assert.Empty() Failure: Collection was not empty
Collection: [MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs(46,14): error CS0433: The type 'AutomataKeyGen' exists in both 'MessagePack.SourceGenerator, Version=3.0.288.2106, Culture=neutral, PublicKeyToken=b4a0369545f0a1be' and 'MessagePack, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b4a0369545f0a1be']
```

A8: `AyaA8Attack.Contractless_IndexerIsIgnored_CompilesCleanly` fails on the new code; on the old code: cannot-compile.
```
Assert.Empty() Failure: Collection was not empty
Collection: [MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs(46,14): error CS0433: The type 'AutomataKeyGen' exists in both 'MessagePack.SourceGenerator, Version=3.0.288.2106, Culture=neutral, PublicKeyToken=b4a0369545f0a1be' and 'MessagePack, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b4a0369545f0a1be']
```

A8: `AyaA8Attack.Contractless_StructWithMixedMembers_CompilesCleanly` fails on the new code; on the old code: cannot-compile.
```
Assert.Empty() Failure: Collection was not empty
Collection: [MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs(46,14): error CS0433: The type 'AutomataKeyGen' exists in both 'MessagePack.SourceGenerator, Version=3.0.288.2106, Culture=neutral, PublicKeyToken=b4a0369545f0a1be' and 'MessagePack, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b4a0369545f0a1be', MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Form…
```

A8: `AyaA8Attack.Contractless_InitOnlyProperty_CompilesCleanly` fails on the new code; on the old code: cannot-compile.
```
Assert.Empty() Failure: Collection was not empty
Collection: [MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs(54,14): error CS0433: The type 'AutomataKeyGen' exists in both 'MessagePack.SourceGenerator, Version=3.0.288.2106, Culture=neutral, PublicKeyToken=b4a0369545f0a1be' and 'MessagePack, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b4a0369545f0a1be', MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Form…
```

A8: `AyaA8Attack.Contractless_OverriddenAndShadowedBaseMembers_CompilesCleanly` fails on the new code; on the old code: cannot-compile.
```
Assert.Empty() Failure: Collection was not empty
Collection: [MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs(56,14): error CS0433: The type 'AutomataKeyGen' exists in both 'MessagePack.SourceGenerator, Version=3.0.288.2106, Culture=neutral, PublicKeyToken=b4a0369545f0a1be' and 'MessagePack, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b4a0369545f0a1be', MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Form…
```

A9: `AyaA9Attack.GeneratedFormatterWithInterfaceOnSecondPartialDeclaration` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs

```

A9: `AyaA9Attack.GeneratedFormatterAndMessagePackObjectInSameCompilation` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MessagePack.GeneratedMessagePackResolver.AnnotatedTypeFormatter.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerat…
```

A9: `AyaA9Attack.GeneratedFormatterWithExplicitNullSurrogateUsesContractlessMode` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MyFormatter.g.cs

```

A9: `AyaA9Attack.HandWrittenPartialFormatterWithoutAttributeDoesNotTriggerGenerator` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.MessagePack.GeneratedMessagePackResolver.MyTypeFormatter.g.cs

```

A9: `AyaA9Attack.GeneratedFormatterNestedInPartialClass` fails on the new code; on the old code: Failed.
```
System.InvalidOperationException : Context: Source generator application
Context: Verifying source generated files
Expected source file list to match
-
-
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\MessagePack.GeneratedMessagePackResolver.g.cs
+MessagePack.SourceGenerator\MessagePack.SourceGenerator.MessagePackGenerator\Formatters.Outer.MyFormatter.g.cs

```

A12: `AyaA12Attack.GeneratedFormatterMustBePartial_IsInSupportedDiagnosticsWithoutDuplicateIds` fails on the new code; on the old code: cannot-compile.
```
Assert.Equal() Failure: Values differ
Expected: 31
Actual:   20
```

K1: `AyaK1Attack.SurrogateTypeArrayDoesNotCrashOrFallbackToContractless` fails on the new code; on the old code: not-arbitrated.
```
Assert.DoesNotContain() Failure: Sub-string found
                                       ↓ (pos 2159)
String: ···"::MyType>\r\n\t{\r\n\t\t// SecretId\r\n\t\tprivate s"···
Found:  "SecretId"
```

Exhaustive mode. 1 member(s) depend on the change and are run by no original test. 0 were probed with 0 of 12 session(s); 0 could be compared on old and new code (rows X… above). The other 1 remain unmeasured.

## 5. What was not checked

- **Unexecuted code.** GeneratedFormatterCollector.GetInstanceMembers(INamedTypeSymbol) — line(s) 263–264 of src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs were never executed by any test; 4 members depend on this symbol and inherit the doubt
- **Unexecuted code.** GeneratedFormatterCollector.GetMemberType(ISymbol) — line(s) 289–290 of src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs were never executed by any test; 5 members depend on this symbol and inherit the doubt
- **Unexecuted code.** GeneratedFormatterCollector.IsPublicReadWrite(ISymbol) — line(s) 282–283 of src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs were never executed by any test; 4 members depend on this symbol and inherit the doubt
- **Unexecuted code.** GeneratedMessagePackFormatterAttribute.SurrogateType — line(s) 176 of src/MessagePack.Annotations/Attributes.cs were never executed by any test
- **Unexecuted code.** GeneratedFormatterCollector.CreateMember(ISymbol, int, string) — line(s) 223–224 of src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs were never executed by any test; 4 members depend on this symbol and inherit the doubt
- **Unexecuted code.** GeneratedFormatterCollector.CreateContractlessMembers(INamedTypeSymbol) — line(s) 132–138, 140–141 of src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs were never executed by any test; 2 members depend on this symbol and inherit the doubt
- **Unexecuted code.** ObjectSerializationInfo.Equals(ObjectSerializationInfo?) — line(s) 168 of src/MessagePack.SourceGenerator/CodeAnalysis/ObjectSerializationInfo.cs were never executed by any test; 57 members depend on this symbol and inherit the doubt
- **Unexecuted code.** GeneratedFormatterCollector.CreateSurrogateMembers(INamedTypeSymbol, INamedTypeSymbol, ReferenceSymbols, Action<Diagnostic>?, Location, out bool) — line(s) 165–166, 172–178, 201–203, 211–212 of src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs were never executed by any test; 2 members depend on this symbol and inherit the doubt
- **Unexecuted code.** GeneratedFormatterCollector.Collect(Compilation, AnalyzerOptions, ReferenceSymbols, Action<Diagnostic>?, INamedTypeSymbol, INamedTypeSymbol?, CancellationToken) — line(s) 78–80, 85, 90–93, 104–119, 121–125 of src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs were never executed by any test; 1 members depend on this symbol and inherit the doubt
- **Unexecuted code.** MessagePackGenerator.Initialize(IncrementalGeneratorInitializationContext) — line(s) 73–75 of src/MessagePack.SourceGenerator/MessagePackGenerator.cs were never executed by any test
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:55 — altering `i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, openFormatterInterface)` (&& → ||) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:219 — altering `target is IPropertySymbol { SetMethod.IsInitOnly: true }` (is X → is not X) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:227 — altering `new MemberSerializationInfo( IsProperty: isProperty, IsWritable: true, IsReadable: true, IsInitOnly: isInitOnl…` (return x → return default) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:229 — altering `true` (true → false) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:230 — altering `true` (true → false) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:236 — altering `memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)` (call passed as argument → default) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:237 — altering `memberType.ToDisplayString(BinaryWriteTypeFormat)` (call passed as argument → default) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:281 — altering `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` (&& → ||) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:281 — altering `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` (a && b → a) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:280 — altering `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` (&& → ||) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/CodeAnalysis/GeneratedFormatterCollector.cs:280 — altering `property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is { DeclaredAccessibility: Acces…` (a && b → a) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs:324 — altering `true` (true → false) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs:325 — altering `AnalyzerUtilities.GetHelpLink(GeneratedFormatterMustBePartialId)` (call passed as argument → default) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs:333 — altering `true` (true → false) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs:334 — altering `AnalyzerUtilities.GetHelpLink(GeneratedFormatterMustImplementFormatterId)` (call passed as argument → default) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs:342 — altering `true` (true → false) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs:343 — altering `AnalyzerUtilities.GetHelpLink(GeneratedFormatterDataTypeNotSupportedId)` (call passed as argument → default) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs:351 — altering `true` (true → false) makes no test fail. The line runs, but nothing checks what it does.
- **Unnoticed break.** src/MessagePack.SourceGenerator/Analyzers/MsgPack00xMessagePackAnalyzer.cs:352 — altering `AnalyzerUtilities.GetHelpLink(SurrogateMemberNotFoundId)` (call passed as argument → default) makes no test fail. The line runs, but nothing checks what it does.
- **Documentation.** 7 file(s), 163 lines: no tool checked that this text is accurate (README.md, doc/analyzers/MsgPack019.md, doc/analyzers/MsgPack020.md, doc/analyzers/MsgPack021.md, …)
- **Build configuration.** 3 file(s), 25 lines: no tool checked that this text is accurate (tests/MessagePack.SourceGenerator.ExecutionTests/MessagePack.SourceGenerator.ExecutionTests.csproj, tests/MessagePack.SourceGenerator.ExternalTypes/MessagePack.SourceGenerator.ExternalTypes.csproj, tests/MessagePack.SourceGenerator.MapModeExecutionTests/MessagePack.SourceGenerator.MapModeExecutionTests.csproj)
- **Templates vs generated code.** Both T4 templates (.tt) and their generated .cs files changed. The templates were not re-run to confirm the committed .cs files are what they produce.

## 6. What AYA refuses to decide

- **Requirement.** Whether this change does what requirement MessagePack-CSharp#2129 asks. AYA measured what the code does under test; it has not interpreted the requirement.
- **Side effects.** Whether behaviour changes in already-existing members are intended. They are reachable from code the requirement does not mention.
- **Proposed tests.** Whether the 8 accepted test(s) written by gemini-4-hydrogen-medium (Gemini, through the Antigravity command line) assert the right behaviour. AYA checked that they compile, pass and reach their target; a model chose what to assert. They are proposals in the proposed-tests folder, not part of the change.

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
| Commit | `083afd994c1c9a994cb2cd5aa0b6ad805a621457` |
| Evidence | sha256 `68825c75b46edfb34186c1c8ea84fcc7b15ed768bd10472558aedd6cb4a2ada8` |
| Looked at by the owner | section 1, down to item no. ___ |
| Residual risk accepted | the items in sections 5 and 6 |
| Date | |
