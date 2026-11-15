// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

public class AyaK10Attack
{
    [Fact]
    public async Task ResolverWithExistingExcludeFromCodeCoverageAttribute_CompilesWithoutErrors()
    {
        const string source = """
            using System.Diagnostics.CodeAnalysis;
            using MessagePack;

            [MessagePackObject]
            public class SampleObject
            {
                [Key(0)]
                public int Value { get; set; }
            }

            namespace SampleNamespace
            {
                [ExcludeFromCodeCoverage]
                [GeneratedMessagePackResolver]
                partial class SampleResolver
                {
                }
            }
            """;

        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) =
            await RunGeneratorAsync(source);

        AssertNoErrors(generatorDiagnostics, "generator");
        AssertNoErrors(updatedCompilation.GetDiagnostics(), "compilation");

        using var peStream = new MemoryStream();
        EmitResult emitResult = updatedCompilation.Emit(peStream);
        AssertNoErrors(emitResult.Diagnostics, "emit");
        Assert.True(emitResult.Success);
    }

    [Fact]
    public async Task NestedResolverWithExistingExcludeFromCodeCoverageAttribute_CompilesWithoutErrors()
    {
        const string source = """
            using System.Diagnostics.CodeAnalysis;
            using MessagePack;

            [MessagePackObject]
            public class NestedSampleObject
            {
                [Key(0)]
                public string Name { get; set; }
            }

            namespace SampleNamespace
            {
                public partial class OuterContainer
                {
                    [ExcludeFromCodeCoverage]
                    [GeneratedMessagePackResolver]
                    internal partial class NestedResolver
                    {
                    }
                }
            }
            """;

        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) =
            await RunGeneratorAsync(source);

        AssertNoErrors(generatorDiagnostics, "generator");
        AssertNoErrors(updatedCompilation.GetDiagnostics(), "compilation");

        INamedTypeSymbol resolverSymbol = updatedCompilation.GetTypeByMetadataName("SampleNamespace.OuterContainer+NestedResolver");
        Assert.NotNull(resolverSymbol);
        Assert.Contains(resolverSymbol.Interfaces, i => i.ToDisplayString() == "MessagePack.IFormatterResolver");
    }

    [Fact]
    public async Task ResolverCompilesAndEmits_WhenNetStandard20Defined()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class NetStandardObject
            {
                [Key(0)]
                public int Id { get; set; }
            }

            namespace SampleNamespace
            {
                [GeneratedMessagePackResolver]
                partial class NetStandardResolver
                {
                }
            }
            """;

        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) =
            await RunGeneratorAsync(source, preprocessorSymbols: ["NETSTANDARD2_0"]);

        AssertNoErrors(generatorDiagnostics, "generator");
        AssertNoErrors(updatedCompilation.GetDiagnostics(), "compilation");

        using var peStream = new MemoryStream();
        EmitResult emitResult = updatedCompilation.Emit(peStream);
        AssertNoErrors(emitResult.Diagnostics, "emit");
        Assert.True(emitResult.Success);
    }

    [Fact]
    public async Task ResolverCompilesAndEmits_WhenNetFrameworkDefined()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class NetFrameworkObject
            {
                [Key(0)]
                public int Id { get; set; }
            }

            namespace SampleNamespace
            {
                [GeneratedMessagePackResolver]
                partial class NetFrameworkResolver
                {
                }
            }
            """;

        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) =
            await RunGeneratorAsync(source, preprocessorSymbols: ["NETFRAMEWORK"]);

        AssertNoErrors(generatorDiagnostics, "generator");
        AssertNoErrors(updatedCompilation.GetDiagnostics(), "compilation");

        using var peStream = new MemoryStream();
        EmitResult emitResult = updatedCompilation.Emit(peStream);
        AssertNoErrors(emitResult.Diagnostics, "emit");
        Assert.True(emitResult.Success);
    }

    [Fact]
    public async Task ResolverInGlobalNamespace_CompilesAndImplementsIFormatterResolver()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class GlobalObject
            {
                [Key(0)]
                public int Value { get; set; }
            }

            [GeneratedMessagePackResolver]
            partial class GlobalResolver
            {
            }
            """;

        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) =
            await RunGeneratorAsync(source);

        AssertNoErrors(generatorDiagnostics, "generator");
        AssertNoErrors(updatedCompilation.GetDiagnostics(), "compilation");

        INamedTypeSymbol resolverSymbol = updatedCompilation.GetTypeByMetadataName("GlobalResolver");
        Assert.NotNull(resolverSymbol);
        Assert.Contains(resolverSymbol.Interfaces, i => i.ToDisplayString() == "MessagePack.IFormatterResolver");
        Assert.NotEmpty(resolverSymbol.GetMembers("Instance"));
        Assert.NotEmpty(resolverSymbol.GetMembers("GetFormatter"));

        using var peStream = new MemoryStream();
        EmitResult emitResult = updatedCompilation.Emit(peStream);
        AssertNoErrors(emitResult.Diagnostics, "emit");
        Assert.True(emitResult.Success);
    }

    private static void AssertNoErrors(IEnumerable<Diagnostic> diagnostics, string stage)
    {
        List<Diagnostic> errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(
            errors.Count == 0,
            $"Unexpected {stage} errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(e => e.ToString()))}");
    }

    private static async Task<(Compilation UpdatedCompilation, ImmutableArray<Diagnostic> GeneratorDiagnostics)> RunGeneratorAsync(
        string source,
        IEnumerable<string> preprocessorSymbols = null)
    {
        ImmutableArray<MetadataReference> frameworkReferences =
            await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        var parseOptions = new CSharpParseOptions(
            LanguageVersion.Latest,
            preprocessorSymbols: preprocessorSymbols);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaK10AttackAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create([new MessagePackGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updatedCompilation, out ImmutableArray<Diagnostic> diagnostics);

        return (updatedCompilation, diagnostics);
    }
}
