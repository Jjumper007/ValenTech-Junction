using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using MessagePack;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaA1Attack
{
    [Fact]
    public void InSource_MissingKeyAttribute_ReportsDiagnostic()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject]
public partial class MissingKeyObject
{
    public int Value { get; set; }
}
""";

        (ImmutableArray<Diagnostic> diagnostics, _) = RunGenerator(source);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void InSource_InaccessibleMessagePackObject_ReportsDiagnostic()
    {
        string source = """
using MessagePack;

namespace App;

public partial class Outer
{
    [MessagePackObject]
    private partial class PrivateInner
    {
        [Key(0)]
        public int Value { get; set; }
    }
}
""";

        (ImmutableArray<Diagnostic> diagnostics, _) = RunGenerator(source);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void MetadataReference_UnserializableMemberType_ReportsDiagnostic()
    {
        string libSource = """
namespace Lib;

public class PlainUnserializable
{
    public int Value { get; set; }
}
""";

        string appSource = """
using MessagePack;
using Lib;

namespace App;

[MessagePackObject]
public partial class AppObject
{
    [Key(0)]
    public PlainUnserializable Member { get; set; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        MetadataReference libRef = CompileToMetadataReference(libSource, "Lib", LanguageVersion.Latest);
        (ImmutableArray<Diagnostic> diagnostics, _) = RunGenerator(appSource, LanguageVersion.Latest, libRef);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void MetadataReference_UnserializableArrayMember_ReportsDiagnostic()
    {
        string libSource = """
namespace Lib;

public class PlainUnserializable
{
    public int Value { get; set; }
}
""";

        string appSource = """
using MessagePack;
using Lib;

namespace App;

[MessagePackObject]
public partial class AppObject
{
    [Key(0)]
    public PlainUnserializable[] Members { get; set; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        MetadataReference libRef = CompileToMetadataReference(libSource, "Lib", LanguageVersion.Latest);
        (ImmutableArray<Diagnostic> diagnostics, _) = RunGenerator(appSource, LanguageVersion.Latest, libRef);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void InSource_ClosedGenericRecordStruct_GeneratesWithoutDiagnostics()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject]
public partial record struct Item<T>([property: Key(0)] string Key);

[MessagePackObject]
public sealed partial record Cmd([property: Key(0)] string Scope)
{
    [Key(1)]
    public Item<int> Member { get; init; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        (ImmutableArray<Diagnostic> diagnostics, GeneratorDriverRunResult runResult) = RunGenerator(source);
        Assert.Empty(diagnostics);
        Assert.NotEmpty(runResult.GeneratedTrees);
    }

    private static MetadataReference CompileToMetadataReference(string source, string assemblyName, LanguageVersion languageVersion)
    {
        var parseOptions = new CSharpParseOptions(languageVersion);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            GetStandardReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);
        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics));
        ms.Position = 0;
        return MetadataReference.CreateFromStream(ms);
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, GeneratorDriverRunResult RunResult) RunGenerator(
        string source,
        LanguageVersion languageVersion = LanguageVersion.Latest,
        params MetadataReference[] extraReferences)
    {
        var parseOptions = new CSharpParseOptions(languageVersion);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        IEnumerable<MetadataReference> references = GetStandardReferences().Concat(extraReferences);
        var compilation = CSharpCompilation.Create(
            "App",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new IIncrementalGenerator[] { new MessagePackGenerator() });
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> diagnostics);
        return (diagnostics, driver.GetRunResult());
    }

    private static IReadOnlyList<MetadataReference> GetStandardReferences()
    {
        _ = typeof(MessagePackObjectAttribute).Assembly;
        _ = typeof(MessagePackSerializer).Assembly;

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                paths.Add(assembly.Location);
            }
        }

        return paths.Select(p => MetadataReference.CreateFromFile(p)).ToArray();
    }
}
