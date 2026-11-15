using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using MessagePack.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

public class AyaP2Probe
{
    private static readonly Lazy<MetadataReference[]> CachedReferences = new(LoadReferences);

    [Fact]
    public void SupportedDiagnosticsMetadata()
    {
        var analyzer = new MsgPack00xMessagePackAnalyzer();

        AyaProbe.Record("SupportedDiagnostics.Count", () => analyzer.SupportedDiagnostics.Length);

        AyaProbe.Record(
            "SupportedDiagnostics.Ids",
            () => analyzer.SupportedDiagnostics
                .Select(d => d.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());

        AyaProbe.Record(
            "SupportedDiagnostics.Descriptors",
            () => analyzer.SupportedDiagnostics
                .OrderBy(d => d.Id, StringComparer.Ordinal)
                .Select(d => $"{d.Id}|{d.Category}|{d.DefaultSeverity}|{d.IsEnabledByDefault}|{d.Title.ToString(CultureInfo.InvariantCulture)}|{d.HelpLinkUri}")
                .ToArray());

        AyaProbe.Record(
            "SupportedDiagnostics.SeverityCounts",
            () => analyzer.SupportedDiagnostics
                .GroupBy(d => d.DefaultSeverity)
                .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal)
                .Select(g => $"{g.Key}:{g.Count()}")
                .ToArray());

        AyaProbe.Record(
            "SupportedDiagnostics.EnabledByDefaultCount",
            () => analyzer.SupportedDiagnostics.Count(d => d.IsEnabledByDefault));
    }

    [Fact]
    public async Task AnalyzerExecutionCases()
    {
        await AyaProbe.RecordAsync("Analyzer.EmptySource", () => RunAnalyzerAsync(string.Empty));

        await AyaProbe.RecordAsync(
            "Analyzer.ValidMessagePackObjectClass",
            () => RunAnalyzerAsync("""
                using MessagePack;

                [MessagePackObject]
                public class Sample
                {
                    [Key(0)]
                    public int Id { get; set; }
                }
                """));

        await AyaProbe.RecordAsync(
            "Analyzer.MissingKeyOnProperty",
            () => RunAnalyzerAsync("""
                using MessagePack;

                [MessagePackObject]
                public class Sample
                {
                    public int Id { get; set; }
                }
                """));

        await AyaProbe.RecordAsync(
            "Analyzer.MixedIntAndStringKeys",
            () => RunAnalyzerAsync("""
                using MessagePack;

                [MessagePackObject]
                public class Sample
                {
                    [Key(0)]
                    public int Id { get; set; }

                    [Key("name")]
                    public string Name { get; set; }
                }
                """));

        await AyaProbe.RecordAsync(
            "Analyzer.ValidMessagePackObjectStruct",
            () => RunAnalyzerAsync("""
                using MessagePack;

                [MessagePackObject]
                public struct SampleStruct
                {
                    [Key(0)]
                    public int X { get; set; }
                }
                """));

        await AyaProbe.RecordAsync(
            "Analyzer.ContractlessMessagePackObject",
            () => RunAnalyzerAsync("""
                using MessagePack;

                [MessagePackObject(true)]
                public class ContractlessSample
                {
                    public int Id { get; set; }
                    public string Name { get; set; }
                }
                """));

        await AyaProbe.RecordAsync(
            "Analyzer.InvalidFormatterType",
            () => RunAnalyzerAsync("""
                using MessagePack;

                [MessagePackFormatter(typeof(object))]
                public class BadFormatterTarget
                {
                }
                """));
    }

    private static MetadataReference[] LoadReferences()
    {
        _ = typeof(MessagePack.MessagePackObjectAttribute).Assembly;
        _ = typeof(MessagePack.MessagePackSerializer).Assembly;

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToArray();
    }

    private static async Task<string[]> RunAnalyzerAsync(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "ProbeAssembly",
            new[] { syntaxTree },
            CachedReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new MsgPack00xMessagePackAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        return diagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Location.SourceSpan.Start)
            .ThenBy(d => d.Location.SourceSpan.Length)
            .ThenBy(d => d.GetMessage(CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .Select(d => $"{d.Id}@{d.Location.SourceSpan.Start}:{d.Severity}:{d.GetMessage(CultureInfo.InvariantCulture)}")
            .ToArray();
    }
}
