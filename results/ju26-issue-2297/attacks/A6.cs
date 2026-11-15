// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaA6Attack
{
    [Fact]
    public async Task UnionInGlobalNamespace_CompilesWithoutDiagnostics()
    {
        const string source = """
            using MessagePack;

            [Union(0, typeof(UnionMemberA))]
            [Union(1, typeof(UnionMemberB))]
            public interface IGlobalUnion
            {
            }

            [MessagePackObject]
            public class UnionMemberA : IGlobalUnion
            {
                [Key(0)]
                public int A { get; set; }
            }

            [MessagePackObject]
            public class UnionMemberB : IGlobalUnion
            {
                [Key(0)]
                public string B { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        (Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics, IReadOnlyList<(string Path, string Text)> generated) =
            await RunGeneratorAndCompileAsync(source);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(generated, g => g.Text.Contains("IMessagePackFormatter<global::IGlobalUnion>"));
    }

    [Fact]
    public async Task UnionInNestedNamespaceAndType_CompilesWithoutDiagnostics()
    {
        const string source = """
            using MessagePack;

            namespace MyApp.Models
            {
                public partial class OuterContainer
                {
                    [Union(0, typeof(NestedImpl))]
                    public interface INestedUnion
                    {
                    }

                    [MessagePackObject]
                    public class NestedImpl : INestedUnion
                    {
                        [Key(0)]
                        public int Value { get; set; }
                    }
                }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        (Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics, IReadOnlyList<(string Path, string Text)> generated) =
            await RunGeneratorAndCompileAsync(source);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(generated, g => g.Text.Contains("IMessagePackFormatter<global::MyApp.Models.OuterContainer.INestedUnion>"));
    }

    [Fact]
    public async Task AbstractClassUnion_CompilesWithoutDiagnostics()
    {
        const string source = """
            using MessagePack;

            namespace MyApp.Unions
            {
                [Union(0, typeof(ConcreteAlpha))]
                [Union(1, typeof(ConcreteBeta))]
                public abstract class AbstractUnionBase
                {
                }

                [MessagePackObject]
                public class ConcreteAlpha : AbstractUnionBase
                {
                    [Key(0)]
                    public int X { get; set; }
                }

                [MessagePackObject]
                public class ConcreteBeta : AbstractUnionBase
                {
                    [Key(0)]
                    public string Y { get; set; }
                }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        (Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics, IReadOnlyList<(string Path, string Text)> generated) =
            await RunGeneratorAndCompileAsync(source);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(generated, g => g.Text.Contains("IMessagePackFormatter<global::MyApp.Unions.AbstractUnionBase>"));
    }

    [Theory]
    [InlineData("NETSTANDARD2_0")]
    [InlineData("NETFRAMEWORK")]
    public async Task UnionFormatter_CompilesWhenLegacyTargetSymbolsAreDefined(string preprocessorSymbol)
    {
        const string source = """
            using MessagePack;

            [Union(0, typeof(LegacyMember))]
            public interface ILegacyUnion
            {
            }

            [MessagePackObject]
            public class LegacyMember : ILegacyUnion
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

        (_, ImmutableArray<Diagnostic> generatorDiagnostics, IReadOnlyList<(string Path, string Text)> generated) =
            await RunGeneratorAndCompileAsync(source);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        CSharpParseOptions parseOptions = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: [preprocessorSymbol]);
        ImmutableArray<MetadataReference> references = await GetReferencesAsync();

        List<SyntaxTree> trees = [CSharpSyntaxTree.ParseText(source, parseOptions)];
        foreach ((string path, string text) in generated)
        {
            trees.Add(CSharpSyntaxTree.ParseText(text, parseOptions, path: path));
        }

        CSharpCompilation recompiled = CSharpCompilation.Create(
            assemblyName: $"AyaA6Attack_{preprocessorSymbol}",
            syntaxTrees: trees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Assert.Empty(recompiled.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(recompiled.GetTypeByMetadataName("MessagePack.Formatters.ILegacyUnionFormatter"));
    }

    [Fact]
    public async Task UnionFormatter_PreservesInternalSealedClassDeclaration()
    {
        const string source = """
            using MessagePack;

            [Union(0, typeof(ItemImpl))]
            public interface IItemUnion
            {
            }

            [MessagePackObject]
            public class ItemImpl : IItemUnion
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

        (Compilation outputCompilation, _, IReadOnlyList<(string Path, string Text)> generated) =
            await RunGeneratorAndCompileAsync(source);

        (string Path, string Text) unionFile = generated.Single(g => g.Text.Contains("IMessagePackFormatter<global::IItemUnion>"));
        Assert.Contains("internal sealed class IItemUnionFormatter: MsgPack::Formatters.IMessagePackFormatter<global::IItemUnion>", unionFile.Text);

        INamedTypeSymbol? formatterSymbol = outputCompilation.GetTypeByMetadataName("MessagePack.Formatters.IItemUnionFormatter");
        Assert.NotNull(formatterSymbol);
        Assert.Equal(Accessibility.Internal, formatterSymbol.DeclaredAccessibility);
        Assert.True(formatterSymbol.IsSealed);
    }

    private static async Task<ImmutableArray<MetadataReference>> GetReferencesAsync()
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        return [.. frameworkReferences, .. ReferencesHelper.GetReferences(ReferencesSet.MessagePack)];
    }

    private static async Task<(Compilation OutputCompilation, ImmutableArray<Diagnostic> GeneratorDiagnostics, IReadOnlyList<(string Path, string Text)> Generated)> RunGeneratorAndCompileAsync(string source)
    {
        ImmutableArray<MetadataReference> references = await GetReferencesAsync();

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaA6Attack",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);
        GeneratorDriverRunResult result = driver.GetRunResult();

        List<(string Path, string Text)> generated = result.GeneratedTrees
            .Select(tree => (tree.FilePath, tree.GetText().ToString()))
            .ToList();

        return (outputCompilation, generatorDiagnostics, generated);
    }
}
