// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaP2Probe
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> DefaultReferences = new Lazy<IReadOnlyList<MetadataReference>>(CreateDefaultReferences);

    [Fact]
    public void Ordinary_MessagePackObject_WithKeysAndResolver()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject]
public class SimpleItem
{
    [Key(0)]
    public int Id { get; set; }

    [Key(1)]
    public string? Name { get; set; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        AyaProbe.Record("Ordinary_MessagePackObject_WithKeysAndResolver", () => RunGenerator(source));
    }

    [Fact]
    public void StringKeys_And_IgnoreMember()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject(true)]
public class NamedItem
{
    public int Id { get; set; }

    [IgnoreMember]
    public string? Secret { get; set; }

    public double Score { get; set; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        AyaProbe.Record("StringKeys_And_IgnoreMember", () => RunGenerator(source));
    }

    [Fact]
    public void RecordAndRecordStruct_PositionalKeys()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject]
public sealed partial record PersonRecord([property: Key(0)] string Name, [property: Key(1)] int Age);

[MessagePackObject]
public partial record struct PointRecord([property: Key(0)] int X, [property: Key(1)] int Y);

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        AyaProbe.Record("RecordAndRecordStruct_PositionalKeys", () => RunGenerator(source));
    }

    [Fact]
    public void Generics_And_NullableTypes()
    {
        string source = """
using System.Collections.Generic;
using MessagePack;

namespace App;

[MessagePackObject]
public class Box<T>
{
    [Key(0)]
    public T? Value { get; set; }
}

[MessagePackObject]
public class Container
{
    [Key(0)]
    public Box<int>? IntBox { get; set; }

    [Key(1)]
    public List<string>? Items { get; set; }

    [Key(2)]
    public int? OptionalCount { get; set; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        AyaProbe.Record("Generics_And_NullableTypes", () => RunGenerator(source));
    }

    [Fact]
    public void Union_InterfaceAndImplementations()
    {
        string source = """
using MessagePack;

namespace App;

[Union(0, typeof(Dog))]
[Union(1, typeof(Cat))]
public interface IAnimal
{
    string Name { get; }
}

[MessagePackObject]
public class Dog : IAnimal
{
    [Key(0)]
    public string Name { get; set; } = "";
}

[MessagePackObject]
public class Cat : IAnimal
{
    [Key(0)]
    public string Name { get; set; } = "";
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        AyaProbe.Record("Union_InterfaceAndImplementations", () => RunGenerator(source));
    }

    [Fact]
    public void InaccessibleNestedType_ReportsDiagnostic()
    {
        string source = """
using MessagePack;

namespace App;

public class Outer
{
    [MessagePackObject]
    private class HiddenItem
    {
        [Key(0)]
        public int Value { get; set; }
    }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        AyaProbe.Record("InaccessibleNestedType_ReportsDiagnostic", () => RunGenerator(source));
    }

    [Fact]
    public void UnannotatedProperty_ReportsDiagnostic()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject]
public class IncompleteItem
{
    [Key(0)]
    public int Id { get; set; }

    public string? MissingKey { get; set; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        AyaProbe.Record("UnannotatedProperty_ReportsDiagnostic", () => RunGenerator(source));
    }

    [Fact]
    public void DuplicateKeys_ReportsDiagnostic()
    {
        string source = """
using MessagePack;

namespace App;

[MessagePackObject]
public class DuplicateKeyItem
{
    [Key(0)]
    public int First { get; set; }

    [Key(0)]
    public int Second { get; set; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        AyaProbe.Record("DuplicateKeys_ReportsDiagnostic", () => RunGenerator(source));
    }

    [Fact]
    public void MetadataReference_ValidClosedGenericRecordStruct()
    {
        string libSource = """
using MessagePack;

namespace Lib;

[MessagePackObject]
public partial record struct Item<T>([property: Key(0)] string Key);
""";

        string appSource = """
using MessagePack;
using Lib;

namespace App;

[MessagePackObject]
public sealed partial record Cmd([property: Key(0)] string Scope)
{
    [Key(1)]
    public Item<int> Member { get; init; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        AyaProbe.Record("MetadataReference_ValidClosedGenericRecordStruct", () => RunGeneratorWithReference(libSource, appSource));
    }

    [Fact]
    public void MetadataReference_TypeWithDiagnosticCondition()
    {
        string libSource = """
using MessagePack;

namespace Lib;

[MessagePackObject]
public class ExternalItem
{
    [Key(0)]
    public int Id { get; set; }

    public string? UnannotatedInLib { get; set; }
}
""";

        string appSource = """
using MessagePack;
using Lib;

namespace App;

[MessagePackObject]
public class LocalHolder
{
    [Key(0)]
    public ExternalItem? Item { get; set; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        AyaProbe.Record("MetadataReference_TypeWithDiagnosticCondition", () => RunGeneratorWithReference(libSource, appSource));
    }

    private static string RunGenerator(string appSource)
    {
        return RunGeneratorCore(null, appSource);
    }

    private static string RunGeneratorWithReference(string libSource, string appSource)
    {
        return RunGeneratorCore(libSource, appSource);
    }

    private static string RunGeneratorCore(string? libSource, string appSource)
    {
        CSharpParseOptions parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var references = new List<MetadataReference>(DefaultReferences.Value);

        if (libSource is not null)
        {
            CSharpCompilation libCompilation = CSharpCompilation.Create(
                "Lib",
                new[] { CSharpSyntaxTree.ParseText(libSource, parseOptions, path: "Lib.cs") },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var emitResult = libCompilation.Emit(ms);
            if (!emitResult.Success)
            {
                string libErrors = string.Join(
                    "; ",
                    emitResult.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.Id + ":" + d.GetMessage(CultureInfo.InvariantCulture)));
                return "LIB_EMIT_FAILED: " + libErrors;
            }

            ms.Position = 0;
            references.Add(MetadataReference.CreateFromStream(ms));
        }

        CSharpCompilation appCompilation = CSharpCompilation.Create(
            "App",
            new[] { CSharpSyntaxTree.ParseText(appSource, parseOptions, path: "App.cs") },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        object generator = new MessagePack.SourceGenerator.MessagePackGenerator();
        GeneratorDriver driver = generator switch
        {
            IIncrementalGenerator inc => CSharpGeneratorDriver.Create(new[] { inc.AsSourceGenerator() }, parseOptions: parseOptions),
            ISourceGenerator src => CSharpGeneratorDriver.Create(new[] { src }, parseOptions: parseOptions),
            _ => throw new InvalidOperationException("Unsupported generator type."),
        };

        driver = driver.RunGeneratorsAndUpdateCompilation(appCompilation, out _, out _);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        var sb = new StringBuilder();
        sb.AppendLine("DIAGNOSTICS:");
        foreach (Diagnostic d in runResult.Diagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Severity)
            .ThenBy(d => d.Location.SourceSpan.Start)
            .ThenBy(d => d.Location.SourceSpan.Length)
            .ThenBy(d => d.GetMessage(CultureInfo.InvariantCulture), StringComparer.Ordinal))
        {
            string loc = d.Location.IsInSource
                ? $"source:{d.Location.SourceSpan.Start}..{d.Location.SourceSpan.End}"
                : d.Location.Kind.ToString();
            sb.AppendLine($"  [{d.Severity}] {d.Id} ({loc}): {d.GetMessage(CultureInfo.InvariantCulture)}");
        }

        sb.AppendLine("GENERATED:");
        foreach (SyntaxTree tree in runResult.GeneratedTrees.OrderBy(t => t.FilePath.Replace('\\', '/'), StringComparer.Ordinal))
        {
            string normalizedPath = tree.FilePath.Replace('\\', '/');
            string normalizedText = tree.ToString().Replace("\r\n", "\n");
            sb.AppendLine($"--- {normalizedPath} ---");
            sb.AppendLine(normalizedText);
        }

        return sb.ToString();
    }

    private static IReadOnlyList<MetadataReference> CreateDefaultReferences()
    {
        _ = typeof(MessagePack.MessagePackObjectAttribute).Assembly;
        _ = typeof(MessagePack.MessagePackSerializer).Assembly;
        _ = typeof(List<>).Assembly;
        _ = typeof(Enumerable).Assembly;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var refs = new List<MetadataReference>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location) && seen.Add(asm.Location))
            {
                refs.Add(MetadataReference.CreateFromFile(asm.Location));
            }
        }

        return refs;
    }
}
