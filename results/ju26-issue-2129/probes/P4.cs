using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaP4Probe
{
    private static readonly MetadataReference[] References = BuildReferences();

    private static MetadataReference[] BuildReferences()
    {
        _ = typeof(object).Assembly;
        _ = typeof(Enumerable).Assembly;
        _ = typeof(List<>).Assembly;
        _ = typeof(MessagePack.MessagePackObjectAttribute).Assembly;
        _ = typeof(MessagePack.MessagePackSerializer).Assembly;

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(loc => (MetadataReference)MetadataReference.CreateFromFile(loc))
            .ToArray();
    }

    private static string RunGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "Test.cs");
        var compilation = CSharpCompilation.Create(
            "ProbeAssembly",
            new[] { syntaxTree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePack.SourceGenerator.MessagePackGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        var runResult = driver.GetRunResult();

        var sb = new StringBuilder();

        foreach (var diag in diagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Character)
            .ThenBy(d => d.GetMessage(CultureInfo.InvariantCulture), StringComparer.Ordinal))
        {
            var pos = diag.Location.GetLineSpan().StartLinePosition;
            sb.Append("DIAG ")
              .Append(diag.Id)
              .Append(" (")
              .Append(pos.Line)
              .Append(',')
              .Append(pos.Character)
              .Append("): ")
              .AppendLine(diag.GetMessage(CultureInfo.InvariantCulture));
        }

        foreach (var tree in runResult.GeneratedTrees.OrderBy(t => Path.GetFileName(t.FilePath), StringComparer.Ordinal))
        {
            sb.Append("=== FILE: ").Append(Path.GetFileName(tree.FilePath)).AppendLine(" ===");
            sb.AppendLine(tree.ToString());
        }

        return sb.ToString();
    }

    [Fact]
    public void StringKey_KeyAsPropertyName_SimpleClass()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject(keyAsPropertyName: true)]
            public class SimplePerson
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
            """;

        AyaProbe.Record("StringKey_KeyAsPropertyName_SimpleClass", () => RunGenerator(source));
    }

    [Fact]
    public void StringKey_ExplicitKeys_WithUtf8AndSpecialChars()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class Utf8KeysModel
            {
                [Key("first_name")]
                public string FirstName { get; set; }

                [Key("名前")]
                public string JapaneseName { get; set; }

                [Key("ключ")]
                public int Count { get; set; }

                [Key("a\"b\\c")]
                public bool Flag { get; set; }
            }
            """;

        AyaProbe.Record("StringKey_ExplicitKeys_WithUtf8AndSpecialChars", () => RunGenerator(source));
    }

    [Fact]
    public void StringKey_StructWithFieldsAndProperties()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject(keyAsPropertyName: true)]
            public struct PointStruct
            {
                public double X;
                public double Y { get; set; }
            }
            """;

        AyaProbe.Record("StringKey_StructWithFieldsAndProperties", () => RunGenerator(source));
    }

    [Fact]
    public void StringKey_GenericClassWithConstraints()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject(keyAsPropertyName: true)]
            public class GenericBox<T> where T : class, new()
            {
                public T Value { get; set; }
                public string Label { get; set; }
            }
            """;

        AyaProbe.Record("StringKey_GenericClassWithConstraints", () => RunGenerator(source));
    }

    [Fact]
    public void StringKey_NestedInNamespaceAndOuterClass()
    {
        const string source = """
            using MessagePack;

            namespace Sample.Nested
            {
                public partial class OuterContainer
                {
                    [MessagePackObject(keyAsPropertyName: true)]
                    public class InnerItem
                    {
                        public int Code { get; set; }
                        public string Title { get; set; }
                    }
                }
            }
            """;

        AyaProbe.Record("StringKey_NestedInNamespaceAndOuterClass", () => RunGenerator(source));
    }

    [Fact]
    public void StringKey_EmptyClass()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject(keyAsPropertyName: true)]
            public class EmptyStringKeyType
            {
            }
            """;

        AyaProbe.Record("StringKey_EmptyClass", () => RunGenerator(source));
    }

    [Fact]
    public void StringKey_WithNestedMessagePackObjectAndCollection()
    {
        const string source = """
            using System.Collections.Generic;
            using MessagePack;

            [MessagePackObject(keyAsPropertyName: true)]
            public class ChildItem
            {
                public int Value { get; set; }
            }

            [MessagePackObject(keyAsPropertyName: true)]
            public class ParentContainer
            {
                public ChildItem Child { get; set; }
                public List<int> Numbers { get; set; }
            }
            """;

        AyaProbe.Record("StringKey_WithNestedMessagePackObjectAndCollection", () => RunGenerator(source));
    }

    [Fact]
    public void StringKey_WithIgnoreMemberAndSerializationConstructor()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class CtorModel
            {
                [Key("id")]
                public int Id { get; }

                [Key("tag")]
                public string Tag { get; }

                [IgnoreMember]
                public string Ignored { get; set; }

                [SerializationConstructor]
                public CtorModel(int id, string tag)
                {
                    Id = id;
                    Tag = tag;
                }
            }
            """;

        AyaProbe.Record("StringKey_WithIgnoreMemberAndSerializationConstructor", () => RunGenerator(source));
    }

    [Fact]
    public void StringKey_RecordType()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public record PersonRecord(
                [property: Key("name")] string Name,
                [property: Key("age")] int Age);
            """;

        AyaProbe.Record("StringKey_RecordType", () => RunGenerator(source));
    }

    [Fact]
    public void StringKey_ManyPropertiesWithLongKeys()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject(keyAsPropertyName: true)]
            public class WideModel
            {
                public int AlphaPropertyOne { get; set; }
                public int BetaPropertyTwo { get; set; }
                public int GammaPropertyThree { get; set; }
                public int DeltaPropertyFour { get; set; }
                public int EpsilonPropertyFive { get; set; }
                public int ZetaPropertySix { get; set; }
                public int EtaPropertySeven { get; set; }
                public int ThetaPropertyEight { get; set; }
                public int IotaPropertyNine { get; set; }
                public int KappaPropertyTen { get; set; }
            }
            """;

        AyaProbe.Record("StringKey_ManyPropertiesWithLongKeys", () => RunGenerator(source));
    }
}
