// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

public class AyaS1Probe
{
    [Fact]
    public async Task RunAll()
    {
        var baseReferences = (await ReferencesHelper.DefaultTargetFrameworkReferences
            .ResolveAsync(LanguageNames.CSharp, CancellationToken.None))
            .AddRange(ReferencesHelper.GetReferences(ReferencesSet.MessagePack));

        var additionalTexts = LoadAdditionalFiles();

        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithWarningLevel(99)
            .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                ["CS1591"] = ReportDiagnostic.Suppress,
                ["CS0169"] = ReportDiagnostic.Suppress,
                ["CS0414"] = ReportDiagnostic.Suppress,
            });

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new MessagePack.SourceGenerator.Analyzers.MsgPack001SpecifyOptionsAnalyzer(),
            new MessagePack.SourceGenerator.Analyzers.MsgPack002UseConstantOptionsAnalyzer(),
            new MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer());

        var generators = ImmutableArray.Create(
            ToSourceGenerator(new MessagePack.SourceGenerator.CompositeResolverGenerator()),
            ToSourceGenerator(new MessagePack.SourceGenerator.MessagePackGenerator()));

        foreach (var input in AyaProbe.Inputs())
        {
            await AyaProbe.RecordAsync(
                input.Key,
                async () => await DescribeAsync(
                    input.Value,
                    baseReferences,
                    additionalTexts,
                    compilationOptions,
                    analyzers,
                    generators));
        }
    }

    private static async Task<string> DescribeAsync(
        string text,
        ImmutableArray<MetadataReference> baseReferences,
        ImmutableArray<AdditionalText> additionalTexts,
        CSharpCompilationOptions compilationOptions,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        ImmutableArray<ISourceGenerator> generators)
    {
        var normalizedLines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
        if (normalizedLines.Length > 0 && normalizedLines[0].StartsWith("// AYA-SYMBOLS:", StringComparison.Ordinal))
        {
            var rawSymbols = normalizedLines[0].Substring("// AYA-SYMBOLS:".Length);
            var symbols = rawSymbols.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (symbols.Length > 0)
            {
                parseOptions = parseOptions.WithPreprocessorSymbols(symbols);
            }
        }

        MemoryStream? splitStream = null;
        try
        {
            var references = baseReferences;
            string inputText;

            int splitIndex = Array.FindIndex(
                normalizedLines,
                static line => string.Equals(line.Trim(), "// AYA-SPLIT", StringComparison.Ordinal));

            if (splitIndex >= 0)
            {
                string libText = string.Join("\n", normalizedLines, 0, splitIndex);
                inputText = string.Join("\n", normalizedLines, splitIndex + 1, normalizedLines.Length - splitIndex - 1);

                var libTree = CSharpSyntaxTree.ParseText(libText, parseOptions, path: "Lib.cs");
                var libCompilation = CSharpCompilation.Create(
                    "AyaSplitLib",
                    new[] { libTree },
                    baseReferences,
                    compilationOptions);

                splitStream = new MemoryStream();
                var emitResult = libCompilation.Emit(splitStream);
                if (!emitResult.Success)
                {
                    return "SPLIT-FAILED";
                }

                splitStream.Position = 0;
                references = baseReferences.Add(MetadataReference.CreateFromStream(splitStream));
            }
            else
            {
                inputText = text;
            }

            var inputTree = CSharpSyntaxTree.ParseText(inputText, parseOptions, path: "Input.cs");
            var inputCompilation = CSharpCompilation.Create(
                "AyaProbeAssembly",
                new[] { inputTree },
                references,
                compilationOptions);

            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: generators,
                additionalTexts: additionalTexts,
                parseOptions: parseOptions);

            driver = driver.RunGeneratorsAndUpdateCompilation(
                inputCompilation,
                out var outputCompilation,
                out var generatorDiagnostics,
                CancellationToken.None);

            var runResult = driver.GetRunResult();

            var analyzerOptions = new Microsoft.CodeAnalysis.Diagnostics.AnalyzerOptions(additionalTexts);
            var analyzerDiagnostics = await inputCompilation
                .WithAnalyzers(analyzers, analyzerOptions)
                .GetAnalyzerDiagnosticsAsync(CancellationToken.None);

            var diagLines = new List<string>();
            foreach (var diag in generatorDiagnostics.Concat(analyzerDiagnostics))
            {
                diagLines.Add(FormatCodeUnderTestDiagnostic(diag));
            }

            diagLines.Sort(StringComparer.Ordinal);

            var generatedSources = runResult.Results
                .SelectMany(static r => r.GeneratedSources)
                .OrderBy(static s => s.HintName, StringComparer.Ordinal)
                .ThenBy(static s => s.SourceText.ToString(), StringComparer.Ordinal)
                .ToList();

            var generatedTrees = new HashSet<SyntaxTree>(runResult.GeneratedTrees);

            var compilerErrorLines = new List<string>();
            foreach (var diag in outputCompilation.GetDiagnostics(CancellationToken.None))
            {
                if (diag.Severity != DiagnosticSeverity.Error)
                {
                    continue;
                }

                bool inGenerated = diag.Location.IsInSource
                    && diag.Location.SourceTree is not null
                    && generatedTrees.Contains(diag.Location.SourceTree);

                string prefix = inGenerated ? "ERROR-GEN" : "ERROR";
                string msg = SingleLine(diag.GetMessage(CultureInfo.InvariantCulture));
                compilerErrorLines.Add($"{prefix} {diag.Id}: {msg}");
            }

            compilerErrorLines.Sort(StringComparer.Ordinal);

            var userTypeLines = DescribeUserTypes(outputCompilation, inputTree);

            var sb = new StringBuilder();
            foreach (var line in diagLines)
            {
                sb.AppendLine(line);
            }

            foreach (var source in generatedSources)
            {
                sb.Append("OUT ").AppendLine(source.HintName);
                sb.AppendLine(source.SourceText.ToString());
            }

            foreach (var line in compilerErrorLines)
            {
                sb.AppendLine(line);
            }

            foreach (var line in userTypeLines)
            {
                sb.AppendLine(line);
            }

            return sb.ToString();
        }
        finally
        {
            splitStream?.Dispose();
        }
    }

    private static List<string> DescribeUserTypes(Compilation finalCompilation, SyntaxTree inputTree)
    {
        var semanticModel = finalCompilation.GetSemanticModel(inputTree);
        var root = inputTree.GetRoot(CancellationToken.None);
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var lines = new List<string>();

        foreach (var node in root.DescendantNodes())
        {
            if (node is not BaseTypeDeclarationSyntax and not DelegateDeclarationSyntax)
            {
                continue;
            }

            if (semanticModel.GetDeclaredSymbol(node, CancellationToken.None) is not INamedTypeSymbol typeSymbol)
            {
                continue;
            }

            if (!seen.Add(typeSymbol))
            {
                continue;
            }

            var attributes = typeSymbol.GetAttributes()
                .Select(static a => a.AttributeClass?.ToDisplayString() ?? string.Empty)
                .OrderBy(static n => n, StringComparer.Ordinal);

            var baseList = new List<string>();
            if (typeSymbol.BaseType is not null)
            {
                baseList.Add(typeSymbol.BaseType.ToDisplayString());
            }

            foreach (var iface in typeSymbol.Interfaces)
            {
                baseList.Add(iface.ToDisplayString());
            }

            baseList.Sort(StringComparer.Ordinal);

            int memberCount = typeSymbol.GetMembers().Length;
            string typeName = typeSymbol.ToDisplayString();

            lines.Add(
                $"USER {typeName}: attributes [{string.Join(", ", attributes)}]; bases [{string.Join(", ", baseList)}]; members {memberCount}");
        }

        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    private static string FormatCodeUnderTestDiagnostic(Diagnostic diag)
    {
        string rawMessage = diag.GetMessage(CultureInfo.InvariantCulture);
        if (diag.Id is "CS8785" or "AD0001")
        {
            return $"CRASH {diag.Id}: {FirstLine(rawMessage)}";
        }

        return $"DIAG {diag.Id} {diag.Severity}: {SingleLine(rawMessage)}";
    }

    private static string FirstLine(string text)
    {
        int cr = text.IndexOf('\r');
        int lf = text.IndexOf('\n');
        int end = (cr, lf) switch
        {
            (-1, -1) => text.Length,
            (-1, _) => lf,
            (_, -1) => cr,
            _ => Math.Min(cr, lf),
        };

        return text.Substring(0, end);
    }

    private static string SingleLine(string text)
    {
        return text.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal);
    }

    private static ISourceGenerator ToSourceGenerator(object generator) => generator switch
    {
        ISourceGenerator sg => sg,
        IIncrementalGenerator ig => ig.AsSourceGenerator(),
        _ => throw new InvalidOperationException($"Unsupported generator type: {generator.GetType()}"),
    };

    private static ImmutableArray<AdditionalText> LoadAdditionalFiles()
    {
        const string additionalFilePrefix = "AdditionalFiles.";
        var assembly = Assembly.GetExecutingAssembly();
        var builder = ImmutableArray.CreateBuilder<AdditionalText>();

        foreach (var resourceName in assembly.GetManifestResourceNames().OrderBy(static n => n, StringComparer.Ordinal))
        {
            if (!resourceName.StartsWith(additionalFilePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing manifest resource: {resourceName}");
            using var reader = new StreamReader(stream);
            string content = reader.ReadToEnd();
            string fileName = resourceName.Substring(additionalFilePrefix.Length);
            builder.Add(new InMemoryAdditionalText(fileName, SourceText.From(content)));
        }

        return builder.ToImmutable();
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText text;

        internal InMemoryAdditionalText(string path, SourceText text)
        {
            this.Path = path;
            this.text = text;
        }

        public override string Path { get; }

        public override SourceText? GetText(CancellationToken cancellationToken = default) => this.text;
    }
}
