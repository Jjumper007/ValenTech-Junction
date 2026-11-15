// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaK9Attack
{
    [Fact]
    public async Task GeneratedResolverCompilesWhenUserClassHasExcludeFromCodeCoverageAttribute()
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
                internal partial class MyResolver
                {
                }
            }
            """;

        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics, GeneratorDriverRunResult runResult) =
            await RunGeneratorAndCompileAsync(source);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(runResult.GeneratedTrees);

        Diagnostic[] errors = updatedCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task GeneratedNestedResolverWithUserExcludeFromCodeCoverageCompilesWithoutErrors()
    {
        const string source = """
            using System.Diagnostics.CodeAnalysis;
            using MessagePack;

            [MessagePackObject]
            public class SampleItem
            {
                [Key(0)]
                public string Name { get; set; }
            }

            namespace MyApp
            {
                public partial class OuterContainer
                {
                    [ExcludeFromCodeCoverage]
                    [GeneratedMessagePackResolver]
                    public partial class NestedResolver
                    {
                    }
                }
            }
            """;

        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics, GeneratorDriverRunResult runResult) =
            await RunGeneratorAndCompileAsync(source);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(runResult.GeneratedTrees);

        Diagnostic[] errors = updatedCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task GeneratedResolverCompilesWithNetStandard20PreprocessorSymbol()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class Item
            {
                [Key(0)]
                public int Id { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics, GeneratorDriverRunResult runResult) =
            await RunGeneratorAndCompileAsync(source, "NETSTANDARD2_0");

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(runResult.GeneratedTrees);

        Diagnostic[] errors = updatedCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task GeneratedResolverCompilesWithNetFrameworkPreprocessorSymbol()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class Item
            {
                [Key(0)]
                public int Id { get; set; }
            }

            namespace MyApp
            {
                public partial class Container
                {
                    [GeneratedMessagePackResolver]
                    partial class CustomResolver
                    {
                    }
                }
            }
            """;

        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics, GeneratorDriverRunResult runResult) =
            await RunGeneratorAndCompileAsync(source, "NETFRAMEWORK");

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(runResult.GeneratedTrees);

        Diagnostic[] errors = updatedCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    private static async Task<(Compilation UpdatedCompilation, ImmutableArray<Diagnostic> GeneratorDiagnostics, GeneratorDriverRunResult RunResult)> RunGeneratorAndCompileAsync(
        string source,
        params string[] preprocessorSymbols)
    {
        ImmutableArray<MetadataReference> frameworkReferences =
            await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: preprocessorSymbols);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: nameof(AyaK9Attack),
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation updatedCompilation,
            out ImmutableArray<Diagnostic> generatorDiagnostics);

        return (updatedCompilation, generatorDiagnostics, driver.GetRunResult());
    }
}
