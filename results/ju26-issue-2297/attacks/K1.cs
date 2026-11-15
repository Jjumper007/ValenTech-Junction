// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaK1Attack
{
    private static async Task<(IReadOnlyList<string> Generated, IReadOnlyList<Diagnostic> Errors)> RunAsync(string source, params string[] preprocessorSymbols)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: preprocessorSymbols);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaK1Attack",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MessagePackGenerator()).WithUpdatedParseOptions(parseOptions);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();

        List<string> generated = result.GeneratedTrees.Select(t => t.GetText().ToString()).ToList();
        List<Diagnostic> errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        return (generated, errors);
    }

    private static string Describe(IReadOnlyList<Diagnostic> errors)
        => string.Join(Environment.NewLine, errors.Select(e => e.ToString()));

    [Fact]
    public async Task ResolverInNamespace_GeneratedCodeCompiles()
    {
        const string source = """
            using MessagePack;

            namespace MyNs
            {
                [MessagePackObject]
                public class MyObject
                {
                    [Key(0)]
                    public int Value { get; set; }
                }

                [GeneratedMessagePackResolver]
                partial class MyResolver
                {
                }
            }
            """;

        var (generated, errors) = await RunAsync(source);
        Assert.NotEmpty(generated);
        Assert.True(errors.Count == 0, Describe(errors));
    }

    [Fact]
    public async Task UserResolverAlreadyExcludedFromCoverage_StillCompiles()
    {
        const string source = """
            using MessagePack;

            namespace MyNs
            {
                [MessagePackObject]
                public class MyObject
                {
                    [Key(0)]
                    public int Value { get; set; }
                }

                [GeneratedMessagePackResolver]
                [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                partial class MyResolver
                {
                }
            }
            """;

        var (generated, errors) = await RunAsync(source);
        Assert.NotEmpty(generated);
        Assert.True(errors.Count == 0, Describe(errors));
    }

    [Fact]
    public async Task NestedUserResolverAlreadyExcludedFromCoverage_StillCompiles()
    {
        const string source = """
            using MessagePack;
            using System.Diagnostics.CodeAnalysis;

            namespace MyNs
            {
                [MessagePackObject]
                public class MyObject
                {
                    [Key(0)]
                    public int Value { get; set; }
                }

                partial class Outer
                {
                    [GeneratedMessagePackResolver]
                    [ExcludeFromCodeCoverage]
                    partial class MyResolver
                    {
                    }
                }
            }
            """;

        var (generated, errors) = await RunAsync(source);
        Assert.NotEmpty(generated);
        Assert.True(errors.Count == 0, Describe(errors));
    }

    [Fact]
    public async Task GlobalNamespaceUserResolverAlreadyExcludedFromCoverage_StillCompiles()
    {
        const string source = """
            using MessagePack;
            using System.Diagnostics.CodeAnalysis;

            [MessagePackObject]
            public class MyObject
            {
                [Key(0)]
                public int Value { get; set; }
            }

            [ExcludeFromCodeCoverage]
            [GeneratedMessagePackResolver]
            partial class MyGlobalResolver
            {
            }
            """;

        var (generated, errors) = await RunAsync(source);
        Assert.NotEmpty(generated);
        Assert.True(errors.Count == 0, Describe(errors));
    }

    [Fact]
    public async Task NetFrameworkSymbol_UserResolverAlreadyExcluded_Compiles()
    {
        const string source = """
            using MessagePack;

            namespace MyNs
            {
                [MessagePackObject]
                public class MyObject
                {
                    [Key(0)]
                    public int Value { get; set; }
                }

                [GeneratedMessagePackResolver]
                [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                partial class MyResolver
                {
                }
            }
            """;

        var (generated, errors) = await RunAsync(source, "NETFRAMEWORK");
        Assert.NotEmpty(generated);
        Assert.True(errors.Count == 0, Describe(errors));
    }
}
