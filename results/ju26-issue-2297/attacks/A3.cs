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
using Microsoft.CodeAnalysis.Testing;
using Xunit;

public class AyaA3Attack
{
    private const string EnumTestSource = """
        using System;
        using MessagePack;

        public enum EmptyEnum
        {
        }

        public enum ByteBackedEnum : byte
        {
            Zero = 0,
            One = 1,
            Max = byte.MaxValue,
        }

        [Flags]
        public enum FlagsEnum
        {
            None = 0,
            Read = 1,
            Write = 2,
        }

        namespace OuterNamespace
        {
            public partial class OuterContainer
            {
                public partial class InnerContainer
                {
                    public enum DeeplyNestedEnum
                    {
                        First = 1,
                        Second = 2,
                    }
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

    [Fact]
    public async Task GeneratedEnumFormatters_CompileWithoutErrors_ForVariousEnumShapes()
    {
        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics, IReadOnlyList<(string Path, string Text)> generated) =
            await RunGeneratorAndCompileAsync(EnumTestSource);

        Assert.DoesNotContain(generatorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(updatedCompilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);

        AssertGeneratedEnumFormatterExists(generated, "EmptyEnum");
        AssertGeneratedEnumFormatterExists(generated, "ByteBackedEnum");
        AssertGeneratedEnumFormatterExists(generated, "FlagsEnum");
        AssertGeneratedEnumFormatterExists(generated, "DeeplyNestedEnum");
    }

    [Fact]
    public async Task GeneratedEnumFormatters_CompileCleanly_WhenNetStandard20IsDefined()
    {
        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics, IReadOnlyList<(string Path, string Text)> generated) =
            await RunGeneratorAndCompileAsync(EnumTestSource, ["NETSTANDARD2_0"]);

        Assert.DoesNotContain(generatorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(updatedCompilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);

        AssertGeneratedEnumFormatterExists(generated, "EmptyEnum");
        AssertGeneratedEnumFormatterExists(generated, "ByteBackedEnum");
        AssertGeneratedEnumFormatterExists(generated, "DeeplyNestedEnum");
    }

    [Fact]
    public async Task GeneratedEnumFormatters_CompileCleanly_WhenNetFrameworkIsDefined()
    {
        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics, IReadOnlyList<(string Path, string Text)> generated) =
            await RunGeneratorAndCompileAsync(EnumTestSource, ["NETFRAMEWORK"]);

        Assert.DoesNotContain(generatorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(updatedCompilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);

        AssertGeneratedEnumFormatterExists(generated, "EmptyEnum");
        AssertGeneratedEnumFormatterExists(generated, "ByteBackedEnum");
        AssertGeneratedEnumFormatterExists(generated, "DeeplyNestedEnum");
    }

    [Fact]
    public async Task GeneratedEnumFormatters_HaveBalancedPreprocessorDirectives_AndValidStructure()
    {
        (_, _, IReadOnlyList<(string Path, string Text)> generated) =
            await RunGeneratorAndCompileAsync(EnumTestSource);

        string[] expectedEnums = ["EmptyEnum", "ByteBackedEnum", "FlagsEnum", "DeeplyNestedEnum"];

        foreach (string enumName in expectedEnums)
        {
            (string path, string text) = AssertGeneratedEnumFormatterExists(generated, enumName);
            string[] lines = text.Replace("\r\n", "\n").Split('\n');

            int depth = 0;
            foreach (string line in lines)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("#if ", StringComparison.Ordinal))
                {
                    depth++;
                }
                else if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
                {
                    depth--;
                    Assert.True(depth >= 0, $"Unmatched #endif in '{path}'.");
                }
            }

            Assert.Equal(0, depth);
            Assert.Contains("internal sealed class ", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GeneratedEnumFormatters_CompileWithoutErrors_WhenSystemIdentifierIsShadowed()
    {
        const string shadowingSource = """
            using MessagePack;

            namespace ShadowingTest
            {
                public partial class Container
                {
                    public class System
                    {
                    }

                    public enum ShadowedEnum
                    {
                        Alpha = 0,
                        Beta = 1,
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

        (Compilation updatedCompilation, ImmutableArray<Diagnostic> generatorDiagnostics, IReadOnlyList<(string Path, string Text)> generated) =
            await RunGeneratorAndCompileAsync(shadowingSource);

        Assert.DoesNotContain(generatorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(updatedCompilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        AssertGeneratedEnumFormatterExists(generated, "ShadowedEnum");
    }

    private static (string Path, string Text) AssertGeneratedEnumFormatterExists(
        IReadOnlyList<(string Path, string Text)> generated,
        string enumName)
    {
        (string Path, string Text) match = generated.FirstOrDefault(g =>
            g.Text.Contains($"IMessagePackFormatter<", StringComparison.Ordinal) &&
            g.Text.Contains(enumName, StringComparison.Ordinal));

        Assert.False(string.IsNullOrEmpty(match.Text), $"Expected generated formatter for enum '{enumName}'.");
        return match;
    }

    private static async Task<(Compilation UpdatedCompilation, ImmutableArray<Diagnostic> GeneratorDiagnostics, IReadOnlyList<(string Path, string Text)> Generated)> RunGeneratorAndCompileAsync(
        string source,
        IEnumerable<string>? preprocessorSymbols = null)
    {
        ImmutableArray<MetadataReference> frameworkReferences =
            await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        var parseOptions = new CSharpParseOptions(
            LanguageVersion.Latest,
            preprocessorSymbols: preprocessorSymbols);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaA3AttackAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new MessagePackGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updatedCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);
        GeneratorDriverRunResult result = driver.GetRunResult();

        List<(string Path, string Text)> generated = result.GeneratedTrees
            .Select(tree => (tree.FilePath, tree.GetText().ToString()))
            .ToList();

        return (updatedCompilation, generatorDiagnostics, generated);
    }
}
