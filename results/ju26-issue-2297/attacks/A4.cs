// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;

public class AyaA4Attack
{
    private const string ExcludeFromCodeCoverageAttributeMetadataName = "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute";

    [Fact]
    public async Task CollectionAndArrayMembers_GeneratedFormatterCompilesAndHasExcludeAttribute()
    {
        const string source = """
            using System.Collections.Generic;
            using MessagePack;

            [MessagePackObject]
            public class CollectionAndArrayMembers
            {
                [Key(0)]
                public int[] Numbers { get; set; }

                [Key(1)]
                public List<string> Names { get; set; }

                [Key(2)]
                public Dictionary<string, int> Counts { get; set; }
            }
            """;

        (Compilation outputCompilation, IReadOnlyList<SyntaxTree> generatedTrees) = await RunGeneratorAndCompileAsync(source);
        AssertGeneratedTreesHaveNoDiagnostics(outputCompilation, generatedTrees);

        INamedTypeSymbol formatterSymbol = GetSingleFormatterSymbol(outputCompilation, generatedTrees);
        Assert.Equal(Accessibility.Internal, formatterSymbol.DeclaredAccessibility);
        Assert.True(formatterSymbol.IsSealed);
        Assert.Contains(formatterSymbol.AllInterfaces, i => i.Name == "IMessagePackFormatter");
        Assert.True(HasExcludeFromCodeCoverageAttribute(formatterSymbol));
    }

    [Fact]
    public async Task EmptyObjectWithoutFormatterResolver_GeneratedFormatterCompilesAndHasExcludeAttribute()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class EmptyObjectWithoutFormatterResolver
            {
            }
            """;

        (Compilation outputCompilation, IReadOnlyList<SyntaxTree> generatedTrees) = await RunGeneratorAndCompileAsync(source);
        AssertGeneratedTreesHaveNoDiagnostics(outputCompilation, generatedTrees);

        INamedTypeSymbol formatterSymbol = GetSingleFormatterSymbol(outputCompilation, generatedTrees);
        Assert.Equal(Accessibility.Internal, formatterSymbol.DeclaredAccessibility);
        Assert.True(formatterSymbol.IsSealed);
        Assert.Contains(formatterSymbol.AllInterfaces, i => i.Name == "IMessagePackFormatter");
        Assert.True(HasExcludeFromCodeCoverageAttribute(formatterSymbol));
    }

    [Fact]
    public async Task GenericObjectWithConstraints_GeneratedFormatterCompilesAndPreservesConstraints()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class GenericObjectWithConstraints<T>
                where T : class, new()
            {
                [Key(0)]
                public T Value { get; set; }
            }
            """;

        (Compilation outputCompilation, IReadOnlyList<SyntaxTree> generatedTrees) = await RunGeneratorAndCompileAsync(source);
        AssertGeneratedTreesHaveNoDiagnostics(outputCompilation, generatedTrees);

        INamedTypeSymbol formatterSymbol = GetSingleFormatterSymbol(outputCompilation, generatedTrees);
        Assert.Equal(Accessibility.Internal, formatterSymbol.DeclaredAccessibility);
        Assert.True(formatterSymbol.IsSealed);
        Assert.True(HasExcludeFromCodeCoverageAttribute(formatterSymbol));
        Assert.Single(formatterSymbol.TypeParameters);
        Assert.True(formatterSymbol.TypeParameters[0].HasReferenceTypeConstraint);
        Assert.True(formatterSymbol.TypeParameters[0].HasConstructorConstraint);
    }

    [Fact]
    public async Task NestedAndNamespacedObject_GeneratedFormatterCompilesAndHasExcludeAttribute()
    {
        const string source = """
            using MessagePack;

            namespace SampleApp.Models
            {
                public partial class OuterContainer
                {
                    [MessagePackObject]
                    public class NestedItem
                    {
                        [Key(0)]
                        public int Id { get; set; }
                    }
                }
            }
            """;

        (Compilation outputCompilation, IReadOnlyList<SyntaxTree> generatedTrees) = await RunGeneratorAndCompileAsync(source);
        AssertGeneratedTreesHaveNoDiagnostics(outputCompilation, generatedTrees);

        INamedTypeSymbol formatterSymbol = GetSingleFormatterSymbol(outputCompilation, generatedTrees);
        Assert.Equal(Accessibility.Internal, formatterSymbol.DeclaredAccessibility);
        Assert.True(formatterSymbol.IsSealed);
        Assert.True(HasExcludeFromCodeCoverageAttribute(formatterSymbol));
    }

    [Theory]
    [InlineData("NETSTANDARD2_0")]
    [InlineData("NETFRAMEWORK")]
    public async Task GuardedTargetSymbols_SuppressExcludeAttributeAndCompileCleanly(string preprocessorSymbol)
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class GuardedTargetSample
            {
                [Key(0)]
                public int Value { get; set; }
            }
            """;

        (Compilation outputCompilation, IReadOnlyList<SyntaxTree> generatedTrees) = await RunGeneratorAndCompileAsync(source, preprocessorSymbol);
        AssertGeneratedTreesHaveNoDiagnostics(outputCompilation, generatedTrees);

        INamedTypeSymbol formatterSymbol = GetSingleFormatterSymbol(outputCompilation, generatedTrees);
        Assert.Equal(Accessibility.Internal, formatterSymbol.DeclaredAccessibility);
        Assert.True(formatterSymbol.IsSealed);
        Assert.False(HasExcludeFromCodeCoverageAttribute(formatterSymbol));
    }

    private static async Task<(Compilation OutputCompilation, IReadOnlyList<SyntaxTree> GeneratedTrees)> RunGeneratorAndCompileAsync(
        string source,
        params string[] preprocessorSymbols)
    {
        var frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: preprocessorSymbols);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: nameof(AyaA4Attack),
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out var generatorDiagnostics);

        Assert.DoesNotContain(generatorDiagnostics, d => d.Severity >= DiagnosticSeverity.Warning);

        GeneratorDriverRunResult result = driver.GetRunResult();
        return (outputCompilation, result.GeneratedTrees);
    }

    private static void AssertGeneratedTreesHaveNoDiagnostics(Compilation compilation, IReadOnlyList<SyntaxTree> generatedTrees)
    {
        Assert.NotEmpty(generatedTrees);

        var generatedTreeSet = new HashSet<SyntaxTree>(generatedTrees);
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning && d.Location.SourceTree is not null && generatedTreeSet.Contains(d.Location.SourceTree))
            .ToList();

        Assert.Empty(diagnostics);
    }

    private static INamedTypeSymbol GetSingleFormatterSymbol(Compilation compilation, IReadOnlyList<SyntaxTree> generatedTrees)
    {
        var formatterSymbols = new List<INamedTypeSymbol>();
        foreach (SyntaxTree tree in generatedTrees)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(tree);
            foreach (ClassDeclarationSyntax classDecl in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(classDecl) is INamedTypeSymbol symbol
                    && symbol.AllInterfaces.Any(i => i.Name == "IMessagePackFormatter"))
                {
                    formatterSymbols.Add(symbol);
                }
            }
        }

        return Assert.Single(formatterSymbols);
    }

    private static bool HasExcludeFromCodeCoverageAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == ExcludeFromCodeCoverageAttributeMetadataName);
    }
}
