// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaK5Attack
{
    private const string Resolvers = "typeof(MessagePack.Resolvers.NativeGuidResolver), typeof(MessagePack.Resolvers.StandardResolver)";

    [Fact]
    public async Task CompositeResolverAlreadyMarkedExcludeFromCodeCoverageStillCompiles()
    {
        string source = $$"""
            using System.Diagnostics.CodeAnalysis;
            using MessagePack;

            namespace MyApp
            {
                [ExcludeFromCodeCoverage]
                [CompositeResolver({{Resolvers}})]
                partial class MyCompositeResolver
                {
                }
            }
            """;

        (IReadOnlyList<string> generated, IReadOnlyList<Diagnostic> errors) = await RunAsync(source, null);

        Assert.NotEmpty(generated);
        Assert.True(errors.Count == 0, "Unexpected errors: " + string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public async Task CompositeResolverMarkedExcludeFromCodeCoverageInGlobalNamespaceStillCompiles()
    {
        string source = $$"""
            using MessagePack;

            [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            [CompositeResolver({{Resolvers}})]
            partial class MyGlobalCompositeResolver
            {
            }
            """;

        (IReadOnlyList<string> generated, IReadOnlyList<Diagnostic> errors) = await RunAsync(source, null);

        Assert.NotEmpty(generated);
        Assert.True(errors.Count == 0, "Unexpected errors: " + string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public async Task CompositeResolverInNamespaceCompilesAndKeepsShape()
    {
        string source = $$"""
            using MessagePack;

            namespace MyApp.Inner
            {
                [CompositeResolver({{Resolvers}})]
                partial class MyCompositeResolver
                {
                }
            }
            """;

        (IReadOnlyList<string> generated, IReadOnlyList<Diagnostic> errors) = await RunAsync(source, null);

        Assert.True(errors.Count == 0, "Unexpected errors: " + string.Join(Environment.NewLine, errors));
        string text = string.Join("\n", generated).Replace("\r\n", "\n");
        Assert.Contains("partial class MyCompositeResolver : MsgPack::IFormatterResolver", text);
        Assert.Contains("public static readonly MyCompositeResolver Instance = new MyCompositeResolver();", text);
        Assert.Contains("static class FormatterCache<T>", text);
        Assert.Contains("using MsgPack = global::MessagePack;", text);
    }

    [Fact]
    public async Task CompositeResolverNestedInTypeCompilesWithNetStandardSymbol()
    {
        string source = $$"""
            using MessagePack;

            namespace MyApp
            {
                partial class Outer
                {
                    [CompositeResolver({{Resolvers}})]
                    partial class MyNestedResolver
                    {
                    }
                }
            }
            """;

        (IReadOnlyList<string> generated, IReadOnlyList<Diagnostic> errors) = await RunAsync(source, new[] { "NETSTANDARD2_0" });

        Assert.NotEmpty(generated);
        Assert.True(errors.Count == 0, "Unexpected errors: " + string.Join(Environment.NewLine, errors));
        string text = string.Join("\n", generated).Replace("\r\n", "\n");
        Assert.Contains("partial class MyNestedResolver : MsgPack::IFormatterResolver", text);
    }

    private static async Task<(IReadOnlyList<string> Generated, IReadOnlyList<Diagnostic> Errors)> RunAsync(string source, string[]? symbols)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: symbols);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaK5Attack",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[] { new CompositeResolverGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();

        List<string> generated = result.GeneratedTrees.Select(t => t.GetText().ToString()).ToList();
        List<Diagnostic> errors = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        return (generated, errors);
    }
}
