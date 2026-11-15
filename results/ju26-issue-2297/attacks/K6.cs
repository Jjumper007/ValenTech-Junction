// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaK6Attack
{
    private const string ObjectSource = """
        using MessagePack;

        [MessagePackObject]
        public class MyObject
        {
            [Key(0)]
            public int Value { get; set; }
        }

        """;

    [Fact]
    public async Task GeneratedResolverCompilesWithoutErrors()
    {
        string source = ObjectSource + """
            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        (Compilation output, _) = await RunAsync(source, new CSharpParseOptions(LanguageVersion.Latest));
        AssertNoErrors(output);
    }

    [Fact]
    public async Task GeneratedResolverCompilesWhenNetStandardSymbolDefined()
    {
        string source = ObjectSource + """
            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        var options = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: new[] { "NETSTANDARD2_0", "NETFRAMEWORK" });
        (Compilation output, _) = await RunAsync(source, options);
        AssertNoErrors(output);
    }

    [Fact]
    public async Task UserPartialResolverAlreadyExcludedFromCoverageStillCompiles()
    {
        // Before the change, a user could put [ExcludeFromCodeCoverage] on their own partial resolver declaration.
        // The attribute does not allow multiple uses, so the generator must not cause a duplicate attribute error.
        string source = ObjectSource + """
            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        (Compilation output, _) = await RunAsync(source, new CSharpParseOptions(LanguageVersion.Latest));
        AssertNoErrors(output);
    }

    [Fact]
    public async Task NestedResolverCompilesWithoutErrors()
    {
        string source = ObjectSource + """
            namespace My.Space
            {
                partial class Outer
                {
                    [GeneratedMessagePackResolver]
                    partial class InnerResolver
                    {
                    }
                }
            }
            """;

        (Compilation output, _) = await RunAsync(source, new CSharpParseOptions(LanguageVersion.Latest));
        AssertNoErrors(output);
    }

    [Fact]
    public async Task ResolverDocCommentStillAttachesToType()
    {
        string source = ObjectSource + """
            namespace MessagePack
            {
                /// <summary>Resolver.</summary>
                [GeneratedMessagePackResolver]
                public partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        var options = new CSharpParseOptions(LanguageVersion.Latest, documentationMode: DocumentationMode.Diagnose);
        (Compilation output, IReadOnlyList<SyntaxTree> generatedTrees) = await RunAsync(source, options);
        AssertNoErrors(output);

        var misplaced = output.GetDiagnostics()
            .Where(d => d.Id == "CS1587" && d.Location.SourceTree is not null && generatedTrees.Contains(d.Location.SourceTree))
            .ToList();
        Assert.True(misplaced.Count == 0, string.Join(Environment.NewLine, misplaced));
    }

    private static void AssertNoErrors(Compilation output)
    {
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    private static async Task<(Compilation Output, IReadOnlyList<SyntaxTree> Generated)> RunAsync(string source, CSharpParseOptions parseOptions)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaK6Attack",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new MessagePackGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();

        Assert.NotEmpty(result.GeneratedTrees);
        return (output, result.GeneratedTrees.ToList());
    }
}
