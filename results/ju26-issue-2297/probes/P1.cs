// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaP1Probe
{
    [Fact]
    public async Task TopLevelObjectAndResolver()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class SampleObject
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

        await AyaProbe.RecordAsync("TopLevelObjectAndResolver", async () => await RunGeneratorAsync(source));
    }

    [Fact]
    public async Task NamespaceScopedResolver()
    {
        const string source = """
            using MessagePack;

            namespace MyApp.Serialization
            {
                [MessagePackObject]
                public class Item
                {
                    [Key(0)]
                    public string Name { get; set; }
                }

                [GeneratedMessagePackResolver]
                public partial class AppResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("NamespaceScopedResolver", async () => await RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ResolverInNonGenericPartialClass()
    {
        const string source = """
            using MessagePack;

            public partial class OuterContainer
            {
                [MessagePackObject]
                public class Payload
                {
                    [Key(0)]
                    public int Id { get; set; }
                }

                [GeneratedMessagePackResolver]
                internal partial class NestedResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("ResolverInNonGenericPartialClass", async () => await RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ResolverInGenericPartialClassWithoutConstraints()
    {
        const string source = """
            using MessagePack;

            public partial class GenericOuter<T>
            {
                [GeneratedMessagePackResolver]
                internal partial class NestedResolver
                {
                }
            }

            [MessagePackObject]
            public class SimpleRecord
            {
                [Key(0)]
                public int Code { get; set; }
            }
            """;

        await AyaProbe.RecordAsync("ResolverInGenericPartialClassWithoutConstraints", async () => await RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ResolverInGenericPartialClassWithConstraints()
    {
        const string source = """
            using MessagePack;

            public partial class ConstrainedOuter<T>
                where T : class
            {
                [GeneratedMessagePackResolver]
                internal partial class NestedResolver
                {
                }
            }

            [MessagePackObject]
            public class SimpleRecord
            {
                [Key(0)]
                public int Code { get; set; }
            }
            """;

        await AyaProbe.RecordAsync("ResolverInGenericPartialClassWithConstraints", async () => await RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ResolverInGenericPartialClassWithMultipleConstraints()
    {
        const string source = """
            using MessagePack;

            internal partial class MultiConstrainedOuter<TKey, TValue>
                where TKey : notnull
                where TValue : class, new()
            {
                [GeneratedMessagePackResolver]
                public partial class NestedResolver
                {
                }
            }

            [MessagePackObject]
            public class SimpleRecord
            {
                [Key(0)]
                public string Label { get; set; }
            }
            """;

        await AyaProbe.RecordAsync("ResolverInGenericPartialClassWithMultipleConstraints", async () => await RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ResolverInDeeplyNestedGenericTypes()
    {
        const string source = """
            using MessagePack;

            namespace DeepNs
            {
                public partial class Level1<T> where T : class
                {
                    internal partial class Level2<U> where U : struct
                    {
                        private partial class Level3
                        {
                            [GeneratedMessagePackResolver]
                            internal partial class DeepResolver
                            {
                            }
                        }
                    }
                }

                [MessagePackObject]
                public class DeepItem
                {
                    [Key(0)]
                    public long Timestamp { get; set; }
                }
            }
            """;

        await AyaProbe.RecordAsync("ResolverInDeeplyNestedGenericTypes", async () => await RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ResolverInGenericPartialStructWithConstraints()
    {
        const string source = """
            using MessagePack;

            public partial struct StructOuter<T> where T : unmanaged
            {
                [GeneratedMessagePackResolver]
                public partial class NestedResolver
                {
                }
            }

            [MessagePackObject]
            public class SimpleRecord
            {
                [Key(0)]
                public int Number { get; set; }
            }
            """;

        await AyaProbe.RecordAsync("ResolverInGenericPartialStructWithConstraints", async () => await RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ObjectNestedInGenericPartialClassWithConstraints()
    {
        const string source = """
            using MessagePack;

            public partial class Container<T> where T : class
            {
                [MessagePackObject]
                public class NestedObject
                {
                    [Key(0)]
                    public int Value { get; set; }
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

        await AyaProbe.RecordAsync("ObjectNestedInGenericPartialClassWithConstraints", async () => await RunGeneratorAsync(source));
    }

    [Fact]
    public async Task GenericMessagePackObjectWithConstraintsAndEnumAndUnion()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class GenericObj<T> where T : class, new()
            {
                [Key(0)]
                public T Value { get; set; }
            }

            public enum SampleEnum
            {
                Zero,
                One,
            }

            [Union(0, typeof(UnionImpl))]
            public interface IUnionBase
            {
            }

            [MessagePackObject]
            public class UnionImpl : IUnionBase
            {
                [Key(0)]
                public SampleEnum Kind { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("GenericMessagePackObjectWithConstraintsAndEnumAndUnion", async () => await RunGeneratorAsync(source));
    }

    private static async Task<string> RunGeneratorAsync(string source)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaP1ProbeCompilation",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();

        var parts = new List<string>();

        foreach (Diagnostic diagnostic in result.Diagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Character)
            .ThenBy(d => d.GetMessage(), StringComparer.Ordinal))
        {
            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
            parts.Add($"DIAG:{diagnostic.Id}@{span.StartLinePosition.Line}:{span.StartLinePosition.Character}:{diagnostic.GetMessage()}");
        }

        foreach (SyntaxTree tree in result.GeneratedTrees.OrderBy(t => t.FilePath, StringComparer.Ordinal))
        {
            string normalizedPath = tree.FilePath.Replace('\\', '/');
            parts.Add($"FILE:{normalizedPath}\n{tree.GetText()}");
        }

        return string.Join("\n---\n", parts);
    }
}
