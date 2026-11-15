// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaA4Attack
{
    [Fact]
    public void PositionalRecord_MissingKeyAttribute_ReportsDiagnosticInSource()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject]
public partial record MyRecord(int UnkeyedValue);

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        (ImmutableArray<Diagnostic> diagnostics, SyntaxTree syntaxTree) = RunGenerator(source);

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Location.IsInSource && d.Location.SourceTree == syntaxTree && d.Location.SourceSpan.Length > 0);
    }

    [Fact]
    public void PositionalRecordStruct_DuplicateKeyAttribute_ReportsDiagnosticInSource()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject]
public partial record struct MyRecordStruct([property: Key(0)] int First, [property: Key(0)] int Second);

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        (ImmutableArray<Diagnostic> diagnostics, SyntaxTree syntaxTree) = RunGenerator(source);

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Location.IsInSource && d.Location.SourceTree == syntaxTree && d.Location.SourceSpan.Length > 0);
    }

    [Fact]
    public void PositionalRecord_UnserializablePropertyType_ReportsDiagnosticInSource()
    {
        string source = """
using MessagePack;

namespace App;

public class Unannotated
{
    public int Value { get; set; }
}

[MessagePackObject]
public partial record MyRecord([property: Key(0)] Unannotated BadMember);

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        (ImmutableArray<Diagnostic> diagnostics, SyntaxTree syntaxTree) = RunGenerator(source);

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Location.IsInSource && d.Location.SourceTree == syntaxTree && d.Location.SourceSpan.Length > 0);
    }

    [Fact]
    public void PositionalRecord_ConflictingKeyAndIgnoreMember_ReportsDiagnosticInSource()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject]
public partial record MyRecord([property: Key(0), IgnoreMember] int Conflicting);

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        (ImmutableArray<Diagnostic> diagnostics, SyntaxTree syntaxTree) = RunGenerator(source);

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Location.IsInSource && d.Location.SourceTree == syntaxTree && d.Location.SourceSpan.Length > 0);
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, SyntaxTree SyntaxTree) RunGenerator(string source)
    {
        CSharpParseOptions parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePack.SourceGenerator.MessagePackGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> diagnostics);
        return (diagnostics, syntaxTree);
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        IEnumerable<string> assemblyPaths = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Concat(new[]
            {
                typeof(object).Assembly.Location,
                typeof(Enumerable).Assembly.Location,
                typeof(MessagePack.MessagePackObjectAttribute).Assembly.Location,
                typeof(MessagePack.KeyAttribute).Assembly.Location,
                typeof(MessagePack.IgnoreMemberAttribute).Assembly.Location,
                typeof(MessagePack.GeneratedMessagePackResolverAttribute).Assembly.Location,
            })
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return assemblyPaths.Select(path => MetadataReference.CreateFromFile(path));
    }
}
