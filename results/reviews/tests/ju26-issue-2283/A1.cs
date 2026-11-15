// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using MessagePack;
using MessagePack.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

/// <summary>
/// Review tests for the change that implements issue 2283 (closed generic value types from a referenced assembly).
/// Each test compiles a library to a metadata image in memory, references it from an application compilation,
/// and runs the MessagePack source generator and the MsgPack00x analyzer on the application.
/// </summary>
public class AyaA1Attack
{
    private const string Resolver = "\n[MessagePack.GeneratedMessagePackResolver] partial class AppResolver { }\n";

    private const string AppUsesItemOfInt = """
        using MessagePack;
        using Lib;
        namespace App;
        [MessagePackObject]
        public class Cmd
        {
            [Key(0)]
            public Item<int> Member { get; set; }
        }
        """ + Resolver;

    /// <summary>
    /// Finding 1: a referenced generic type with duplicate int keys used to produce a build-breaking MsgPack005 error
    /// in the consuming project. Without it, the build only shows warnings while no resolver is generated.
    /// </summary>
    [Fact]
    public async Task F1a_DuplicateKeysInReferencedGenericType_StillReportsMsgPack005()
    {
        Outcome outcome = await RunAsync(
            """
            using MessagePack;
            namespace Lib;
            [MessagePackObject]
            public class Item<T>
            {
                [Key(0)] public T Value { get; set; }
                [Key(0)] public int Other { get; set; }
            }
            """,
            AppUsesItemOfInt);

        AssertAnalyzerError(outcome, "MsgPack005", "must be unique");
    }

    /// <summary>
    /// Finding 1: a referenced generic type that mixes int and string keys used to produce a build-breaking MsgPack005 error
    /// in the consuming project.
    /// </summary>
    [Fact]
    public async Task F1b_MixedKeysInReferencedGenericType_StillReportsMsgPack005()
    {
        Outcome outcome = await RunAsync(
            """
            using MessagePack;
            namespace Lib;
            [MessagePackObject]
            public class Item<T>
            {
                [Key(0)] public T Value { get; set; }
                [Key("b")] public int Other { get; set; }
            }
            """,
            AppUsesItemOfInt);

        AssertAnalyzerError(outcome, "MsgPack005", "must be of the same type");
    }

    /// <summary>
    /// Finding 2: an unattributed public member of a referenced generic class is silently left out of the formatter
    /// that the consuming project generates for that class. The consuming project used to get a MsgPack004 error.
    /// </summary>
    [Fact]
    public async Task F2_UnkeyedPublicMemberOfReferencedGenericClass_StillReportsMsgPack004()
    {
        Outcome outcome = await RunAsync(
            """
            using MessagePack;
            namespace Lib;
            [MessagePackObject]
            public class Item<T>
            {
                [Key(0)] public T Value { get; set; }
                public int Unkeyed { get; set; }
            }
            """,
            AppUsesItemOfInt);

        AssertAnalyzerError(outcome, "MsgPack004", "Unkeyed");
    }

    /// <summary>
    /// Finding 3: a referenced generic class without a usable constructor gets no formatter, so the generated resolver
    /// does not compile. The consuming project used to get the MsgPack007 error that explains why.
    /// </summary>
    [Fact]
    public async Task F3a_ReferencedGenericClassWithoutPublicConstructor_StillReportsMsgPack007()
    {
        Outcome outcome = await RunAsync(
            """
            using MessagePack;
            namespace Lib;
            [MessagePackObject]
            public class Item<T>
            {
                private Item() { }
                public static Item<T> Create() => new Item<T>();
                [Key(0)] public T Value { get; set; }
            }
            """,
            AppUsesItemOfInt);

        AssertAnalyzerError(outcome, "MsgPack007", "Cannot find a public constructor");
    }

    /// <summary>
    /// Finding 3: a referenced generic class whose serialization constructor does not match its members gets no formatter.
    /// The consuming project used to get the MsgPack007 error that explains why.
    /// </summary>
    [Fact]
    public async Task F3b_ReferencedGenericClassWithMismatchedConstructor_StillReportsMsgPack007()
    {
        Outcome outcome = await RunAsync(
            """
            using MessagePack;
            namespace Lib;
            [MessagePackObject]
            public class Item<T>
            {
                [SerializationConstructor]
                public Item(string notAMember) { }
                [Key(0)] public T Value { get; set; }
            }
            """,
            AppUsesItemOfInt);

        AssertAnalyzerError(outcome, "MsgPack007", "Deserializing constructor parameter type mismatch");
    }

    /// <summary>
    /// Finding 3: a type declared in the consuming project inherits a keyed member, from a referenced base class,
    /// whose type is not a MessagePack object. The consuming project used to get MsgPack003 for its own type.
    /// </summary>
    [Fact]
    public async Task F3c_InSourceTypeInheritingUnformattableMemberFromReferencedBase_StillReportsMsgPack003()
    {
        Outcome outcome = await RunAsync(
            """
            using MessagePack;
            namespace Lib;
            public class Unattributed<T> { public T V { get; set; } }
            [MessagePackObject]
            public class Base
            {
                [Key(0)] public Unattributed<int> U { get; set; }
            }
            """,
            """
            using MessagePack;
            using Lib;
            namespace App;
            [MessagePackObject]
            public class Derived : Base
            {
                [Key(1)]
                public int X { get; set; }
            }
            """ + Resolver);

        AssertAnalyzerError(outcome, "MsgPack003", "Base.U");
    }

    /// <summary>
    /// Finding 3: a referenced generic type has a keyed member whose type is not a MessagePack object.
    /// The consuming project generates the formatter for it and used to get MsgPack003.
    /// </summary>
    [Fact]
    public async Task F3d_ReferencedGenericTypeWithUnformattableMember_StillReportsMsgPack003()
    {
        Outcome outcome = await RunAsync(
            """
            using MessagePack;
            namespace Lib;
            public class Unattributed<T> { public T V { get; set; } }
            [MessagePackObject]
            public class Wrapper<T>
            {
                [Key(0)] public Unattributed<T> U { get; set; }
            }
            """,
            """
            using MessagePack;
            using Lib;
            namespace App;
            [MessagePackObject]
            public class Cmd
            {
                [Key(0)]
                public Wrapper<int> W { get; set; }
            }
            """ + Resolver);

        AssertAnalyzerError(outcome, "MsgPack003", "Wrapper.U");
    }

    /// <summary>
    /// Finding 4: the new policy drops every diagnostic without a location in the consuming project, not only the ones
    /// about members of closed generic value types. A referenced type whose MessagePackFormatterAttribute names a type
    /// that is not a formatter used to produce MsgPack006 in the consuming project.
    /// </summary>
    [Fact]
    public async Task F4_ReferencedTypeWithInvalidFormatterAttribute_StillReportsMsgPack006()
    {
        Outcome outcome = await RunAsync(
            """
            using MessagePack;
            namespace Lib;
            [MessagePackFormatter(typeof(string))]
            public class Foo { }
            """,
            """
            using MessagePack;
            using Lib;
            namespace App;
            [MessagePackObject]
            public class Cmd
            {
                [Key(0)]
                public Foo Member { get; set; }
            }
            """ + Resolver);

        AssertAnalyzerError(outcome, "MsgPack006", "IMessagePackFormatter");
    }

    /// <summary>
    /// Finding 5: a positional record property with a null key, declared in the project being compiled,
    /// must be reported as MsgPack005 just like an ordinary property is.
    /// </summary>
    [Fact]
    public async Task F5_NullKeyOnPositionalRecordProperty_ReportsMsgPack005()
    {
        Outcome outcome = await RunAsync(
            null,
            """
            using MessagePack;
            namespace App;
            [MessagePackObject]
            public record Cmd([property: Key(null)] string Scope);
            """ + Resolver);

        AssertAnalyzerError(outcome, "MsgPack005", "Both int and string keys are null");
    }

    /// <summary>
    /// Finding 6: the README says generic value types from a referenced assembly can be used as members.
    /// A referenced generic struct in map mode with AllowPrivate must produce generated code that compiles.
    /// </summary>
    [Fact]
    public async Task F6a_ReferencedGenericStruct_MapModeAllowPrivate_GeneratedCodeCompiles()
    {
        Outcome outcome = await RunAsync(
            """
            using MessagePack;
            namespace Lib;
            [MessagePackObject(true, AllowPrivate = true)]
            public struct Item<T>
            {
                public T Value { get; set; }
            }
            """,
            AppUsesItemOfInt);

        AssertResolverGeneratedAndCompiles(outcome);
    }

    /// <summary>
    /// Finding 6: a referenced generic struct with a private keyed field and AllowPrivate must produce generated code that compiles.
    /// </summary>
    [Fact]
    public async Task F6b_ReferencedGenericStruct_PrivateKeyedField_GeneratedCodeCompiles()
    {
        Outcome outcome = await RunAsync(
            """
            using MessagePack;
            namespace Lib;
            [MessagePackObject(AllowPrivate = true)]
            public partial struct Item<T>
            {
                [Key(0)] private T value;
                public Item(T v) { this.value = v; }
                public T Get() => this.value;
            }
            """,
            AppUsesItemOfInt);

        AssertResolverGeneratedAndCompiles(outcome);
    }

    /// <summary>
    /// Finding 6: a referenced struct nested in a generic class, used closed, must produce generated code that compiles.
    /// </summary>
    [Fact]
    public async Task F6c_ReferencedStructNestedInGenericClass_GeneratedCodeCompiles()
    {
        Outcome outcome = await RunAsync(
            """
            using MessagePack;
            namespace Lib;
            public static class Outer<TOuter>
            {
                [MessagePackObject]
                public struct Inner { [Key(0)] public TOuter V { get; set; } }
            }
            """,
            """
            using MessagePack;
            using Lib;
            namespace App;
            [MessagePackObject]
            public class Cmd
            {
                [Key(0)] public Outer<int>.Inner D { get; set; }
            }
            """ + Resolver);

        AssertResolverGeneratedAndCompiles(outcome);
    }

    private static void AssertAnalyzerError(Outcome outcome, string id, string messageFragment)
    {
        bool found = outcome.AnalyzerDiagnostics.Any(d =>
            d.Id == id &&
            d.Severity == DiagnosticSeverity.Error &&
            d.GetMessage().Contains(messageFragment, StringComparison.Ordinal));

        Assert.True(found, $"Expected the analyzer to report error {id} containing \"{messageFragment}\" in the consuming project.{Environment.NewLine}{outcome.Describe()}");
    }

    private static void AssertResolverGeneratedAndCompiles(Outcome outcome)
    {
        Assert.True(outcome.GeneratedHintNames.Contains("App.AppResolver.g.cs"), $"Expected the resolver to be generated.{Environment.NewLine}{outcome.Describe()}");
        Assert.True(outcome.CompileErrors.Length == 0, $"Expected the consuming project to compile with the generated code.{Environment.NewLine}{outcome.Describe()}");
    }

    private static async Task<Outcome> RunAsync(string? libSource, string appSource)
    {
        CSharpParseOptions parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        CSharpCompilationOptions compilationOptions = new(OutputKind.DynamicallyLinkedLibrary);

        ImmutableArray<MetadataReference> frameworkReferences = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        List<MetadataReference> references = frameworkReferences.ToList();
        foreach (string location in new[] { typeof(MessagePackObjectAttribute).Assembly.Location, typeof(MessagePackSerializer).Assembly.Location }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            references.Add(MetadataReference.CreateFromFile(location));
        }

        List<MetadataReference> appReferences = new(references);
        if (libSource is not null)
        {
            // The library is referenced as metadata only, as it is in a command-line build.
            CSharpCompilation lib = CSharpCompilation.Create("Lib", new[] { CSharpSyntaxTree.ParseText(libSource, parseOptions, path: "Lib.cs") }, references, compilationOptions);
            using MemoryStream image = new();
            Microsoft.CodeAnalysis.Emit.EmitResult emitResult = lib.Emit(image);
            Assert.True(emitResult.Success, "The library must compile: " + string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.Id)));
            appReferences.Add(MetadataReference.CreateFromImage(image.ToArray()));
        }

        Compilation app = CSharpCompilation.Create("App", new[] { CSharpSyntaxTree.ParseText(appSource, parseOptions, path: "App.cs") }, appReferences, compilationOptions);

        // Run the source generator.
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new MessagePack.SourceGenerator.MessagePackGenerator().AsSourceGenerator() }, parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(app, out Compilation appWithGeneratedCode, out _);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Run the analyzer on the compilation that includes the generated code, as a real build does.
        List<string> analyzerExceptions = new();
        CompilationWithAnalyzersOptions analyzerOptions = new(
            new Microsoft.CodeAnalysis.Diagnostics.AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            onAnalyzerException: (exception, analyzer, diagnostic) => analyzerExceptions.Add(exception.GetType().Name),
            concurrentAnalysis: false,
            logAnalyzerExecutionTime: false);
        ImmutableArray<Diagnostic> analyzerDiagnostics = await appWithGeneratedCode
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MsgPack00xMessagePackAnalyzer()), analyzerOptions)
            .GetAnalyzerDiagnosticsAsync();

        return new Outcome(
            runResult.Diagnostics,
            runResult.Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName).ToImmutableArray(),
            appWithGeneratedCode.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray(),
            analyzerDiagnostics,
            analyzerExceptions.ToImmutableArray());
    }

    private sealed class Outcome
    {
        internal Outcome(ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableArray<string> generatedHintNames, ImmutableArray<Diagnostic> compileErrors, ImmutableArray<Diagnostic> analyzerDiagnostics, ImmutableArray<string> analyzerExceptions)
        {
            this.GeneratorDiagnostics = generatorDiagnostics;
            this.GeneratedHintNames = generatedHintNames;
            this.CompileErrors = compileErrors;
            this.AnalyzerDiagnostics = analyzerDiagnostics;
            this.AnalyzerExceptions = analyzerExceptions;
        }

        internal ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }

        internal ImmutableArray<string> GeneratedHintNames { get; }

        internal ImmutableArray<Diagnostic> CompileErrors { get; }

        internal ImmutableArray<Diagnostic> AnalyzerDiagnostics { get; }

        internal ImmutableArray<string> AnalyzerExceptions { get; }

        internal string Describe()
        {
            StringBuilder builder = new();
            builder.AppendLine("Generator diagnostics: " + Join(this.GeneratorDiagnostics.Select(d => d.Id)));
            builder.AppendLine("Generated files: " + Join(this.GeneratedHintNames));
            builder.AppendLine("Compile errors after generation: " + Join(this.CompileErrors.Select(d => d.Id).Distinct()));
            builder.AppendLine("Analyzer diagnostics: " + Join(this.AnalyzerDiagnostics.Select(d => $"{d.Severity} {d.Id} ({d.Location.Kind})")));
            builder.AppendLine("Analyzer exceptions: " + Join(this.AnalyzerExceptions));
            return builder.ToString();
        }

        private static string Join(IEnumerable<string> values)
        {
            string joined = string.Join(", ", values);
            return joined.Length == 0 ? "(none)" : joined;
        }
    }
}
