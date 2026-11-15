// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using MessagePack;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaA3Attack
{
    [Fact]
    public void PositionalRecord_MissingKeyAttribute_ReportsDiagnostic()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject]
public partial record MyRecord(int Value);

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        RunGenerator(source, out _, out ImmutableArray<Diagnostic> diagnostics);

        Assert.Contains(diagnostics, d => d.Id.StartsWith("MsgPack", StringComparison.Ordinal));
    }

    [Fact]
    public void PositionalRecordStruct_MissingKeyAttribute_ReportsDiagnostic()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject]
public partial record struct MyRecordStruct(int Value);

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        RunGenerator(source, out _, out ImmutableArray<Diagnostic> diagnostics);

        Assert.Contains(diagnostics, d => d.Id.StartsWith("MsgPack", StringComparison.Ordinal));
    }

    [Fact]
    public void UnannotatedMetadataType_AsMember_ReportsDiagnostic()
    {
        string libSource = """
namespace Lib;

public class UnannotatedType
{
    public int X { get; set; }
}
""";

        string appSource = """
using MessagePack;
using Lib;

namespace App;

[MessagePackObject]
public partial class AppObj
{
    [Key(0)]
    public UnannotatedType Member { get; set; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        CSharpCompilation libCompilation = CreateCompilation("Lib", libSource);
        using var ms = new MemoryStream();
        var emitResult = libCompilation.Emit(ms);
        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics));
        ms.Position = 0;
        MetadataReference libRef = MetadataReference.CreateFromStream(ms);

        RunGenerator(appSource, out _, out ImmutableArray<Diagnostic> diagnostics, libRef);

        Assert.Contains(diagnostics, d => d.Id.StartsWith("MsgPack", StringComparison.Ordinal));
    }

    [Fact]
    public void ClosedGenericRecordStruct_FromCompilationReference_GeneratesWithoutDiagnostics()
    {
        string libSource = """
using MessagePack;

namespace Lib;

[MessagePackObject]
public partial record struct Item<T>([property: Key(0)] string Key);
""";

        string appSource = """
using MessagePack;
using Lib;

namespace App;

[MessagePackObject]
public sealed partial record Cmd([property: Key(0)] string Scope)
{
    [Key(1)]
    public Item<int> Member { get; init; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        CSharpCompilation libCompilation = CreateCompilation("Lib", libSource);
        MetadataReference libRef = libCompilation.ToMetadataReference();

        RunGenerator(appSource, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics, libRef);

        Assert.Empty(diagnostics);
        Assert.Contains(outputCompilation.SyntaxTrees, t => t.FilePath.EndsWith("App.AppResolver.g.cs", StringComparison.Ordinal));
    }

    private static CSharpCompilation CreateCompilation(string assemblyName, string source, params MetadataReference[] extraReferences)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, parseOptions);
        MetadataReference[] references = GetMetadataReferences().Concat(extraReferences).ToArray();
        return CSharpCompilation.Create(
            assemblyName,
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static void RunGenerator(string source, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics, params MetadataReference[] extraReferences)
    {
        CSharpCompilation compilation = CreateCompilation("App", source, extraReferences);
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new MessagePackGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out diagnostics);
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        _ = typeof(MessagePackObjectAttribute).Assembly;
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToArray();
    }
}
