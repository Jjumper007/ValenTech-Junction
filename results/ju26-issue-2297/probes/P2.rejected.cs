// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaP2Probe
{
    [Fact]
    public async Task GlobalNamespaceResolverWithNoTypes()
    {
        const string source = """
            using MessagePack;

            [GeneratedMessagePackResolver]
            partial class GlobalResolver
            {
            }
            """;

        await AyaProbe.RecordAsync("GlobalNamespaceResolverWithNoTypes", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task SingleNamespaceResolverWithNoTypes()
    {
        const string source = """
            using MessagePack;

            namespace MyApp
            {
                [GeneratedMessagePackResolver]
                partial class AppResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("SingleNamespaceResolverWithNoTypes", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task MessagePackNamespaceResolverWithIntKeyObject()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class SimpleItem
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

        await AyaProbe.RecordAsync("MessagePackNamespaceResolverWithIntKeyObject", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task NestedClassResolver()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class Payload
            {
                [Key(0)]
                public string Text { get; set; }
            }

            namespace MyApp.Serialization
            {
                public partial class OuterContainer
                {
                    [GeneratedMessagePackResolver]
                    partial class NestedResolver
                    {
                    }
                }
            }
            """;

        await AyaProbe.RecordAsync("NestedClassResolver", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task DeeplyNestedResolver()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject(keyAsPropertyName: true)]
            public class NamedPayload
            {
                public int Count { get; set; }
            }

            namespace Company.Product.Module
            {
                internal partial class LevelOne
                {
                    internal partial class LevelTwo
                    {
                        [GeneratedMessagePackResolver]
                        private partial class DeepResolver
                        {
                        }
                    }
                }
            }
            """;

        await AyaProbe.RecordAsync("DeeplyNestedResolver", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ResolverWithObjectEnumAndUnion()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class MyObject
            {
                [Key(0)]
                public int Value { get; set; }
            }

            [MessagePackObject(keyAsPropertyName: true)]
            public class MyStringKeyObject
            {
                public string Name { get; set; }
            }

            public enum MyEnum
            {
                A,
                B,
            }

            [Union(0, typeof(MyUnionMember))]
            public interface IMyUnion
            {
            }

            [MessagePackObject]
            public class MyUnionMember : IMyUnion
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

        await AyaProbe.RecordAsync("ResolverWithObjectEnumAndUnion", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ResolverWithGenericObjectAndStruct()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public struct PointData
            {
                [Key(0)]
                public int X { get; set; }

                [Key(1)]
                public int Y { get; set; }
            }

            [MessagePackObject]
            public class Box<T>
            {
                [Key(0)]
                public T Content { get; set; }
            }

            [MessagePackObject]
            public class BoxHolder
            {
                [Key(0)]
                public Box<PointData> Item { get; set; }
            }

            namespace CustomResolvers
            {
                [GeneratedMessagePackResolver]
                internal partial class GenericResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("ResolverWithGenericObjectAndStruct", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task MultipleResolversInSameCompilation()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class FirstType
            {
                [Key(0)]
                public int A { get; set; }
            }

            namespace FirstNs
            {
                [GeneratedMessagePackResolver]
                partial class FirstResolver
                {
                }
            }

            namespace SecondNs
            {
                [GeneratedMessagePackResolver]
                partial class SecondResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("MultipleResolversInSameCompilation", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task NonPartialResolverDiagnosticCase()
    {
        const string source = """
            using MessagePack;

            namespace BrokenNs
            {
                [GeneratedMessagePackResolver]
                class NonPartialResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("NonPartialResolverDiagnosticCase", () => RunGeneratorAsync(source));
    }

    private static async Task<string> RunGeneratorAsync(string source)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaP2ProbeAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();

        var trees = result.GeneratedTrees
            .OrderBy(t => t.FilePath, StringComparer.Ordinal)
            .Select(t => $"=== FILE: {t.FilePath} ===\n{t.GetText()}");

        var diagnostics = result.Diagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Location.SourceSpan.Start)
            .Select(d =>
            {
                FileLinePositionSpan span = d.Location.GetLineSpan();
                return $"DIAG {d.Id} ({d.Severity}) @{span.StartLinePosition.Line}:{span.StartLinePosition.Character}: {d.GetMessage()}";
            });

        return string.Join("\n", diagnostics.Concat(trees));
    }
}
