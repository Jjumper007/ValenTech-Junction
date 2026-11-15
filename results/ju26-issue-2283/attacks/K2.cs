// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using Xunit.Abstractions;

public class AyaK2Attack
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaK2Attack(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview));

        MetadataReference[] references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(MessagePackObjectAttribute).Assembly.Location),
            })
            .Distinct()
            .ToArray();

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static ImmutableArray<Diagnostic> RunGeneratorAndGetDiagnostics(string source)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        MessagePackGenerator generator = new MessagePackGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> diagnostics);
        return diagnostics;
    }

    /// <summary>
    /// Positional records declare synthesized properties whose DeclaringSyntaxReferences point to a
    /// <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.ParameterSyntax"/>, not a <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax"/>.
    /// Casting to PropertyDeclarationSyntax with `as` yields null and loses the source location of diagnostics on positional record properties!
    /// </summary>
    [Fact]
    public void PositionalRecord_PublicPropertyWithoutKey_ReportsDiagnosticAtSourceLocation()
    {
        string source = """
using MessagePack;

namespace TempProject;

[MessagePackObject]
public partial record MyPositionalRecord(int MissingKeyProp);
""";

        ImmutableArray<Diagnostic> diagnostics = RunGeneratorAndGetDiagnostics(source);

        Diagnostic diag = Assert.Single(diagnostics, d => d.Id == "MsgPack004");
        Assert.True(diag.Location.IsInSource, $"Expected diagnostic location to be in source, but was {diag.Location}.");
        FileLinePositionSpan span = diag.Location.GetLineSpan();
        Assert.Equal(5, span.StartLinePosition.Line);
    }

    /// <summary>
    /// Positional record structs also synthesize properties backed by ParameterSyntax.
    /// If a positional property has an invalid attribute or missing key, the diagnostic must be reported with a valid source location.
    /// </summary>
    [Fact]
    public void PositionalRecordStruct_PublicPropertyWithoutKey_ReportsDiagnosticAtSourceLocation()
    {
        string source = """
using MessagePack;

namespace TempProject;

[MessagePackObject]
public partial record struct MyPositionalRecordStruct([property: Key(0)] int ValidProp, string MissingKeyProp);
""";

        ImmutableArray<Diagnostic> diagnostics = RunGeneratorAndGetDiagnostics(source);

        Diagnostic diag = Assert.Single(diagnostics, d => d.Id == "MsgPack004");
        Assert.True(diag.Location.IsInSource, $"Expected diagnostic location to be in source, but was {diag.Location}.");
    }

    /// <summary>
    /// An indexer is represented by IPropertySymbol in Roslyn, but its syntax node is IndexerDeclarationSyntax,
    /// not PropertyDeclarationSyntax. If an indexer is annotated with [Key], any diagnostic on it must retain its source location.
    /// </summary>
    [Fact]
    public void IndexerWithKeyAttribute_DoesNotCrashAndReportsDiagnosticInSourceIfApplicable()
    {
        string source = """
using MessagePack;

namespace TempProject;

[MessagePackObject]
public partial class WithIndexer
{
    [Key(0)]
    public int NormalProp { get; set; }

    public int this[int index]
    {
        get => NormalProp;
        set => NormalProp = value;
    }
}
""";

        ImmutableArray<Diagnostic> diagnostics = RunGeneratorAndGetDiagnostics(source);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>
    /// Partial properties (C# 13) have multiple DeclaringSyntaxReferences (defining and implementing declarations).
    /// When a diagnostic is reported on a partial property missing [Key], its location must be in source.
    /// </summary>
    [Fact]
    public void PartialProperty_MissingKey_ReportsDiagnosticAtSourceLocation()
    {
        string source = """
using MessagePack;

namespace TempProject;

[MessagePackObject]
public partial class MyPartialPropClass
{
    public partial int PropWithoutKey { get; set; }
}

public partial class MyPartialPropClass
{
    private int _field;
    public partial int PropWithoutKey
    {
        get => _field;
        set => _field = value;
    }
}
""";

        ImmutableArray<Diagnostic> diagnostics = RunGeneratorAndGetDiagnostics(source);

        Diagnostic diag = Assert.Single(diagnostics, d => d.Id == "MsgPack004");
        Assert.True(diag.Location.IsInSource, $"Expected diagnostic location to be in source, but was {diag.Location}.");
    }
}
