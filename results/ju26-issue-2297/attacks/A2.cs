// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaA2Attack
{
    [Fact]
    public async Task ResolverInNamespaceWithExistingExcludeFromCodeCoverageCompilesWithoutErrors()
    {
        const string source = """
            using System.Diagnostics.CodeAnalysis;
            using MessagePack;

            [MessagePackObject]
            public class MyObject
            {
                [Key(0)]
                public int Value { get; set; }
            }

            namespace MyApp
            {
                [ExcludeFromCodeCoverage]
                [GeneratedMessagePackResolver]
                partial class MyResolver
                {
                }
            }
            """;

        (Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) = await RunGeneratorAsync(source);
        AssertCompilesCleanly(outputCompilation, generatorDiagnostics);
    }

    [Fact]
    public async Task ResolverInGlobalNamespaceWithExistingExcludeFromCodeCoverageCompilesWithoutErrors()
    {
        const string source = """
            using System.Diagnostics.CodeAnalysis;
            using MessagePack;

            [MessagePackObject]
            public class MyObject
            {
                [Key(0)]
                public int Value { get; set; }
            }

            [ExcludeFromCodeCoverage]
            [GeneratedMessagePackResolver]
            partial class GlobalResolver
            {
            }
            """;

        (Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) = await RunGeneratorAsync(source);
        AssertCompilesCleanly(outputCompilation, generatorDiagnostics);
    }

    [Fact]
    public async Task NestedResolverWithExistingExcludeFromCodeCoverageCompilesWithoutErrors()
    {
        const string source = """
            using System.Diagnostics.CodeAnalysis;
            using MessagePack;

            [MessagePackObject]
            public class MyObject
            {
                [Key(0)]
                public int Value { get; set; }
            }

            namespace MyApp
            {
                partial class OuterContainer
                {
                    [ExcludeFromCodeCoverage]
                    [GeneratedMessagePackResolver]
                    partial class NestedResolver
                    {
                    }
                }
            }
            """;

        (Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) = await RunGeneratorAsync(source);
        AssertCompilesCleanly(outputCompilation, generatorDiagnostics);
    }

    [Fact]
    public async Task SplitPartialResolverWithExistingExcludeFromCodeCoverageCompilesWithoutErrors()
    {
        const string resolverSource = """
            using MessagePack;

            [MessagePackObject]
            public class MyObject
            {
                [Key(0)]
                public int Value { get; set; }
            }

            namespace MyApp
            {
                [GeneratedMessagePackResolver]
                partial class SplitResolver
                {
                }
            }
            """;

        const string attributePartSource = """
            using System.Diagnostics.CodeAnalysis;

            namespace MyApp
            {
                [ExcludeFromCodeCoverage]
                partial class SplitResolver
                {
                }
            }
            """;

        (Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics) = await RunGeneratorAsync(resolverSource, attributePartSource);
        AssertCompilesCleanly(outputCompilation, generatorDiagnostics);
    }

    private static void AssertCompilesCleanly(Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics)
    {
        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        Diagnostic[] errors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    private static async Task<(Compilation OutputCompilation, ImmutableArray<Diagnostic> GeneratorDiagnostics)> RunGeneratorAsync(params string[] sources)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        SyntaxTree[] syntaxTrees = sources
            .Select(s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.Latest)))
            .ToArray();

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaA2AttackTests",
            syntaxTrees: syntaxTrees,
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);
        return (outputCompilation, generatorDiagnostics);
    }
}
