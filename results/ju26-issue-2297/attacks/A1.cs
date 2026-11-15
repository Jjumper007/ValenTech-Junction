// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaA1Attack
{
    [Fact]
    public async Task NestedMessagePackObjectInGenericClassWithConstraints_CompilesCleanly()
    {
        const string source = """
            using MessagePack;

            public partial class Outer<T> where T : class
            {
                [MessagePackObject]
                public class Inner
                {
                    [Key(0)]
                    public int Value { get; set; }
                }
            }
            """;

        (GeneratorDriverRunResult result, Compilation outputCompilation) = await RunGeneratorAsync(source);

        Assert.NotEmpty(result.GeneratedTrees);
        AssertNoErrors(result, outputCompilation);

        string allGenerated = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        Assert.Contains("partial class Outer<T>", allGenerated);
        Assert.Contains("where T : class", allGenerated);
    }

    [Fact]
    public async Task DeeplyNestedMessagePackObjectInMultipleConstrainedGenericClasses_CompilesCleanly()
    {
        const string source = """
            using MessagePack;

            public partial class Outer<T> where T : class
            {
                public partial class Middle<U> where U : struct
                {
                    [MessagePackObject]
                    public class Inner
                    {
                        [Key(0)]
                        public int Value { get; set; }
                    }
                }
            }
            """;

        (GeneratorDriverRunResult result, Compilation outputCompilation) = await RunGeneratorAsync(source);

        Assert.NotEmpty(result.GeneratedTrees);
        AssertNoErrors(result, outputCompilation);

        string allGenerated = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        Assert.Contains("partial class Outer<T>", allGenerated);
        Assert.Contains("where T : class", allGenerated);
        Assert.Contains("partial class Middle<U>", allGenerated);
        Assert.Contains("where U : struct", allGenerated);
    }

    [Fact]
    public async Task NestedStringKeyObjectEnumAndUnionInConstrainedGenericClass_CompilesCleanly()
    {
        const string source = """
            using MessagePack;

            public partial class Container<T> where T : new()
            {
                [MessagePackObject(keyAsPropertyName: true)]
                public class StringKeyObject
                {
                    public string Name { get; set; }
                }

                public enum NestedEnum
                {
                    First,
                    Second,
                }

                [Union(0, typeof(UnionMember))]
                public interface INestedUnion
                {
                }

                [MessagePackObject]
                public class UnionMember : INestedUnion
                {
                    [Key(0)]
                    public int Id { get; set; }
                }
            }
            """;

        (GeneratorDriverRunResult result, Compilation outputCompilation) = await RunGeneratorAsync(source);

        Assert.True(result.GeneratedTrees.Length >= 3, $"Expected at least 3 generated trees, got {result.GeneratedTrees.Length}.");
        AssertNoErrors(result, outputCompilation);
    }

    [Fact]
    public async Task NestedResolverInNamespaceAndConstrainedGenericClass_CompilesCleanly()
    {
        const string source = """
            using MessagePack;

            namespace MyNamespace
            {
                [MessagePackObject]
                public class SampleObject
                {
                    [Key(0)]
                    public int Value { get; set; }
                }

                public partial class Host<T> where T : class
                {
                    [GeneratedMessagePackResolver]
                    partial class MyResolver
                    {
                    }
                }
            }
            """;

        (GeneratorDriverRunResult result, Compilation outputCompilation) = await RunGeneratorAsync(source);

        Assert.NotEmpty(result.GeneratedTrees);
        AssertNoErrors(result, outputCompilation);

        string allGenerated = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        Assert.Contains("namespace MyNamespace", allGenerated);
        Assert.Contains("partial class Host<T>", allGenerated);
        Assert.Contains("where T : class", allGenerated);
    }

    [Fact]
    public async Task NestedConstrainedGenericClass_CompilesWithNetStandard20Symbol()
    {
        const string source = """
            using MessagePack;

            public partial class Outer<T> where T : class
            {
                [MessagePackObject]
                public class Inner
                {
                    [Key(0)]
                    public int Value { get; set; }
                }
            }
            """;

        (GeneratorDriverRunResult result, Compilation outputCompilation) = await RunGeneratorAsync(source, ["NETSTANDARD2_0"]);

        Assert.NotEmpty(result.GeneratedTrees);
        AssertNoErrors(result, outputCompilation);
    }

    private static void AssertNoErrors(GeneratorDriverRunResult result, Compilation outputCompilation)
    {
        Diagnostic[] generatorErrors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(generatorErrors);

        Diagnostic[] compilationErrors = outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(compilationErrors);
    }

    private static async Task<(GeneratorDriverRunResult Result, Compilation OutputCompilation)> RunGeneratorAsync(
        string source,
        IEnumerable<string>? preprocessorSymbols = null)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: preprocessorSymbols);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaA1AttackTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new MessagePackGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out _);

        return (driver.GetRunResult(), outputCompilation);
    }
}
