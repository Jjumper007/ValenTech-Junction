// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;

public class AyaA5Attack
{
    [Fact]
    public async Task NestedStringKeyObjectInNamespace_CompilesWithoutErrors()
    {
        const string source = """
            using MessagePack;

            namespace Sample.Nested
            {
                public partial class Outer
                {
                    [MessagePackObject(keyAsPropertyName: true)]
                    public class InnerStringKey
                    {
                        public string Name { get; set; }
                        public int Count { get; set; }
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

        (Compilation updatedCompilation, GeneratorDriverRunResult result, var generatorDiagnostics) =
            await RunGeneratorAsync(source);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(updatedCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(
            result.GeneratedTrees,
            t => t.GetText().ToString().Contains("IMessagePackFormatter<global::Sample.Nested.Outer.InnerStringKey>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenericStringKeyObjectWithConstraints_CompilesWithoutErrors()
    {
        const string source = """
            using MessagePack;

            namespace Sample.Generics
            {
                [MessagePackObject(keyAsPropertyName: true)]
                public class GenericStringKey<T>
                    where T : class
                {
                    public T Item { get; set; }
                    public string Label { get; set; }
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

        (Compilation updatedCompilation, GeneratorDriverRunResult result, var generatorDiagnostics) =
            await RunGeneratorAsync(source);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(updatedCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(
            result.GeneratedTrees,
            t => t.GetText().ToString().Contains("IMessagePackFormatter<global::Sample.Generics.GenericStringKey<T>>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmptyStringKeyObject_CompilesWithoutErrors()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject(keyAsPropertyName: true)]
            public class EmptyStringKeyObject
            {
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        (Compilation updatedCompilation, GeneratorDriverRunResult result, var generatorDiagnostics) =
            await RunGeneratorAsync(source);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(updatedCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(
            result.GeneratedTrees,
            t => t.GetText().ToString().Contains("IMessagePackFormatter<global::EmptyStringKeyObject>", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("NETSTANDARD2_0")]
    [InlineData("NETFRAMEWORK")]
    public async Task StringKeyObject_CompilesUnderLegacyTargetSymbols(string preprocessorSymbol)
    {
        const string source = """
            using MessagePack;

            [MessagePackObject(keyAsPropertyName: true)]
            public class TargetSymbolStringKeyObject
            {
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

        (Compilation updatedCompilation, GeneratorDriverRunResult result, var generatorDiagnostics) =
            await RunGeneratorAsync(source, [preprocessorSymbol]);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(updatedCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(
            result.GeneratedTrees,
            t => t.GetText().ToString().Contains("IMessagePackFormatter<global::TargetSymbolStringKeyObject>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StringKeyFormatter_DeclaresInternalSealedFormatterClass()
    {
        const string source = """
            using MessagePack;

            namespace Sample.Formatting
            {
                [MessagePackObject(keyAsPropertyName: true)]
                public class MyStringKeyRecord
                {
                    public string Title { get; set; }
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

        (Compilation updatedCompilation, GeneratorDriverRunResult result, var generatorDiagnostics) =
            await RunGeneratorAsync(source);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(updatedCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        SyntaxTree formatterTree = result.GeneratedTrees.Single(t =>
            t.GetText().ToString().Contains("IMessagePackFormatter<global::Sample.Formatting.MyStringKeyRecord>", StringComparison.Ordinal));

        SemanticModel semanticModel = updatedCompilation.GetSemanticModel(formatterTree);
        ClassDeclarationSyntax classDecl = formatterTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        INamedTypeSymbol symbol = semanticModel.GetDeclaredSymbol(classDecl);

        Assert.NotNull(symbol);
        Assert.Equal(Accessibility.Internal, symbol.DeclaredAccessibility);
        Assert.True(symbol.IsSealed);
        Assert.Contains(symbol.AllInterfaces, i => i.Name == "IMessagePackFormatter");
    }

    private static async Task<(Compilation UpdatedCompilation, GeneratorDriverRunResult Result, IReadOnlyList<Diagnostic> GeneratorDiagnostics)> RunGeneratorAsync(
        string source,
        IEnumerable<string> preprocessorSymbols = null)
    {
        var frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: preprocessorSymbols);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: nameof(AyaA5Attack),
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePackGenerator()).WithUpdatedParseOptions(parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updatedCompilation, out var generatorDiagnostics);
        return (updatedCompilation, driver.GetRunResult(), generatorDiagnostics);
    }
}
