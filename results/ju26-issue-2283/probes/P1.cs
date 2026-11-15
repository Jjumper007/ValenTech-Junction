using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MessagePack;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaP1Probe
{
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

    private static IReadOnlyList<MetadataReference> GetBaseReferences()
    {
        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            typeof(object).Assembly.Location,
            typeof(Enumerable).Assembly.Location,
            typeof(System.Buffers.IBufferWriter<>).Assembly.Location,
            typeof(MessagePackObjectAttribute).Assembly.Location,
            typeof(MessagePackSerializer).Assembly.Location,
        };

        foreach (var asmName in typeof(MessagePackSerializer).Assembly.GetReferencedAssemblies())
        {
            try
            {
                var loaded = Assembly.Load(asmName);
                if (!loaded.IsDynamic && !string.IsNullOrEmpty(loaded.Location))
                {
                    locations.Add(loaded.Location);
                }
            }
            catch
            {
            }
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
            {
                locations.Add(asm.Location);
            }
        }

        return locations
            .Where(File.Exists)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();
    }

    private static string RunGenerator(string source, string? referenceSource = null)
    {
        var references = new List<MetadataReference>(GetBaseReferences());

        if (referenceSource is not null)
        {
            var refTree = CSharpSyntaxTree.ParseText(referenceSource, ParseOptions, path: "Ref.cs");
            var refCompilation = CSharpCompilation.Create(
                "RefAssembly",
                new[] { refTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            using var ms = new MemoryStream();
            var emitResult = refCompilation.Emit(ms);
            if (!emitResult.Success)
            {
                var emitErrors = string.Join(
                    "\n",
                    emitResult.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.ToString()));
                return $"RefEmitFailed:\n{emitErrors}";
            }

            ms.Position = 0;
            references.Add(MetadataReference.CreateFromStream(ms));
        }

        var tree = CSharpSyntaxTree.ParseText(source, ParseOptions, path: "Test.cs");
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var generator = new MessagePackGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            parseOptions: ParseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        var runResult = driver.GetRunResult();

        var sb = new StringBuilder();
        sb.AppendLine("Diagnostics:");
        var formattedDiagnostics = diagnostics
            .Select(d =>
            {
                var span = d.Location.GetLineSpan();
                var loc = d.Location == Location.None
                    ? "none"
                    : $"{span.StartLinePosition.Line}:{span.StartLinePosition.Character}";
                var msg = d.GetMessage(CultureInfo.InvariantCulture);
                return (d.Id, d.Severity, Loc: loc, Msg: msg);
            })
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ThenBy(x => x.Loc, StringComparer.Ordinal)
            .ThenBy(x => x.Msg, StringComparer.Ordinal);

        foreach (var d in formattedDiagnostics)
        {
            sb.AppendLine($"  {d.Id} ({d.Severity}) @{d.Loc}: {d.Msg}");
        }

        var exceptions = runResult.Results
            .Where(r => r.Exception is not null)
            .Select(r => $"{r.Exception!.GetType().FullName}: {r.Exception.Message}")
            .ToArray();

        if (exceptions.Length > 0)
        {
            sb.AppendLine("GeneratorExceptions:");
            foreach (var ex in exceptions)
            {
                sb.AppendLine($"  {ex}");
            }
        }

        sb.AppendLine("GeneratedTrees:");
        foreach (var genTree in runResult.GeneratedTrees.OrderBy(t => t.FilePath.Replace('\\', '/'), StringComparer.Ordinal))
        {
            var normalizedPath = genTree.FilePath.Replace('\\', '/');
            var text = genTree.GetText().ToString().Replace("\r\n", "\n");
            sb.AppendLine($"--- {normalizedPath} ---");
            sb.AppendLine(text);
        }

        return sb.ToString();
    }

    [Fact]
    public void OrdinaryIntKeyedClass()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public class SimpleObj
    {
        [Key(0)]
        public int Id { get; set; }

        [Key(1)]
        public string? Name { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class TestResolver { }
}
""";
        AyaProbe.Record("OrdinaryIntKeyedClass", () => RunGenerator(source));
    }

    [Fact]
    public void StringKeyedAndInitOnlyWithInitializer()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public class InitObj
    {
        [Key("id")]
        public int Id { get; init; } = 42;
    }

    [GeneratedMessagePackResolver]
    partial class TestResolver { }
}
""";
        AyaProbe.Record("StringKeyedAndInitOnlyWithInitializer", () => RunGenerator(source));
    }

    [Fact]
    public void PropertyWithNullKey()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public class NullKeyPropObj
    {
        [Key((string)null!)]
        public int Prop { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class TestResolver { }
}
""";
        AyaProbe.Record("PropertyWithNullKey", () => RunGenerator(source));
    }

    [Fact]
    public void FieldWithNullKey()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public class NullKeyFieldObj
    {
        [Key((string)null!)]
        public int Field;
    }

    [GeneratedMessagePackResolver]
    partial class TestResolver { }
}
""";
        AyaProbe.Record("FieldWithNullKey", () => RunGenerator(source));
    }

    [Fact]
    public void RecordParameterWithNullKey()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public partial record NullKeyRecord([property: Key((string)null!)] int X);

    [GeneratedMessagePackResolver]
    partial class TestResolver { }
}
""";
        AyaProbe.Record("RecordParameterWithNullKey", () => RunGenerator(source));
    }

    [Fact]
    public void MissingKeyOnPublicProperty()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public class MissingKeyObj
    {
        [Key(0)]
        public int Present { get; set; }

        public int Missing { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class TestResolver { }
}
""";
        AyaProbe.Record("MissingKeyOnPublicProperty", () => RunGenerator(source));
    }

    [Fact]
    public void NonPublicMembersWithKeyAndMissingKey()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public partial class NonPublicObj
    {
        [Key(0)]
        private int Secret { get; set; }

        internal int AlsoMissing { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class TestResolver { }
}
""";
        AyaProbe.Record("NonPublicMembersWithKeyAndMissingKey", () => RunGenerator(source));
    }

    [Fact]
    public void DerivedKeyAttributeOnProperty()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    public sealed class MyKeyAttribute : KeyAttribute
    {
        public MyKeyAttribute(int x) : base(x) { }
    }

    [MessagePackObject]
    public class DerivedKeyObj
    {
        [MyKey(0)]
        public int Value { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class TestResolver { }
}
""";
        AyaProbe.Record("DerivedKeyAttributeOnProperty", () => RunGenerator(source));
    }

    [Fact]
    public void MetadataReferenceMissingKey()
    {
        const string refSource = """
using MessagePack;

namespace Lib
{
    [MessagePackObject]
    public class ExtMissingKey
    {
        [Key(0)]
        public int Present { get; set; }

        public int Missing { get; set; }
    }
}
""";

        const string source = """
using MessagePack;
using Lib;

namespace App
{
    [MessagePackObject]
    public class Host
    {
        [Key(0)]
        public ExtMissingKey? Inner { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class TestResolver { }
}
""";
        AyaProbe.Record("MetadataReferenceMissingKey", () => RunGenerator(source, refSource));
    }

    [Fact]
    public void MetadataReferenceNullKey()
    {
        const string refSource = """
using MessagePack;

namespace Lib
{
    [MessagePackObject]
    public class ExtNullKey
    {
        [Key((string)null!)]
        public int Value { get; set; }
    }
}
""";

        const string source = """
using MessagePack;
using Lib;

namespace App
{
    [MessagePackObject]
    public class Host
    {
        [Key(0)]
        public ExtNullKey? Inner { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class TestResolver { }
}
""";
        AyaProbe.Record("MetadataReferenceNullKey", () => RunGenerator(source, refSource));
    }
}
