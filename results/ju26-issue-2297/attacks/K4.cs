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

public class AyaK4Attack
{
    private const string ResolverSource = """
        using MessagePack;

        [MessagePackObject]
        public class SamplePayload
        {
            [Key(0)]
            public int Id { get; set; }
        }

        namespace MyApp.Serialization
        {
            [GeneratedMessagePackResolver]
            internal partial class CustomResolver
            {
            }
        }
        """;

    /// <summary>
    /// ExcludeFromCodeCoverageAttribute has lived in mscorlib / System.Runtime since .NET Framework 4.0
    /// and .NET Standard 2.0. Stripping it under NETSTANDARD2_0 or NETFRAMEWORK leaves generated resolver
    /// types uncovered by the exclusion attribute on those target frameworks.
    /// </summary>
    [Theory]
    [InlineData("NETSTANDARD2_0")]
    [InlineData("NETFRAMEWORK")]
    public async Task ResolverTypesKeepExcludeFromCodeCoverageUnderNetStandard20AndNetFramework(string preprocessorSymbol)
    {
        Compilation outputCompilation = await RunGeneratorAndGetCompilationAsync(ResolverSource, preprocessorSymbol);

        INamedTypeSymbol? resolverType = outputCompilation.GetTypeByMetadataName("MyApp.Serialization.CustomResolver");
        Assert.NotNull(resolverType);

        AssertHasExcludeFromCodeCoverage(resolverType);

        INamedTypeSymbol? formatterCache = resolverType.GetTypeMembers("FormatterCache", 1).SingleOrDefault();
        Assert.NotNull(formatterCache);
        AssertHasExcludeFromCodeCoverage(formatterCache);

        INamedTypeSymbol? getFormatterHelper = resolverType.GetTypeMembers("CustomResolverGetFormatterHelper").SingleOrDefault();
        Assert.NotNull(getFormatterHelper);
        AssertHasExcludeFromCodeCoverage(getFormatterHelper);
    }

    /// <summary>
    /// Nested classes FormatterCache&lt;T&gt; and *GetFormatterHelper are already inside the resolver class.
    /// When the outer resolver class is decorated with [ExcludeFromCodeCoverage], coverage tools already
    /// exclude its nested members; redundant attributes or preprocessor directives inside the class body
    /// should not break compilation or emit warnings under any symbol configuration.
    /// </summary>
    [Theory]
    [InlineData()]
    [InlineData("NETSTANDARD2_0")]
    [InlineData("NETFRAMEWORK")]
    [InlineData("NETSTANDARD2_0", "NETFRAMEWORK")]
    public async Task GeneratedResolverCompilesCleanlyAcrossPreprocessorConfigurations(params string[] preprocessorSymbols)
    {
        Compilation outputCompilation = await RunGeneratorAndGetCompilationAsync(ResolverSource, preprocessorSymbols);

        ImmutableArray<Diagnostic> diagnostics = outputCompilation.GetDiagnostics();
        Assert.Empty(diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
    }

    /// <summary>
    /// When the user-declared partial resolver class already carries [ExcludeFromCodeCoverage],
    /// emitting a duplicate [ExcludeFromCodeCoverage] on the generated partial declaration causes
    /// compiler error CS0579 (Duplicate 'ExcludeFromCodeCoverage' attribute) because AttributeUsage
    /// for ExcludeFromCodeCoverage has AllowMultiple = false.
    /// </summary>
    [Fact]
    public async Task UserResolverAlreadyMarkedWithExcludeFromCodeCoverageDoesNotCauseDuplicateAttributeError()
    {
        const string sourceWithUserAttribute = """
            using System.Diagnostics.CodeAnalysis;
            using MessagePack;

            [MessagePackObject]
            public class Item
            {
                [Key(0)]
                public int Value { get; set; }
            }

            [ExcludeFromCodeCoverage]
            [GeneratedMessagePackResolver]
            public partial class UserExcludedResolver
            {
            }
            """;

        Compilation outputCompilation = await RunGeneratorAndGetCompilationAsync(sourceWithUserAttribute);

        ImmutableArray<Diagnostic> errors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        Assert.Empty(errors);
    }

    /// <summary>
    /// Checks that the XML documentation comment on the generated resolver class stays attached to
    /// the class declaration in the Roslyn syntax/semantic model and is not orphaned or broken by
    /// directives inserted between the doc comment and the partial class declaration.
    /// </summary>
    [Fact]
    public async Task ResolverClassRetainsXmlDocumentationComment()
    {
        Compilation outputCompilation = await RunGeneratorAndGetCompilationAsync(ResolverSource);

        INamedTypeSymbol? resolverType = outputCompilation.GetTypeByMetadataName("MyApp.Serialization.CustomResolver");
        Assert.NotNull(resolverType);

        string? xml = resolverType.GetDocumentationCommentXml();
        Assert.False(string.IsNullOrWhiteSpace(xml), "Expected XML documentation comment on the generated resolver class.");
        Assert.Contains("A MessagePack resolver that uses generated formatters", xml, StringComparison.Ordinal);
    }

    private static void AssertHasExcludeFromCodeCoverage(ISymbol symbol)
    {
        bool hasAttribute = symbol.GetAttributes().Any(
            attr => attr.AttributeClass?.ToDisplayString() == typeof(ExcludeFromCodeCoverageAttribute).FullName);
        Assert.True(hasAttribute, $"Expected '{symbol.ToDisplayString()}' to have [{nameof(ExcludeFromCodeCoverageAttribute)}].");
    }

    private static async Task<Compilation> RunGeneratorAndGetCompilationAsync(string source, params string[] preprocessorSymbols)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: preprocessorSymbols);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaK4AttackTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new MessagePackGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);
        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        return outputCompilation;
    }
}
