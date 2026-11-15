// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

public class AyaK3Attack
{
    private const string SimpleIntKeyedSource = """
        using MessagePack;

        [MessagePackObject]
        public class SimpleIntKeyedObject
        {
            [Key(0)]
            public int Value { get; set; }
        }
        """;

    private const string NestedPrivateDataTypeSource = """
        using MessagePack;

        public partial class OuterContainer
        {
            [MessagePackObject]
            private class NestedData
            {
                [Key(0)]
                public int Id { get; set; }
            }
        }
        """;

    /// <summary>
    /// ExcludeFromCodeCoverageAttribute has existed since .NET Framework 4.0 and .NET Standard 2.0
    /// (in mscorlib / netstandard.dll). Wrapping it in #if !NETSTANDARD2_0 &amp;&amp; !NETFRAMEWORK
    /// suppresses the attribute when targeting netstandard2.0 or net4x even though the type is present
    /// and valid on those targets.
    /// </summary>
    [Theory]
    [InlineData("NETSTANDARD2_0")]
    [InlineData("NETFRAMEWORK")]
    [InlineData("NET472")]
    public async Task GeneratedIntKeyedFormatter_RetainsExcludeFromCodeCoverage_WhenPreprocessorSymbolDefined(string preprocessorSymbol)
    {
        Compilation updatedCompilation = await RunGeneratorWithSymbolsAsync(SimpleIntKeyedSource, preprocessorSymbol);

        // Verify that the target compilation itself actually contains ExcludeFromCodeCoverageAttribute.
        INamedTypeSymbol? excludeAttrSymbol = updatedCompilation.GetTypeByMetadataName(typeof(ExcludeFromCodeCoverageAttribute).FullName!);
        Assert.NotNull(excludeAttrSymbol);

        INamedTypeSymbol formatterSymbol = FindFormatterSymbol(updatedCompilation, "SimpleIntKeyedObjectFormatter");

        bool hasExcludeFromCodeCoverage = formatterSymbol
            .GetAttributes()
            .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, excludeAttrSymbol));

        Assert.True(
            hasExcludeFromCodeCoverage,
            $"Expected generated formatter '{formatterSymbol.ToDisplayString()}' to carry [ExcludeFromCodeCoverage] when '{preprocessorSymbol}' is defined.");
    }

    /// <summary>
    /// When a [MessagePackObject] type is private inside a partial outer type, the generator nests
    /// the generated formatter inside the partial outer type. Because the outer type is NOT decorated
    /// with [ExcludeFromCodeCoverage], the generated partial declaration of the outer type or all of
    /// its generated nested declarations must not have preprocessor directives that violate indentation
    /// or leave the outer partial class unexcluded if coverage tools inspect the enclosing type,
    /// and must compile cleanly without warnings under WarningLevel/TreatWarningsAsErrors.
    /// </summary>
    [Theory]
    [InlineData("NETSTANDARD2_0")]
    [InlineData("NETFRAMEWORK")]
    [InlineData("NET8_0")]
    public async Task GeneratedNestedFormatter_CompilesCleanlyWithZeroDiagnostics_UnderAllPreprocessorConfigurations(string preprocessorSymbol)
    {
        Compilation updatedCompilation = await RunGeneratorWithSymbolsAsync(NestedPrivateDataTypeSource, preprocessorSymbol);

        ImmutableArray<Diagnostic> diagnostics = updatedCompilation.GetDiagnostics();
        Assert.Empty(diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));

        INamedTypeSymbol? excludeAttrSymbol = updatedCompilation.GetTypeByMetadataName(typeof(ExcludeFromCodeCoverageAttribute).FullName!);
        Assert.NotNull(excludeAttrSymbol);

        INamedTypeSymbol formatterSymbol = FindFormatterSymbol(updatedCompilation, "NestedDataFormatter");
        bool hasExcludeFromCodeCoverage = formatterSymbol
            .GetAttributes()
            .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, excludeAttrSymbol));

        Assert.True(
            hasExcludeFromCodeCoverage,
            $"Expected nested formatter '{formatterSymbol.ToDisplayString()}' to have [ExcludeFromCodeCoverage] when '{preprocessorSymbol}' is defined.");
    }

    /// <summary>
    /// FormatterTemplate emits code inside a TransformBase that supports CurrentIndent (pushed by
    /// EmitNestingTypesAndNamespaces when nesting inside outer partial classes). Using raw this.Write
    /// with hardcoded "#if ...\r\n\t[...]\r\n#endif\r\n\tinternal sealed class" bypasses WriteLine indentation
    /// and emits a leading '#' after indentation on line 33 whenever CurrentIndent is non-empty, or emits
    /// inconsistent indentation on nested types.
    /// </summary>
    [Fact]
    public async Task GeneratedNestedFormatter_DoesNotEmitMisindentedOrInvalidPreprocessorDirectives()
    {
        Compilation updatedCompilation = await RunGeneratorWithSymbolsAsync(NestedPrivateDataTypeSource);

        SyntaxTree formatterTree = updatedCompilation.SyntaxTrees
            .Single(t => t.FilePath.Contains("NestedDataFormatter"));

        string text = formatterTree.GetText().ToString();

        // Ensure there are no lexer/parser diagnostics in the generated syntax tree (e.g., CS1024/CS1025).
        Assert.Empty(formatterTree.GetDiagnostics());

        // Every line inside the generated block that is not a preprocessor directive should follow consistent indentation,
        // and any attribute line immediately preceding `internal sealed class` must have the exact same leading whitespace
        // as `internal sealed class`.
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("internal sealed class ", StringComparison.Ordinal))
            {
                string classIndent = lines[i][..^lines[i].TrimStart().Length];

                // Walk backwards past any #endif to find the attribute line if present.
                int attrLineIndex = i - 1;
                while (attrLineIndex >= 0 && lines[attrLineIndex].TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    attrLineIndex--;
                }

                if (attrLineIndex >= 0 && lines[attrLineIndex].TrimStart().StartsWith("[", StringComparison.Ordinal))
                {
                    string attrIndent = lines[attrLineIndex][..^lines[attrLineIndex].TrimStart().Length];
                    Assert.Equal(classIndent, attrIndent);
                }
            }
        }
    }

    /// <summary>
    /// Verifies that FormatterTemplate does not introduce preprocessor directives (`#if` / `#endif`)
    /// into standard int-keyed formatter output at all, because `ExcludeFromCodeCoverageAttribute` is
    /// unconditionally available in every target framework supported by MessagePack (netstandard2.0, net472+, net6.0+).
    /// </summary>
    [Fact]
    public async Task GeneratedIntKeyedFormatter_DoesNotEmitUnnecessaryTargetFrameworkPreprocessorGuards()
    {
        Compilation updatedCompilation = await RunGeneratorWithSymbolsAsync(SimpleIntKeyedSource);

        SyntaxTree formatterTree = updatedCompilation.SyntaxTrees
            .Single(t => t.FilePath.Contains("SimpleIntKeyedObjectFormatter"));

        SyntaxNode root = await formatterTree.GetRootAsync();
        bool hasIfDirective = root
            .DescendantTrivia(descendIntoTrivia: true)
            .Any(t => t.IsKind(SyntaxKind.IfDirectiveTrivia) || t.IsKind(SyntaxKind.EndIfDirectiveTrivia));

        Assert.False(
            hasIfDirective,
            $"Generated formatter should not contain #if/#endif directives around [ExcludeFromCodeCoverage] because the attribute is available in netstandard2.0 and .NET Framework:\n{formatterTree.GetText()}");
    }

    private static async Task<Compilation> RunGeneratorWithSymbolsAsync(string source, params string[] preprocessorSymbols)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        var parseOptions = new CSharpParseOptions(
            LanguageVersion.Latest,
            preprocessorSymbols: preprocessorSymbols);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaK3AttackTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new MessagePackGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updatedCompilation, out _);
        return updatedCompilation;
    }

    private static INamedTypeSymbol FindFormatterSymbol(Compilation compilation, string formatterTypeName)
    {
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (ClassDeclarationSyntax classDecl in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (classDecl.Identifier.Text == formatterTypeName &&
                    model.GetDeclaredSymbol(classDecl) is INamedTypeSymbol symbol)
                {
                    return symbol;
                }
            }
        }

        throw new InvalidOperationException($"Could not find generated formatter symbol '{formatterTypeName}'.");
    }
}
