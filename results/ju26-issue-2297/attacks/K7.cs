// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaK7Attack
{
    private const string FormatterDefinition = """
        using System.Diagnostics.CodeAnalysis;
        using MessagePack;
        using MessagePack.Formatters;

        public class MyIntFormatter : IMessagePackFormatter<int>
        {
            public void Serialize(ref MessagePackWriter writer, int value, MessagePackSerializerOptions options)
            {
                writer.Write(value);
            }

            public int Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                return reader.ReadInt32();
            }
        }
        """;

    [Fact]
    public async Task PartialResolverAlreadyAnnotatedWithExcludeFromCodeCoverageCompilesCleanly()
    {
        string source = $$"""
            {{FormatterDefinition}}

            [ExcludeFromCodeCoverage]
            [CompositeResolver(typeof(MyIntFormatter))]
            public partial class MyCompositeResolver
            {
            }
            """;

        (GeneratorDriverRunResult result, Compilation updatedCompilation) = await RunCompositeGeneratorAsync(source);

        AssertGeneratedSuccessfully(result, updatedCompilation);
    }

    [Fact]
    public async Task NestedPartialResolverAlreadyAnnotatedWithExcludeFromCodeCoverageCompilesCleanly()
    {
        string source = $$"""
            {{FormatterDefinition}}

            namespace MyApp
            {
                public partial class OuterContainer
                {
                    [ExcludeFromCodeCoverage]
                    [CompositeResolver(typeof(MyIntFormatter))]
                    private partial class NestedCompositeResolver
                    {
                    }
                }
            }
            """;

        (GeneratorDriverRunResult result, Compilation updatedCompilation) = await RunCompositeGeneratorAsync(source);

        AssertGeneratedSuccessfully(result, updatedCompilation);
    }

    [Fact]
    public async Task SplitPartialResolverWithExcludeFromCodeCoverageOnOtherDeclarationCompilesCleanly()
    {
        string source = $$"""
            {{FormatterDefinition}}

            [CompositeResolver(typeof(MyIntFormatter))]
            public partial class SplitCompositeResolver
            {
            }

            [ExcludeFromCodeCoverage]
            public partial class SplitCompositeResolver
            {
            }
            """;

        (GeneratorDriverRunResult result, Compilation updatedCompilation) = await RunCompositeGeneratorAsync(source);

        AssertGeneratedSuccessfully(result, updatedCompilation);
    }

    [Fact]
    public async Task GeneratedCompositeResolverCompilesWithNetStandard20Symbol()
    {
        string source = $$"""
            {{FormatterDefinition}}

            [CompositeResolver(typeof(MyIntFormatter))]
            public partial class NetStandardCompositeResolver
            {
            }
            """;

        (GeneratorDriverRunResult result, Compilation updatedCompilation) = await RunCompositeGeneratorAsync(source, "NETSTANDARD2_0");

        AssertGeneratedSuccessfully(result, updatedCompilation);
    }

    [Fact]
    public async Task GeneratedCompositeResolverCompilesWithNetFrameworkSymbol()
    {
        string source = $$"""
            {{FormatterDefinition}}

            [CompositeResolver(typeof(MyIntFormatter))]
            public partial class NetFrameworkCompositeResolver
            {
            }
            """;

        (GeneratorDriverRunResult result, Compilation updatedCompilation) = await RunCompositeGeneratorAsync(source, "NETFRAMEWORK");

        AssertGeneratedSuccessfully(result, updatedCompilation);
    }

    private static void AssertGeneratedSuccessfully(GeneratorDriverRunResult result, Compilation updatedCompilation)
    {
        Assert.NotEmpty(result.GeneratedTrees);

        Diagnostic[] generatorErrors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(generatorErrors);

        Diagnostic[] compilationErrors = updatedCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(compilationErrors);
    }

    private static async Task<(GeneratorDriverRunResult Result, Compilation UpdatedCompilation)> RunCompositeGeneratorAsync(
        string source,
        params string[] preprocessorSymbols)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        CSharpParseOptions parseOptions = new CSharpParseOptions(
            LanguageVersion.Latest,
            preprocessorSymbols: preprocessorSymbols);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaK7AttackTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new CompositeResolverGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updatedCompilation, out _);

        return (driver.GetRunResult(), updatedCompilation);
    }
}
