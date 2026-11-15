// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaP4Probe
{
    [Fact]
    public async Task SimpleIntKeyObject()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class SimpleIntKey
            {
                [Key(0)]
                public int Id { get; set; }

                [Key(1)]
                public string Name { get; set; }
            }
            """;

        await AyaProbe.RecordAsync(nameof(SimpleIntKeyObject), () => RunGeneratorAsync(nameof(SimpleIntKeyObject), source));
    }

    [Fact]
    public async Task StringKeyObject()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject(keyAsPropertyName: true)]
            public class StringKeySample
            {
                public int Count { get; set; }
                public string Label { get; set; }
            }
            """;

        await AyaProbe.RecordAsync(nameof(StringKeyObject), () => RunGeneratorAsync(nameof(StringKeyObject), source));
    }

    [Fact]
    public async Task EmptyObjectWithoutFormatterResolver()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class EmptySample
            {
                [IgnoreMember]
                public int Ignored { get; set; }
            }
            """;

        await AyaProbe.RecordAsync(nameof(EmptyObjectWithoutFormatterResolver), () => RunGeneratorAsync(nameof(EmptyObjectWithoutFormatterResolver), source));
    }

    [Fact]
    public async Task GenericObjectWithConstraints()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class GenericBox<TKey, TValue>
                where TKey : notnull
                where TValue : class, new()
            {
                [Key(0)]
                public TKey Key { get; set; }

                [Key(1)]
                public TValue Value { get; set; }
            }
            """;

        await AyaProbe.RecordAsync(nameof(GenericObjectWithConstraints), () => RunGeneratorAsync(nameof(GenericObjectWithConstraints), source));
    }

    [Fact]
    public async Task NestedInNamespaceAndOuterTypes()
    {
        const string source = """
            using MessagePack;

            namespace Outer.Inner
            {
                public partial class Container
                {
                    public partial class DeepContainer
                    {
                        [MessagePackObject]
                        public class NestedItem
                        {
                            [Key(0)]
                            public int Value { get; set; }
                        }
                    }
                }
            }
            """;

        await AyaProbe.RecordAsync(nameof(NestedInNamespaceAndOuterTypes), () => RunGeneratorAsync(nameof(NestedInNamespaceAndOuterTypes), source));
    }

    [Fact]
    public async Task StructAndRecordObjects()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public struct PointStruct
            {
                [Key(0)]
                public int X;

                [Key(1)]
                public int Y;
            }

            [MessagePackObject]
            public record PersonRecord(
                [property: Key(0)] string FirstName,
                [property: Key(1)] int Age);
            """;

        await AyaProbe.RecordAsync(nameof(StructAndRecordObjects), () => RunGeneratorAsync(nameof(StructAndRecordObjects), source));
    }

    [Fact]
    public async Task SerializationConstructorAndInitOnlyMembers()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class ImmutableConfig
            {
                [Key(0)]
                public string Endpoint { get; }

                [Key(1)]
                public int TimeoutSeconds { get; init; }

                [SerializationConstructor]
                public ImmutableConfig(string endpoint, int timeoutSeconds)
                {
                    this.Endpoint = endpoint;
                    this.TimeoutSeconds = timeoutSeconds;
                }
            }
            """;

        await AyaProbe.RecordAsync(nameof(SerializationConstructorAndInitOnlyMembers), () => RunGeneratorAsync(nameof(SerializationConstructorAndInitOnlyMembers), source));
    }

    [Fact]
    public async Task CollectionAndArrayMembers()
    {
        const string source = """
            using System.Collections.Generic;
            using MessagePack;

            [MessagePackObject]
            public class CollectionHolder
            {
                [Key(0)]
                public int[] Numbers { get; set; }

                [Key(1)]
                public List<string> Tags { get; set; }

                [Key(2)]
                public Dictionary<string, int> Scores { get; set; }
            }
            """;

        await AyaProbe.RecordAsync(nameof(CollectionAndArrayMembers), () => RunGeneratorAsync(nameof(CollectionAndArrayMembers), source));
    }

    [Fact]
    public async Task ObjectWithGeneratedResolver()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class ResolverItem
            {
                [Key(0)]
                public int Code { get; set; }

                [Key(1)]
                public bool Enabled { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync(nameof(ObjectWithGeneratedResolver), () => RunGeneratorAsync(nameof(ObjectWithGeneratedResolver), source));
    }

    [Fact]
    public async Task NullableEnabledObject()
    {
        const string source = """
            #nullable enable
            using MessagePack;

            [MessagePackObject]
            public class NullableSample
            {
                [Key(0)]
                public string RequiredText { get; set; } = string.Empty;

                [Key(1)]
                public string? OptionalText { get; set; }

                [Key(2)]
                public int? OptionalNumber { get; set; }
            }
            """;

        await AyaProbe.RecordAsync(nameof(NullableEnabledObject), () => RunGeneratorAsync(nameof(NullableEnabledObject), source));
    }

    private static async Task<string> RunGeneratorAsync(string assemblyName, string source)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();

        IEnumerable<string> files = result.GeneratedTrees
            .OrderBy(tree => tree.FilePath.Replace('\\', '/'), StringComparer.Ordinal)
            .Select(tree => $"// Path: {tree.FilePath.Replace('\\', '/')}\n{tree.GetText()}");

        IEnumerable<string> diagnostics = result.Diagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Character)
            .Select(d => $"{d.Id} ({d.Severity}) @{d.Location.GetLineSpan().StartLinePosition}: {d.GetMessage()}");

        return string.Join("\n", files.Concat(diagnostics));
    }
}
