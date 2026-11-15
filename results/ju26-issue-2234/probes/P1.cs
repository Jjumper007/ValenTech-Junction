// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

public class AyaP1Probe
{
    private const string Preamble = @"
using MessagePack;
";

    private static readonly ImmutableArray<MetadataReference> s_references = BuildReferences();

    [Fact]
    public async Task ProbeTypeCollectorGetObjectInfo()
    {
        await AyaProbe.RecordAsync("valid int-keyed properties and fields", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial class ValidIntKeys
{
    [Key(0)]
    public int Field0;

    [Key(1)]
    public string Prop1 { get; set; }
}
"));

        await AyaProbe.RecordAsync("valid string-keyed properties and fields", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial class ValidStringKeys
{
    [Key(""f0"")]
    public int Field0;

    [Key(""p1"")]
    public string Prop1 { get; set; }
}
"));

        await AyaProbe.RecordAsync("duplicate int keys on properties", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial class DupIntProps
{
    [Key(0)]
    public string Member1 { get; set; }

    [Key(0)]
    public string Member2 { get; set; }
}
"));

        await AyaProbe.RecordAsync("duplicate string keys on properties", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial class DupStringProps
{
    [Key(""a"")]
    public string Member1 { get; set; }

    [Key(""a"")]
    public string Member2 { get; set; }
}
"));

        await AyaProbe.RecordAsync("mixed int then string keys on properties", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial class MixedIntThenStringProps
{
    [Key(0)]
    public string Member1 { get; set; }

    [Key(""Member2"")]
    public string Member2 { get; set; }
}
"));

        await AyaProbe.RecordAsync("mixed string then int keys on properties", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial class MixedStringThenIntProps
{
    [Key(""Member1"")]
    public string Member1 { get; set; }

    [Key(1)]
    public string Member2 { get; set; }
}
"));

        await AyaProbe.RecordAsync("duplicate int keys on fields", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial class DupIntFields
{
    [Key(0)]
    public int Field1;

    [Key(0)]
    public string Field2;
}
"));

        await AyaProbe.RecordAsync("duplicate string keys on fields", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial class DupStringFields
{
    [Key(""k"")]
    public int Field1;

    [Key(""k"")]
    public string Field2;
}
"));

        await AyaProbe.RecordAsync("mixed int then string keys on fields", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial class MixedIntThenStringFields
{
    [Key(0)]
    public int Field1;

    [Key(""Field2"")]
    public string Field2;
}
"));

        await AyaProbe.RecordAsync("mixed string then int keys on fields", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial class MixedStringThenIntFields
{
    [Key(""Field1"")]
    public int Field1;

    [Key(1)]
    public string Field2;
}
"));

        await AyaProbe.RecordAsync("mixed property int key and field string key", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial class MixedPropAndField
{
    [Key(0)]
    public string Prop1 { get; set; }

    [Key(""Field2"")]
    public int Field2;
}
"));

        await AyaProbe.RecordAsync("suppressed source generation with duplicate int keys", () => AnalyzeAsync(Preamble + @"
[MessagePackObject(SuppressSourceGeneration = true)]
public class SuppressedDupInt
{
    [Key(0)]
    public string Member1 { get; set; }

    [Key(0)]
    public string Member2 { get; set; }
}
"));

        await AyaProbe.RecordAsync("suppressed source generation with mixed keys", () => AnalyzeAsync(Preamble + @"
[MessagePackObject(SuppressSourceGeneration = true)]
public class SuppressedMixed
{
    [Key(0)]
    public string Member1 { get; set; }

    [Key(""Member2"")]
    public string Member2 { get; set; }
}
"));

        await AyaProbe.RecordAsync("struct with duplicate int keys", () => AnalyzeAsync(Preamble + @"
[MessagePackObject]
public partial struct StructDupInt
{
    [Key(0)]
    public int A { get; set; }

    [Key(0)]
    public int B;
}
"));
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        _ = typeof(MessagePackObjectAttribute).Assembly;
        _ = typeof(KeyAttribute).Assembly;
        _ = typeof(object).Assembly;

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static async Task<string> AnalyzeAsync(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "ProbeAssembly",
            new[] { tree },
            s_references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new MsgPack00xMessagePackAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        var formatted = diagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Character)
            .ThenBy(d => d.GetMessage(CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .Select(FormatDiagnostic);

        return string.Join("\n", formatted);
    }

    private static string FormatDiagnostic(Diagnostic d)
    {
        var pos = d.Location.GetLineSpan().StartLinePosition;
        var message = d.GetMessage(CultureInfo.InvariantCulture);
        if (string.Equals(d.Id, "AD0001", StringComparison.Ordinal))
        {
            int newlineIndex = message.IndexOfAny(new[] { '\r', '\n' });
            if (newlineIndex >= 0)
            {
                message = message.Substring(0, newlineIndex);
            }
        }

        return $"{d.Id} ({pos.Line + 1},{pos.Character + 1}): {message}";
    }
}
