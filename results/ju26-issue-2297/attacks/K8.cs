// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

public class AyaK8Attack
{
    [Fact]
    public async Task ResolverWithExistingExcludeFromCodeCoverageAttributeCompilesWithoutErrors()
    {
        const string source = """
            using System.Diagnostics.CodeAnalysis;
            using MessagePack;

            [MessagePackObject]
            public class MyItem
            {
                [Key(0)]
                public int Id { get; set; }
            }

            namespace CustomNamespace
            {
                [GeneratedMessagePackResolver]
                [ExcludeFromCodeCoverage]
                partial class CustomResolver
                {
                }
            }
            """;

        (Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) = await RunGeneratorAndCompileAsync(source);

        AssertNoErrors(generatorDiagnostics, "generator");
        AssertNoErrors(outputCompilation.GetDiagnostics(), "compilation");
    }

    [Fact]
    public async Task NestedResolverWithConstructedTypesCompilesWithoutErrors()
    {
        const string source = """
            using System.Collections.Generic;
            using MessagePack;

            [MessagePackObject]
            public class ContainerObject
            {
                [Key(0)]
                public List<int> Values { get; set; }
            }

            public partial class OuterContainer
            {
                [GeneratedMessagePackResolver]
                public partial class InnerResolver
                {
                }
            }
            """;

        (Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) = await RunGeneratorAndCompileAsync(source);

        AssertNoErrors(generatorDiagnostics, "generator");
        AssertNoErrors(outputCompilation.GetDiagnostics(), "compilation");

        INamedTypeSymbol? resolverType = outputCompilation.GetTypeByMetadataName("OuterContainer+InnerResolver");
        Assert.NotNull(resolverType);
        Assert.Contains(resolverType.AllInterfaces, i => i.ToDisplayString() == "MessagePack.IFormatterResolver");
    }

    [Theory]
    [InlineData("NETSTANDARD2_0")]
    [InlineData("NETFRAMEWORK")]
    public async Task ResolverCompilesWhenTargetFrameworkExclusionSymbolIsDefined(string preprocessorSymbol)
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class SimpleObject
            {
                [Key(0)]
                public string Name { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        (Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) = await RunGeneratorAndCompileAsync(source, preprocessorSymbol);

        AssertNoErrors(generatorDiagnostics, "generator");
        AssertNoErrors(outputCompilation.GetDiagnostics(), "compilation");
    }

    [Fact]
    public async Task PublicResolverInNamespaceWithXmlDocCompilesWithoutErrors()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject(keyAsPropertyName: true)]
            public class NamedObject
            {
                public string Title { get; set; }
            }

            namespace MyApp.Serialization
            {
                /// <summary>Custom resolver.</summary>
                [GeneratedMessagePackResolver]
                public partial class PublicResolver
                {
                }
            }
            """;

        (Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) = await RunGeneratorAndCompileAsync(source);

        AssertNoErrors(generatorDiagnostics, "generator");
        AssertNoErrors(outputCompilation.GetDiagnostics(), "compilation");

        INamedTypeSymbol? resolverType = outputCompilation.GetTypeByMetadataName("MyApp.Serialization.PublicResolver");
        Assert.NotNull(resolverType);
        Assert.NotNull(resolverType.GetMembers("Instance").FirstOrDefault());
    }

    private static void AssertNoErrors(ImmutableArray<Diagnostic> diagnostics, string phase)
    {
        List<Diagnostic> errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(
            errors.Count == 0,
            $"Expected no {phase} errors, but found:{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(e => e.ToString()))}");
    }

    private static async Task<(Compilation OutputCompilation, ImmutableArray<Diagnostic> GeneratorDiagnostics)> RunGeneratorAndCompileAsync(
        string source,
        params string[] preprocessorSymbols)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        CSharpParseOptions parseOptions = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: preprocessorSymbols);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaK8AttackTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new MessagePackGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);

        return (outputCompilation, generatorDiagnostics);
    }
}
