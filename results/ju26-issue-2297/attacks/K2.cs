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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

public class AyaK2Attack
{
    private const string EnumTestSource = """
        using MessagePack;

        namespace SampleNamespace
        {
            public enum SampleEnum : byte
            {
                None = 0,
                First = 1,
                Second = 2,
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)]
                public SampleEnum EnumValue { get; set; }
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

    [Fact]
    public async Task EnumFormatter_WhenCompiledWithNetStandard20Symbol_StillHasExcludeFromCodeCoverageAttribute()
    {
        (Compilation outputCompilation, GeneratorDriverRunResult runResult) = await RunGeneratorWithSymbolsAsync(
            EnumTestSource,
            "NETSTANDARD2_0");

        SyntaxTree enumFormatterTree = Assert.Single(
            runResult.GeneratedTrees,
            t => t.FilePath.Contains("SampleEnumFormatter", StringComparison.Ordinal));

        SemanticModel semanticModel = outputCompilation.GetSemanticModel(enumFormatterTree);
        ClassDeclarationSyntax formatterClass = enumFormatterTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "SampleEnumFormatter");

        INamedTypeSymbol? symbol = semanticModel.GetDeclaredSymbol(formatterClass);
        Assert.NotNull(symbol);
        Assert.Contains(
            symbol.GetAttributes(),
            attr => attr.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute");
    }

    [Fact]
    public async Task EnumFormatter_WhenCompiledWithNetFrameworkSymbol_StillHasExcludeFromCodeCoverageAttribute()
    {
        (Compilation outputCompilation, GeneratorDriverRunResult runResult) = await RunGeneratorWithSymbolsAsync(
            EnumTestSource,
            "NETFRAMEWORK");

        SyntaxTree enumFormatterTree = Assert.Single(
            runResult.GeneratedTrees,
            t => t.FilePath.Contains("SampleEnumFormatter", StringComparison.Ordinal));

        SemanticModel semanticModel = outputCompilation.GetSemanticModel(enumFormatterTree);
        ClassDeclarationSyntax formatterClass = enumFormatterTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "SampleEnumFormatter");

        INamedTypeSymbol? symbol = semanticModel.GetDeclaredSymbol(formatterClass);
        Assert.NotNull(symbol);
        Assert.Contains(
            symbol.GetAttributes(),
            attr => attr.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute");
    }

    [Fact]
    public async Task EnumFormatter_GeneratedSyntaxTree_DoesNotContainConditionalPreprocessorDirectives()
    {
        (_, GeneratorDriverRunResult runResult) = await RunGeneratorWithSymbolsAsync(EnumTestSource);

        SyntaxTree enumFormatterTree = Assert.Single(
            runResult.GeneratedTrees,
            t => t.FilePath.Contains("SampleEnumFormatter", StringComparison.Ordinal));

        SyntaxNode root = await enumFormatterTree.GetRootAsync();
        Assert.False(
            root.ContainsDirectives,
            $"Expected generated enum formatter to not contain preprocessor directives, but got:{Environment.NewLine}{enumFormatterTree.GetText()}");
    }

    [Fact]
    public async Task EnumFormatter_ClassDeclaration_ImmediatelyFollowsEnclosingTypeOpeningBrace()
    {
        (_, GeneratorDriverRunResult runResult) = await RunGeneratorWithSymbolsAsync(EnumTestSource);

        SyntaxTree enumFormatterTree = Assert.Single(
            runResult.GeneratedTrees,
            t => t.FilePath.Contains("SampleEnumFormatter", StringComparison.Ordinal));

        string text = enumFormatterTree.GetText().ToString();
        Assert.Contains("{\r\n\tinternal sealed class SampleEnumFormatter : MsgPack::Formatters.IMessagePackFormatter<global::SampleNamespace.SampleEnum>", text);
    }

    [Fact]
    public async Task EnumFormatter_NestedAndEmptyEnums_ProduceZeroCompilationDiagnostics()
    {
        const string source = """
            using MessagePack;

            public enum EmptyEnum
            {
            }

            [MessagePackObject]
            public class OuterObject
            {
                public enum NestedEnum
                {
                    OnlyMember = 42,
                }

                [Key(0)]
                public EmptyEnum Empty { get; set; }

                [Key(1)]
                public NestedEnum Nested { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        (Compilation outputCompilation, GeneratorDriverRunResult runResult) = await RunGeneratorWithSymbolsAsync(source);

        Assert.Empty(runResult.Diagnostics);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning));

        SyntaxTree nestedEnumTree = Assert.Single(
            runResult.GeneratedTrees,
            t => t.FilePath.Contains("NestedEnumFormatter", StringComparison.Ordinal));

        ClassDeclarationSyntax formatterClass = nestedEnumTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText.Contains("NestedEnumFormatter", StringComparison.Ordinal));

        Assert.Empty(formatterClass.AttributeLists);
    }

    private static async Task<(Compilation OutputCompilation, GeneratorDriverRunResult RunResult)> RunGeneratorWithSymbolsAsync(
        string source,
        params string[] preprocessorSymbols)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(
            LanguageNames.CSharp,
            CancellationToken.None);

        var parseOptions = new CSharpParseOptions(
            LanguageVersion.Latest,
            preprocessorSymbols: preprocessorSymbols);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "EnumTemplateAttackTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new MessagePackGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out _);
        return (outputCompilation, driver.GetRunResult());
    }
}
