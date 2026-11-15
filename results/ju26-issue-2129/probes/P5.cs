using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using MessagePack;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaP5Probe
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new Lazy<ImmutableArray<MetadataReference>>(() =>
    {
        _ = typeof(MessagePackObjectAttribute).Assembly;
        _ = typeof(MessagePackSerializer).Assembly;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location) && seen.Add(assembly.Location))
            {
                list.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        return list.ToImmutable();
    });

    private static string RunGenerator(string source)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "ProbeAssembly",
            new[] { CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default) },
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> diagnostics);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        var sb = new StringBuilder();
        sb.AppendLine("=== Diagnostics ===");
        foreach (Diagnostic d in diagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Character)
            .ThenBy(d => d.GetMessage(CultureInfo.InvariantCulture), StringComparer.Ordinal))
        {
            FileLinePositionSpan span = d.Location.GetLineSpan();
            sb.Append(d.Id)
              .Append(" (")
              .Append(span.StartLinePosition.Line)
              .Append(',')
              .Append(span.StartLinePosition.Character)
              .Append("): ")
              .AppendLine(d.GetMessage(CultureInfo.InvariantCulture));
        }

        sb.AppendLine("=== Generated ===");
        foreach (GeneratedSourceResult src in runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .OrderBy(s => s.HintName, StringComparer.Ordinal))
        {
            sb.Append("--- ").AppendLine(src.HintName);
            sb.AppendLine(src.SourceText.ToString());
        }

        return sb.ToString();
    }

    [Fact]
    public void EmptySource()
    {
        AyaProbe.Record("EmptySource", () => RunGenerator(string.Empty));
    }

    [Fact]
    public void OrdinaryMessagePackObject()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class SimpleItem
            {
                [Key(0)]
                public int Id { get; set; }

                [Key(1)]
                public string Name { get; set; }
            }
            """;

        AyaProbe.Record("OrdinaryMessagePackObject", () => RunGenerator(source));
    }

    [Fact]
    public void CustomFormatterInCompilation()
    {
        const string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class CustomValue
            {
                public int Raw { get; set; }
            }

            public class CustomValueFormatter : IMessagePackFormatter<CustomValue>
            {
                public void Serialize(ref MessagePackWriter writer, CustomValue value, MessagePackSerializerOptions options)
                {
                    if (value == null)
                    {
                        writer.WriteNil();
                        return;
                    }

                    writer.Write(value.Raw);
                }

                public CustomValue Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
                {
                    if (reader.TryReadNil())
                    {
                        return null;
                    }

                    return new CustomValue { Raw = reader.ReadInt32() };
                }
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)]
                public CustomValue Value { get; set; }
            }
            """;

        AyaProbe.Record("CustomFormatterInCompilation", () => RunGenerator(source));
    }

    [Fact]
    public void GenericCustomFormatterInCompilation()
    {
        const string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class Box<T>
            {
                public T Value { get; set; }
            }

            public class BoxFormatter<T> : IMessagePackFormatter<Box<T>>
            {
                public void Serialize(ref MessagePackWriter writer, Box<T> value, MessagePackSerializerOptions options)
                {
                    if (value == null)
                    {
                        writer.WriteNil();
                        return;
                    }

                    options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.Value, options);
                }

                public Box<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
                {
                    if (reader.TryReadNil())
                    {
                        return null;
                    }

                    return new Box<T>
                    {
                        Value = options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options),
                    };
                }
            }

            [MessagePackObject]
            public class BoxHolder
            {
                [Key(0)]
                public Box<int> IntBox { get; set; }
            }
            """;

        AyaProbe.Record("GenericCustomFormatterInCompilation", () => RunGenerator(source));
    }

    [Fact]
    public void InternalAndNestedCustomFormatters()
    {
        const string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class TargetA { }
            public class TargetB { }

            internal class InternalFormatter : IMessagePackFormatter<TargetA>
            {
                public void Serialize(ref MessagePackWriter writer, TargetA value, MessagePackSerializerOptions options) => writer.WriteNil();
                public TargetA Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => null;
            }

            public class Outer
            {
                public class NestedFormatter : IMessagePackFormatter<TargetB>
                {
                    public void Serialize(ref MessagePackWriter writer, TargetB value, MessagePackSerializerOptions options) => writer.WriteNil();
                    public TargetB Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => null;
                }
            }

            [MessagePackObject]
            public class Container
            {
                [Key(0)]
                public TargetA A { get; set; }

                [Key(1)]
                public TargetB B { get; set; }
            }
            """;

        AyaProbe.Record("InternalAndNestedCustomFormatters", () => RunGenerator(source));
    }

    [Fact]
    public void AbstractFormatterClass()
    {
        const string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class AbstractTarget { }

            public abstract class AbstractFormatter : IMessagePackFormatter<AbstractTarget>
            {
                public abstract void Serialize(ref MessagePackWriter writer, AbstractTarget value, MessagePackSerializerOptions options);
                public abstract AbstractTarget Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options);
            }

            [MessagePackObject]
            public class SimpleData
            {
                [Key(0)]
                public int Count { get; set; }
            }
            """;

        AyaProbe.Record("AbstractFormatterClass", () => RunGenerator(source));
    }

    [Fact]
    public void ClassWithBaseListNotImplementingFormatter()
    {
        const string source = """
            using System;
            using MessagePack;

            public class BaseItem { }

            public class DerivedItem : BaseItem
            {
                public int Extra { get; set; }
            }

            [MessagePackObject]
            public class Wrapper
            {
                [Key(0)]
                public int Code { get; set; }
            }
            """;

        AyaProbe.Record("ClassWithBaseListNotImplementingFormatter", () => RunGenerator(source));
    }

    [Fact]
    public void StructFormatterIgnoredByClassPredicate()
    {
        const string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public struct StructTarget
            {
                public int X;
            }

            public struct StructFormatter : IMessagePackFormatter<StructTarget>
            {
                public void Serialize(ref MessagePackWriter writer, StructTarget value, MessagePackSerializerOptions options) => writer.Write(value.X);
                public StructTarget Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => new StructTarget { X = reader.ReadInt32() };
            }

            [MessagePackObject]
            public class StructOwner
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        AyaProbe.Record("StructFormatterIgnoredByClassPredicate", () => RunGenerator(source));
    }

    [Fact]
    public void AssemblyKnownFormatterAttribute()
    {
        const string source = """
            using MessagePack;
            using MessagePack.Formatters;

            [assembly: MessagePackKnownFormatter(typeof(KnownTargetFormatter))]

            public class KnownTarget
            {
                public string Tag { get; set; }
            }

            public class KnownTargetFormatter : IMessagePackFormatter<KnownTarget>
            {
                public void Serialize(ref MessagePackWriter writer, KnownTarget value, MessagePackSerializerOptions options) => writer.Write(value?.Tag);
                public KnownTarget Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => new KnownTarget { Tag = reader.ReadString() };
            }

            [MessagePackObject]
            public class KnownHolder
            {
                [Key(0)]
                public KnownTarget Target { get; set; }
            }
            """;

        AyaProbe.Record("AssemblyKnownFormatterAttribute", () => RunGenerator(source));
    }

    [Fact]
    public void AssemblyAssumedFormattableAttribute()
    {
        const string source = """
            using MessagePack;

            [assembly: MessagePackAssumedFormattable(typeof(ExternalLikeType))]

            public class ExternalLikeType
            {
                public int Value { get; set; }
            }

            [MessagePackObject]
            public class Consumer
            {
                [Key(0)]
                public ExternalLikeType Item { get; set; }
            }
            """;

        AyaProbe.Record("AssemblyAssumedFormattableAttribute", () => RunGenerator(source));
    }
}
