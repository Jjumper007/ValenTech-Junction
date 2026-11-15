// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaA7Attack
{
    private const string StandardSource = """
        using MessagePack;

        [MessagePackObject]
        public class SampleObject
        {
            [Key(0)]
            public int Value { get; set; }
        }

        namespace MessagePack
        {
            [GeneratedMessagePackResolver]
            partial class GeneratedMessagePackResolver
            {
            }
        }
        """;

    [Fact]
    public async Task UserDeclaredExcludeFromCodeCoverageOnPartialResolverCompilesWithoutDuplicateAttributeError()
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

            namespace MessagePack
            {
                [ExcludeFromCodeCoverage]
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        (var generatorDiagnostics, var compilationDiagnostics, _) = await RunGeneratorAndCompileAsync(source);

        Assert.Empty(generatorDiagnostics);
        var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
    }

    [Fact]
    public async Task GeneratedResolverCompilesWithoutErrorsInGlobalNamespaceAndNestedType()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class SampleObject
            {
                [Key(0)]
                public int Value { get; set; }
            }

            [GeneratedMessagePackResolver]
            partial class GlobalResolver
            {
            }

            namespace CustomNamespace
            {
                partial class OuterContainer
                {
                    [GeneratedMessagePackResolver]
                    partial class NestedResolver
                    {
                    }
                }
            }
            """;

        (var generatorDiagnostics, var compilationDiagnostics, var outputCompilation) = await RunGeneratorAndCompileAsync(source);

        Assert.Empty(generatorDiagnostics);
        var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(e => e.ToString())));

        var globalResolver = outputCompilation.GetTypeByMetadataName("GlobalResolver");
        Assert.NotNull(globalResolver);
        Assert.Contains(globalResolver.GetMembers(), m => m.Name == "Instance");
        Assert.Contains(globalResolver.GetMembers(), m => m.Name == "GetFormatter");

        var nestedResolver = outputCompilation.GetTypeByMetadataName("CustomNamespace.OuterContainer+NestedResolver");
        Assert.NotNull(nestedResolver);
        Assert.Contains(nestedResolver.GetMembers(), m => m.Name == "Instance");
        Assert.Contains(nestedResolver.GetMembers(), m => m.Name == "GetFormatter");
    }

    [Fact]
    public async Task GeneratedResolverCompilesWithoutErrorsWhenNetStandard20Defined()
    {
        (var generatorDiagnostics, var compilationDiagnostics, var outputCompilation) = await RunGeneratorAndCompileAsync(StandardSource, "NETSTANDARD2_0");

        Assert.Empty(generatorDiagnostics);
        var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
        Assert.NotNull(outputCompilation.GetTypeByMetadataName("MessagePack.GeneratedMessagePackResolver"));
    }

    [Fact]
    public async Task GeneratedResolverCompilesWithoutErrorsWhenNetFrameworkDefined()
    {
        (var generatorDiagnostics, var compilationDiagnostics, var outputCompilation) = await RunGeneratorAndCompileAsync(StandardSource, "NETFRAMEWORK");

        Assert.Empty(generatorDiagnostics);
        var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
        Assert.NotNull(outputCompilation.GetTypeByMetadataName("MessagePack.GeneratedMessagePackResolver"));
    }

    private static async Task<(IReadOnlyList<Diagnostic> GeneratorDiagnostics, IReadOnlyList<Diagnostic> CompilationDiagnostics, Compilation OutputCompilation)> RunGeneratorAndCompileAsync(
        string source,
        params string[] preprocessorSymbols)
    {
        var frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest).WithPreprocessorSymbols(preprocessorSymbols);

        var compilation = CSharpCompilation.Create(
            assemblyName: "AyaA7AttackTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new MessagePackGenerator().AsSourceGenerator() }, parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        return (generatorDiagnostics, outputCompilation.GetDiagnostics(), outputCompilation);
    }
}
