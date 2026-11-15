// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Text;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Defects of the <c>[GeneratedMessagePackFormatter]</c> feature (issue 2129).
/// Every test states what a user should get and fails while the defect is present.
/// </summary>
public class AyaA2Attack
{
    private const string Preamble = "using System; using System.Collections.Generic; using MessagePack; using MessagePack.Formatters; using MessagePack.Resolvers;\n";

    /// <summary>
    /// Options that put the generated resolver first, as the README of the feature and its own tests do.
    /// </summary>
    private const string Composite = "MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(MessagePack.GeneratedMessagePackResolver.Instance, StandardResolver.Instance))";

    private const string ExtPerson = """
        namespace Ext
        {
            public class Person { public int Id { get; set; } public string Name { get; set; } }
        }
        """;

    private const string ExtPersonWithBadSurrogate = """
        using MessagePack;
        namespace Ext
        {
            public class Person { public int Id { get; set; } public string Name { get; set; } }
            public class PersonSurrogate
            {
                [Key(0)] public int Id { get; set; }
                [Key(1)] public string Missing { get; set; }
            }
        }
        """;

    private const string HandWrittenPersonMembers = """
            public void Serialize(ref MessagePackWriter writer, Ext.Person value, MessagePackSerializerOptions options) => throw new System.NotImplementedException();
            public Ext.Person Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new System.NotImplementedException();
        """;

    /// <summary>
    /// The attribute only exists once the feature is present, so this file does not compile without it (on purpose).
    /// </summary>
    private static readonly Type NewFeature = typeof(MessagePack.GeneratedMessagePackFormatterAttribute);

    [Fact]
    public async Task F1a_DefaultOptionsFindGeneratedFormatterForExternalType()
    {
        RunResult r = await RunAsync(
            """
            [GeneratedMessagePackFormatter]
            public partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
            {
            }

            public static class Entry
            {
                public static string Run()
                {
                    // No options: this is MessagePackSerializer's default (StandardResolver).
                    byte[] bytes = MessagePackSerializer.Serialize(new Ext.Person { Id = 7, Name = "seven" });
                    Ext.Person result = MessagePackSerializer.Deserialize<Ext.Person>(bytes);
                    return result.Id + ":" + result.Name;
                }
            }
            """,
            ExtPerson);

        AssertNoCompilerErrors(r);
        Assert.Equal("7:seven", Invoke(r));
    }

    [Fact]
    public async Task F1b_DefaultOptionsSerializeLocalObjectThatContainsExternalType()
    {
        RunResult r = await RunAsync(
            """
            [GeneratedMessagePackFormatter]
            public partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
            {
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)] public Ext.Person Person { get; set; }
            }

            public static class Entry
            {
                public static string Run()
                {
                    byte[] bytes = MessagePackSerializer.Serialize(new Holder { Person = new Ext.Person { Id = 7, Name = "seven" } });
                    Holder result = MessagePackSerializer.Deserialize<Holder>(bytes);
                    return result.Person.Id + ":" + result.Person.Name;
                }
            }
            """,
            ExtPerson);

        AssertNoCompilerErrors(r);
        Assert.Equal("7:seven", Invoke(r));
    }

    [Fact]
    public async Task F2a_IgnoreDataMemberOnExternalTypeIsHonoured()
    {
        RunResult r = await RunAsync(
            $$"""
            [GeneratedMessagePackFormatter]
            public partial class DcFormatter : IMessagePackFormatter<Ext.Dc>
            {
            }

            public static class Entry
            {
                public static string Run() => MessagePackSerializer.ConvertToJson(
                    MessagePackSerializer.Serialize(new Ext.Dc { Id = 1, Name = "n", Secret = "s3cr3t-value" }, {{Composite}}));
            }
            """,
            """
            using System.Runtime.Serialization;
            namespace Ext
            {
                [DataContract]
                public class Dc
                {
                    [DataMember] public int Id { get; set; }
                    [DataMember] public string Name { get; set; }
                    [IgnoreDataMember] public string Secret { get; set; }
                }
            }
            """);

        AssertNoCompilerErrors(r);
        Assert.DoesNotContain("s3cr3t-value", (string)Invoke(r)!);
    }

    [Fact]
    public async Task F2b_IgnoreMemberOnExternalMessagePackObjectIsHonoured()
    {
        RunResult r = await RunAsync(
            $$"""
            [GeneratedMessagePackFormatter]
            public partial class MpFormatter : IMessagePackFormatter<Ext.Mp>
            {
            }

            public static class Entry
            {
                public static string Run() => MessagePackSerializer.ConvertToJson(
                    MessagePackSerializer.Serialize(new Ext.Mp { Id = 1, Name = "n", Secret = "s3cr3t-value" }, {{Composite}}));
            }
            """,
            """
            using MessagePack;
            namespace Ext
            {
                [MessagePackObject]
                public class Mp
                {
                    [Key(0)] public int Id { get; set; }
                    [Key(1)] public string Name { get; set; }
                    [IgnoreMember] public string Secret { get; set; }
                }
            }
            """,
            externalReferencesMessagePack: true);

        AssertNoCompilerErrors(r);
        Assert.DoesNotContain("s3cr3t-value", (string)Invoke(r)!);
    }

    [Fact]
    public async Task F3a_TypeWithOnlyGetOnlyMembersIsSerializedOrDiagnosed()
    {
        RunResult r = await RunAsync(
            $$"""
            [GeneratedMessagePackFormatter]
            public partial class ImmutableFormatter : IMessagePackFormatter<Ext.Immutable>
            {
            }

            public static class Entry
            {
                public static string Run() => MessagePackSerializer.ConvertToJson(
                    MessagePackSerializer.Serialize(new Ext.Immutable(4242, "seven"), {{Composite}}));
            }
            """,
            """
            namespace Ext
            {
                public class Immutable
                {
                    public Immutable() { }
                    public Immutable(int id, string name) { Id = id; Name = name; }
                    public int Id { get; }
                    public string Name { get; }
                }
            }
            """);

        if (HasMsgPackDiagnostic(r, DiagnosticSeverity.Warning))
        {
            return; // Telling the user that nothing can be serialized is an acceptable outcome.
        }

        AssertNoCompilerErrors(r);
        Assert.Contains("4242", (string)Invoke(r)!);
    }

    [Fact]
    public async Task F3b_GetOnlyCollectionMemberIsSerializedOrDiagnosed()
    {
        RunResult r = await RunAsync(
            $$"""
            [GeneratedMessagePackFormatter]
            public partial class MixedFormatter : IMessagePackFormatter<Ext.Mixed>
            {
            }

            public static class Entry
            {
                public static string Run()
                {
                    var value = new Ext.Mixed { Id = 1 };
                    value.Items.Add(4242);
                    return MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(value, {{Composite}}));
                }
            }
            """,
            """
            using System.Collections.Generic;
            namespace Ext
            {
                public class Mixed
                {
                    public int Id { get; set; }
                    public List<int> Items { get; } = new List<int>();
                }
            }
            """);

        if (HasMsgPackDiagnostic(r, DiagnosticSeverity.Warning))
        {
            return;
        }

        AssertNoCompilerErrors(r);
        Assert.Contains("4242", (string)Invoke(r)!);
    }

    [Fact]
    public async Task F3c_PropertyWhoseGetterIsOverriddenIsStillSerialized()
    {
        RunResult r = await RunAsync(
            $$"""
            [GeneratedMessagePackFormatter]
            public partial class DerivedFormatter : IMessagePackFormatter<Ext.Derived>
            {
            }

            public static class Entry
            {
                public static string Run() => MessagePackSerializer.ConvertToJson(
                    MessagePackSerializer.Serialize(new Ext.Derived { Id = 4242, Own = 1 }, {{Composite}}));
            }
            """,
            """
            namespace Ext
            {
                public class Base { public virtual int Id { get; set; } }
                public class Derived : Base
                {
                    public override int Id { get => base.Id; }
                    public int Own { get; set; }
                }
            }
            """);

        if (HasMsgPackDiagnostic(r, DiagnosticSeverity.Warning))
        {
            return;
        }

        AssertNoCompilerErrors(r);
        Assert.Contains("4242", (string)Invoke(r)!);
    }

    [Fact]
    public async Task F4a_SurrogateErrorInReferencedProjectDoesNotCrashTheGenerator()
    {
        // The surrogate lives in another project that is referenced as a compilation (what an IDE does for project references).
        RunResult r = await RunAsync(
            $$"""
            [MessagePackObject]
            public class Local { [Key(0)] public int A { get; set; } }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(Ext.PersonSurrogate))]
            public partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
            {
            {{HandWrittenPersonMembers}}
            }
            """,
            ExtPersonWithBadSurrogate,
            externalAsCompilationReference: true,
            externalReferencesMessagePack: true);

        Assert.Null(r.GeneratorException);
        Assert.DoesNotContain(r.GeneratorDiagnostics, d => d.Id == "CS8785");
        Assert.Contains(r.GeneratorDiagnostics, d => d.Id == "MsgPack022");

        // The unrelated [MessagePackObject] type of this project must keep its generated formatter.
        Assert.Contains(r.GeneratedTrees, t => t.FilePath.Contains("LocalFormatter"));
    }

    [Fact]
    public async Task F4b_SurrogateErrorInReferencedAssemblyIsReportedAtASourceLocation()
    {
        RunResult r = await RunAsync(
            $$"""
            [GeneratedMessagePackFormatter(SurrogateType = typeof(Ext.PersonSurrogate))]
            public partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
            {
            {{HandWrittenPersonMembers}}
            }
            """,
            ExtPersonWithBadSurrogate,
            externalReferencesMessagePack: true);

        Diagnostic diagnostic = Assert.Single(r.GeneratorDiagnostics, d => d.Id == "MsgPack022");
        Assert.True(diagnostic.Location.IsInSource, "MsgPack022 has no source location: " + diagnostic);
    }

    [Fact]
    public async Task F5_ExplicitCallbackReceiverImplementationCompiles()
    {
        RunResult r = await RunAsync(
            """
            public class Cb : IMessagePackSerializationCallbackReceiver
            {
                public int Id { get; set; }
                void IMessagePackSerializationCallbackReceiver.OnBeforeSerialize() { }
                void IMessagePackSerializationCallbackReceiver.OnAfterDeserialize() { }
            }

            [GeneratedMessagePackFormatter]
            public partial class CbFormatter : IMessagePackFormatter<Cb>
            {
            }
            """);

        Assert.Empty(r.GeneratorDiagnostics);
        AssertNoCompilerErrors(r);
    }

    [Fact]
    public async Task F6a_PrivateFieldOfSurrogateDoesNotNeedAKey()
    {
        RunResult r = await RunAsync(
            """
            public class PersonSurrogate
            {
                private int cache;
                [Key(0)] public int Id { get => this.cache; set => this.cache = value; }
                [Key(1)] public string Name { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(PersonSurrogate))]
            public partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
            {
            }
            """,
            ExtPerson);

        Assert.DoesNotContain(r.GeneratorDiagnostics, d => d.Id == "MsgPack004");
        AssertNoCompilerErrors(r);
    }

    [Fact]
    public async Task F6b_SurrogateWithKeyAsPropertyNameIsAccepted()
    {
        RunResult r = await RunAsync(
            """
            [MessagePackObject(keyAsPropertyName: true)]
            public class PersonSurrogate
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(PersonSurrogate))]
            public partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
            {
            }
            """,
            ExtPerson);

        Assert.DoesNotContain(r.GeneratorDiagnostics, d => d.Id == "MsgPack004");
        AssertNoCompilerErrors(r);
    }

    [Fact]
    public async Task F6c_MessagePackFormatterAttributeOnSurrogateMemberIsUsedOrDiagnosed()
    {
        RunResult r = await RunAsync(
            $$"""
            public class RedactingFormatter : IMessagePackFormatter<string>
            {
                public void Serialize(ref MessagePackWriter writer, string value, MessagePackSerializerOptions options) => writer.Write("REDACTED");
                public string Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => reader.ReadString();
            }

            public class PersonSurrogate
            {
                [Key(0)] public int Id { get; set; }
                [Key(1), MessagePackFormatter(typeof(RedactingFormatter))] public string Name { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(PersonSurrogate))]
            public partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
            {
            }

            public static class Entry
            {
                public static string Run() => MessagePackSerializer.ConvertToJson(
                    MessagePackSerializer.Serialize(new Ext.Person { Id = 1, Name = "plain-text-name" }, {{Composite}}));
            }
            """,
            ExtPerson);

        if (HasMsgPackDiagnostic(r, DiagnosticSeverity.Warning))
        {
            return; // Saying that the attribute is not supported on a surrogate is an acceptable outcome.
        }

        AssertNoCompilerErrors(r);
        string json = (string)Invoke(r)!;
        Assert.Contains("REDACTED", json);
        Assert.DoesNotContain("plain-text-name", json);
    }

    [Fact]
    public async Task F6d_SurrogateTypeThatCannotBeUsedIsDiagnosed()
    {
        // typeof(Sur[]) is not a type whose members can describe a contract. It must not silently mean "no surrogate".
        RunResult r = await RunAsync(
            """
            public class Sur { [Key(0)] public int Id { get; set; } }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(Sur[]))]
            public partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
            {
            }
            """,
            ExtPerson);

        Assert.True(HasMsgPackDiagnostic(r, DiagnosticSeverity.Warning), "The unusable SurrogateType was ignored without a diagnostic." + Describe(r));
    }

    [Fact]
    public async Task F7_UseMapModeAppliesToSurrogateWithIntKeys()
    {
        RunResult r = await RunAsync(
            $$"""
            namespace MessagePack
            {
                [GeneratedMessagePackResolver(UseMapMode = true)]
                partial class GeneratedMessagePackResolver { }
            }

            [MessagePackObject]
            public class Local { [Key(0)] public int A { get; set; } }

            public class PersonSurrogate
            {
                [Key(0)] public int Id { get; set; }
                [Key(1)] public string Name { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(PersonSurrogate))]
            public partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
            {
            }

            public static class Entry
            {
                public static string Run()
                {
                    var options = {{Composite}};
                    byte local = MessagePackSerializer.Serialize(new Local { A = 1 }, options)[0];
                    byte external = MessagePackSerializer.Serialize(new Ext.Person { Id = 1, Name = "n" }, options)[0];
                    return (local & 0xF0).ToString("X2") + ":" + (external & 0xF0).ToString("X2");
                }
            }
            """,
            ExtPerson);

        AssertNoCompilerErrors(r);

        // 0x80 is a fixmap header, 0x90 a fixarray header. Map mode asks for maps everywhere.
        Assert.Equal("80:80", Invoke(r));
    }

    [Fact]
    public async Task F8a_FormatterNestedInNonPartialTypeIsDiagnosed()
    {
        RunResult r = await RunAsync(
            """
            public class Outer
            {
                [GeneratedMessagePackFormatter]
                public partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
                {
                }
            }
            """,
            ExtPerson);

        AssertCompilesOrIsDiagnosed(r);
    }

    [Fact]
    public async Task F8b_FormatterWithPrivateConstructorAndInstanceFieldCompiles()
    {
        // The singleton shape that the analyzer accepts for hand-written formatters.
        RunResult r = await RunAsync(
            """
            [GeneratedMessagePackFormatter]
            public sealed partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
            {
                public static readonly PersonFormatter Instance = new PersonFormatter();

                private PersonFormatter() { }
            }
            """,
            ExtPerson);

        AssertCompilesOrIsDiagnosed(r);
    }

    [Fact]
    public async Task F8c_GenericFormatterClassCompilesOrIsDiagnosed()
    {
        RunResult r = await RunAsync(
            """
            [GeneratedMessagePackFormatter]
            public partial class PersonFormatter<T> : IMessagePackFormatter<Ext.Person>
            {
            }
            """,
            ExtPerson);

        AssertCompilesOrIsDiagnosed(r);
    }

    [Fact]
    public async Task F8d_FormatterThatInheritsTheInterfaceCompilesOrIsDiagnosed()
    {
        RunResult r = await RunAsync(
            """
            public abstract class BaseFormatter<T> : IMessagePackFormatter<T>
            {
                public abstract void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options);
                public abstract T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options);
            }

            [GeneratedMessagePackFormatter]
            public partial class PersonFormatter : BaseFormatter<Ext.Person>
            {
            }
            """,
            ExtPerson);

        AssertCompilesOrIsDiagnosed(r);
    }

    [Fact]
    public async Task F8e_PartialRecordFormatterCompilesOrIsDiagnosed()
    {
        RunResult r = await RunAsync(
            """
            [GeneratedMessagePackFormatter]
            public partial record PersonFormatter : IMessagePackFormatter<Ext.Person>
            {
            }
            """,
            ExtPerson);

        AssertCompilesOrIsDiagnosed(r);
    }

    [Fact]
    public async Task F8f_DataTypeThatIsAlsoALocalMessagePackObjectCompilesOrIsDiagnosed()
    {
        RunResult r = await RunAsync(
            """
            [MessagePackObject]
            public class Local { [Key(0)] public int A { get; set; } }

            [GeneratedMessagePackFormatter]
            public partial class LocalFormatter : IMessagePackFormatter<Local>
            {
            }
            """);

        AssertCompilesOrIsDiagnosed(r);
    }

    [Fact]
    public async Task F9_FormattersForMemberTypesAreRegisteredInGeneratedResolver()
    {
        // For a [MessagePackObject] type the generated resolver also serves the enum, array and list types of its members,
        // which is what makes it usable without the dynamic (reflection/IL emitting) resolvers.
        RunResult r = await RunAsync(
            """
            [GeneratedMessagePackFormatter]
            public partial class OrderFormatter : IMessagePackFormatter<Ext.Order>
            {
            }

            public static class Entry
            {
                public static string Run()
                {
                    IFormatterResolver resolver = MessagePack.GeneratedMessagePackResolver.Instance;
                    var missing = new List<string>();
                    if (resolver.GetFormatter<Ext.Order>() == null) { missing.Add("Order"); }
                    if (resolver.GetFormatter<Ext.Shade>() == null) { missing.Add("Shade"); }
                    if (resolver.GetFormatter<Ext.Shade[]>() == null) { missing.Add("Shade[]"); }
                    if (resolver.GetFormatter<List<Ext.Shade>>() == null) { missing.Add("List<Shade>"); }
                    return string.Join(",", missing);
                }
            }
            """,
            """
            using System.Collections.Generic;
            namespace Ext
            {
                public enum Shade { Light, Dark }
                public class Order
                {
                    public Shade Shade { get; set; }
                    public List<Shade> Shades { get; set; }
                    public Shade[] Arr { get; set; }
                }
            }
            """);

        AssertNoCompilerErrors(r);
        Assert.Equal(string.Empty, Invoke(r));
    }

    [Fact]
    public async Task F10_DocumentedUsageHasNoAnalyzerWarningWhenNullableIsEnabled()
    {
        // Exactly the shape shown in the README and in the attribute's documentation.
        RunResult r = await RunAsync(
            """
            namespace My.App
            {
                [GeneratedMessagePackFormatter]
                public partial class PersonFormatter : IMessagePackFormatter<Ext.Person>
                {
                }
            }
            """,
            ExtPerson,
            nullableEnabled: true);

        AssertNoCompilerErrors(r);
        Assert.DoesNotContain(r.AnalyzerDiagnostics, d => d.Id == "MsgPack014");
    }

    private static void AssertNoCompilerErrors(RunResult r)
    {
        Assert.True(r.GeneratorException is null, "The generator threw: " + r.GeneratorException);
        Assert.True(r.CompilerErrors.IsEmpty, "The compilation has errors after source generation." + Describe(r));
    }

    /// <summary>
    /// A formatter the user declared must either get a compilable implementation, or an error from MessagePack that explains why not.
    /// </summary>
    private static void AssertCompilesOrIsDiagnosed(RunResult r)
    {
        Assert.True(r.GeneratorException is null, "The generator threw: " + r.GeneratorException);
        if (HasMsgPackDiagnostic(r, DiagnosticSeverity.Error))
        {
            return;
        }

        Assert.True(r.CompilerErrors.IsEmpty, "Compiler errors and no MessagePack error diagnostic that explains them." + Describe(r));
    }

    private static bool HasMsgPackDiagnostic(RunResult r, DiagnosticSeverity minimum)
        => r.GeneratorDiagnostics.Concat(r.AnalyzerDiagnostics).Any(d => d.Id.StartsWith("MsgPack", StringComparison.Ordinal) && d.Severity >= minimum);

    private static string Describe(RunResult r)
    {
        StringBuilder sb = new();
        sb.AppendLine();
        foreach (Diagnostic d in r.GeneratorDiagnostics)
        {
            sb.AppendLine("generator: " + d.Id + " " + d.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        }

        foreach (Diagnostic d in r.AnalyzerDiagnostics)
        {
            sb.AppendLine("analyzer: " + d.Id + " " + d.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        }

        foreach (Diagnostic d in r.CompilerErrors)
        {
            sb.AppendLine("compiler: " + d.Id + " " + d.GetMessage(System.Globalization.CultureInfo.InvariantCulture) + " @ " + Path.GetFileName(d.Location.SourceTree?.FilePath) + d.Location.GetLineSpan().StartLinePosition);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Emits the compilation, loads it and calls the public static <c>Entry.Run()</c> that the test source declares.
    /// </summary>
    private static object? Invoke(RunResult r)
    {
        using MemoryStream stream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult emit = r.Output.Emit(stream);
        Assert.True(emit.Success, "Emit failed." + Describe(r));

        AssemblyLoadContext context = new("AyaA2Attack");
        Assembly? external = r.ExternalImage is null ? null : context.LoadFromStream(new MemoryStream(r.ExternalImage));
        context.Resolving += (_, name) => external is not null && name.Name == external.GetName().Name ? external : null;
        Assembly assembly = context.LoadFromStream(new MemoryStream(stream.ToArray()));

        MethodInfo run = assembly.GetType("Entry", throwOnError: true)!.GetMethod("Run")!;
        try
        {
            return run.Invoke(null, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static ImmutableArray<MetadataReference> GetReferences(bool messagePack)
    {
        // The framework the tests run on, so that what is compiled here can also be loaded and run here.
        string frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        ImmutableArray<MetadataReference>.Builder references = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (string path in trusted.Split(Path.PathSeparator))
        {
            if (string.Equals(Path.GetDirectoryName(path), frameworkDirectory, StringComparison.OrdinalIgnoreCase))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        if (messagePack)
        {
            references.Add(MetadataReference.CreateFromFile(NewFeature.Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(MessagePack.MessagePackSerializer).Assembly.Location));
        }

        return references.ToImmutable();
    }

    private static async Task<RunResult> RunAsync(
        string source,
        string? externalSource = null,
        bool externalAsCompilationReference = false,
        bool externalReferencesMessagePack = false,
        bool nullableEnabled = false)
    {
        CSharpParseOptions parseOptions = new(LanguageVersion.CSharp12);
        ImmutableArray<MetadataReference> references = GetReferences(messagePack: true);
        byte[]? externalImage = null;

        if (externalSource is not null)
        {
            CSharpCompilation external = CSharpCompilation.Create(
                "ExternalLib",
                new[] { CSharpSyntaxTree.ParseText(externalSource, parseOptions, path: "Ext.cs", encoding: Encoding.UTF8) },
                GetReferences(messagePack: externalReferencesMessagePack),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            Assert.Empty(external.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

            if (externalAsCompilationReference)
            {
                references = references.Add(external.ToMetadataReference());
            }
            else
            {
                using MemoryStream stream = new();
                Assert.True(external.Emit(stream).Success);
                externalImage = stream.ToArray();
                references = references.Add(MetadataReference.CreateFromImage(externalImage));
            }
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            "Consumer",
            new[] { CSharpSyntaxTree.ParseText(Preamble + source, parseOptions, path: "Test.cs", encoding: Encoding.UTF8) },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: nullableEnabled ? NullableContextOptions.Enable : NullableContextOptions.Disable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new MessagePackGenerator().AsSourceGenerator() }, parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> generatorDiagnostics);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        ImmutableArray<Diagnostic> analyzerDiagnostics = await output
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MsgPack00xMessagePackAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        return new RunResult(
            output,
            generatorDiagnostics,
            runResult.Results.Select(result => result.Exception).FirstOrDefault(exception => exception is not null),
            output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray(),
            analyzerDiagnostics,
            runResult.GeneratedTrees,
            externalImage);
    }

    private sealed record RunResult(
        Compilation Output,
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        Exception? GeneratorException,
        ImmutableArray<Diagnostic> CompilerErrors,
        ImmutableArray<Diagnostic> AnalyzerDiagnostics,
        ImmutableArray<SyntaxTree> GeneratedTrees,
        byte[]? ExternalImage);
}
