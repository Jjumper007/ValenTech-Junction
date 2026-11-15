// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaP6Probe
{
    [Fact]
    public void ProbeEmptyCompilation()
    {
        AyaProbe.Record("empty_compilation", () => RunGenerator(string.Empty));
    }

    [Fact]
    public void ProbeMissingMessagePackReference()
    {
        const string source = """
            public class PlainClass
            {
                public int Value { get; set; }
            }
            """;

        AyaProbe.Record("missing_messagepack_reference", () => RunGenerator(source, includeMessagePack: false));
    }

    [Fact]
    public void ProbeSimpleMessagePackObjectWithIntKeys()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class SimpleItem
            {
                [Key(0)]
                public int Id { get; set; }

                [Key(1)]
                public string Name { get; set; } = string.Empty;

                [IgnoreMember]
                public int Ignored { get; set; }
            }
            """;

        AyaProbe.Record("simple_messagepack_object_int_keys", () => RunGenerator(source));
    }

    [Fact]
    public void ProbeKeyAsPropertyNameObject()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject(true)]
            public class NamedItem
            {
                public int Alpha { get; set; }
                public double Beta { get; set; }
            }
            """;

        AyaProbe.Record("key_as_property_name_object", () => RunGenerator(source));
    }

    [Fact]
    public void ProbeStructAndRecordTypes()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public readonly struct PointStruct
            {
                [Key(0)]
                public int X { get; }

                [Key(1)]
                public int Y { get; }

                public PointStruct(int x, int y)
                {
                    X = x;
                    Y = y;
                }
            }

            [MessagePackObject]
            public record PersonRecord(
                [property: Key(0)] string FirstName,
                [property: Key(1)] int Age);
            """;

        AyaProbe.Record("struct_and_record_types", () => RunGenerator(source));
    }

    [Fact]
    public void ProbeGenericMessagePackObject()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class Box<T>
            {
                [Key(0)]
                public T Value { get; set; } = default!;
            }

            [MessagePackObject]
            public class BoxHolder
            {
                [Key(0)]
                public Box<int>? IntBox { get; set; }

                [Key(1)]
                public Box<string>? StringBox { get; set; }
            }
            """;

        AyaProbe.Record("generic_messagepack_object", () => RunGenerator(source));
    }

    [Fact]
    public void ProbeUnionInterface()
    {
        const string source = """
            using MessagePack;

            [Union(0, typeof(ImplA))]
            [Union(1, typeof(ImplB))]
            public interface IShape
            {
                int Area { get; }
            }

            [MessagePackObject]
            public class ImplA : IShape
            {
                [Key(0)]
                public int Area { get; set; }
            }

            [MessagePackObject]
            public class ImplB : IShape
            {
                [Key(0)]
                public int Area { get; set; }

                [Key(1)]
                public string Label { get; set; } = string.Empty;
            }
            """;

        AyaProbe.Record("union_interface", () => RunGenerator(source));
    }

    [Fact]
    public void ProbeEnumAndCollectionMembers()
    {
        const string source = """
            using System.Collections.Generic;
            using MessagePack;

            public enum SampleColor
            {
                Red = 0,
                Green = 1,
                Blue = 2,
            }

            [MessagePackObject]
            public class CollectionHolder
            {
                [Key(0)]
                public SampleColor Color { get; set; }

                [Key(1)]
                public int[] Numbers { get; set; } = new int[0];

                [Key(2)]
                public List<string> Tags { get; set; } = new List<string>();

                [Key(3)]
                public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
            }
            """;

        AyaProbe.Record("enum_and_collection_members", () => RunGenerator(source));
    }

    [Fact]
    public void ProbeNestedTypesInNamespace()
    {
        const string source = """
            using MessagePack;

            namespace SampleApp.Models
            {
                [MessagePackObject]
                public class OuterModel
                {
                    [Key(0)]
                    public InnerModel? Inner { get; set; }

                    [MessagePackObject]
                    public class InnerModel
                    {
                        [Key(0)]
                        public string Code { get; set; } = string.Empty;
                    }
                }
            }
            """;

        AyaProbe.Record("nested_types_in_namespace", () => RunGenerator(source));
    }

    [Fact]
    public void ProbeCustomFormatterInCompilation()
    {
        const string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public readonly struct CustomId
            {
                public CustomId(int raw) => Raw = raw;
                public int Raw { get; }
            }

            public sealed class CustomIdFormatter : IMessagePackFormatter<CustomId>
            {
                public void Serialize(ref MessagePackWriter writer, CustomId value, MessagePackSerializerOptions options)
                {
                    writer.Write(value.Raw);
                }

                public CustomId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
                {
                    return new CustomId(reader.ReadInt32());
                }
            }

            [MessagePackObject]
            public class EntityWithCustomId
            {
                [Key(0)]
                public CustomId Id { get; set; }
            }
            """;

        AyaProbe.Record("custom_formatter_in_compilation", () => RunGenerator(source));
    }

    [Fact]
    public void ProbeMixedKeysDiagnostic()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class MixedKeysModel
            {
                [Key(0)]
                public int Id { get; set; }

                [Key("Name")]
                public string Name { get; set; } = string.Empty;
            }
            """;

        AyaProbe.Record("mixed_keys_diagnostic", () => RunGenerator(source));
    }

    [Fact]
    public void ProbeSerializationConstructor()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class ImmutableModel
            {
                [SerializationConstructor]
                public ImmutableModel(int id, string label)
                {
                    Id = id;
                    Label = label;
                }

                [Key(0)]
                public int Id { get; }

                [Key(1)]
                public string Label { get; }
            }
            """;

        AyaProbe.Record("serialization_constructor", () => RunGenerator(source));
    }

    private static string RunGenerator(string source, bool includeMessagePack = true)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        ImmutableArray<MetadataReference> references = GetMetadataReferences(includeMessagePack);

        var compilation = CSharpCompilation.Create(
            assemblyName: "ProbeAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new MessagePackGenerator().AsSourceGenerator() });
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        GeneratorDriverRunResult runResult = driver.GetRunResult();

        var diagnostics = runResult.Diagnostics
            .Select(FormatDiagnostic)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var generatedSources = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .OrderBy(s => s.HintName, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Diagnostics ({diagnostics.Count}):");
        foreach (string diag in diagnostics)
        {
            sb.AppendLine(diag);
        }

        sb.AppendLine($"GeneratedSources ({generatedSources.Count}):");
        foreach (GeneratedSourceResult generated in generatedSources)
        {
            sb.AppendLine($"--- {generated.HintName} ---");
            sb.AppendLine(generated.SourceText.ToString());
        }

        return sb.ToString();
    }

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{diagnostic.Id} ({diagnostic.Severity}) @ {span.StartLinePosition.Line}:{span.StartLinePosition.Character}-{span.EndLinePosition.Line}:{span.EndLinePosition.Character}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}");
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences(bool includeMessagePack)
    {
        var assemblies = new HashSet<Assembly>
        {
            typeof(object).Assembly,
            typeof(Attribute).Assembly,
            typeof(Enumerable).Assembly,
            typeof(List<>).Assembly,
            typeof(Task).Assembly,
        };

        foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!loaded.IsDynamic && !string.IsNullOrEmpty(loaded.Location))
            {
                string? name = loaded.GetName().Name;
                if (includeMessagePack || (name is not null && !name.StartsWith("MessagePack", StringComparison.OrdinalIgnoreCase)))
                {
                    assemblies.Add(loaded);
                }
            }
        }

        if (includeMessagePack)
        {
            assemblies.Add(typeof(MessagePack.MessagePackObjectAttribute).Assembly);
            assemblies.Add(typeof(MessagePack.Formatters.IMessagePackFormatter<>).Assembly);
        }

        return assemblies
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .GroupBy(a => a.Location, StringComparer.OrdinalIgnoreCase)
            .Select(g => (MetadataReference)MetadataReference.CreateFromFile(g.First().Location))
            .ToImmutableArray();
    }
}
