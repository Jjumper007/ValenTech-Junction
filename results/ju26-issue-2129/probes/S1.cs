// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

public class AyaS1Probe
{
    private static readonly CSharpCompilationOptions BaseCompilationOptions =
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithAllowUnsafe(false)
            .WithWarningLevel(99)
            .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                ["CS1591"] = ReportDiagnostic.Suppress,
            });

    [Fact]
    public async Task ProbeAllInputs()
    {
        ImmutableArray<MetadataReference> baseReferences =
            (await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None))
            .AddRange(ReferencesHelper.GetReferences(ReferencesSet.MessagePack));

        ImmutableArray<ISourceGenerator> generators = ImmutableArray.Create(
            ToSourceGenerator(new MessagePackGenerator()),
            ToSourceGenerator(new CompositeResolverGenerator()));

        ImmutableArray<DiagnosticAnalyzer> analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new MsgPack001SpecifyOptionsAnalyzer(),
            new MsgPack002UseConstantOptionsAnalyzer(),
            new MsgPack00xMessagePackAnalyzer());

        foreach (KeyValuePair<string, string> input in AyaProbe.Inputs())
        {
            await AyaProbe.RecordAsync(input.Key, async () => await DescribeAsync(input.Value, baseReferences, generators, analyzers));
        }
    }

    private static async Task<string> DescribeAsync(
        string rawInput,
        ImmutableArray<MetadataReference> baseReferences,
        ImmutableArray<ISourceGenerator> generators,
        ImmutableArray<DiagnosticAnalyzer> analyzers)
    {
        CSharpParseOptions parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        string[]? symbols = TryParseSymbolsDirective(rawInput);
        if (symbols is not null)
        {
            parseOptions = parseOptions.WithPreprocessorSymbols(symbols);
        }

        ImmutableArray<MetadataReference> references = baseReferences;
        string inputSource = rawInput;

        if (TrySplitInput(rawInput, out string? librarySource, out string? mainSource))
        {
            SyntaxTree libTree = CSharpSyntaxTree.ParseText(librarySource!, parseOptions, path: "Lib.cs");
            CSharpCompilation libCompilation = CSharpCompilation.Create(
                "AyaSplitLib",
                new[] { libTree },
                baseReferences,
                BaseCompilationOptions);

            using MemoryStream peStream = new MemoryStream();
            if (!libCompilation.Emit(peStream).Success)
            {
                return "SPLIT-FAILED";
            }

            peStream.Position = 0;
            references = baseReferences.Add(MetadataReference.CreateFromStream(peStream));
            inputSource = mainSource!;
        }

        SyntaxTree inputTree = CSharpSyntaxTree.ParseText(inputSource, parseOptions, path: "Input.cs");
        CSharpCompilation compilation = CSharpCompilation.Create(
            "AyaTest",
            new[] { inputTree },
            references,
            BaseCompilationOptions);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updatedCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);

        CompilationWithAnalyzers compilationWithAnalyzers = updatedCompilation.WithAnalyzers(analyzers);
        ImmutableArray<Diagnostic> analyzerDiagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);

        List<string> diagLines = new List<string>();
        foreach (Diagnostic d in generatorDiagnostics.Concat(analyzerDiagnostics))
        {
            string rawMessage = d.GetMessage(CultureInfo.InvariantCulture);
            if (d.Id is "CS8785" or "AD0001")
            {
                string firstLine = rawMessage
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? string.Empty;
                diagLines.Add($"CRASH {d.Id}: {firstLine}");
            }
            else
            {
                string singleLine = NormalizeSingleLine(rawMessage);
                diagLines.Add($"DIAG {d.Id} {d.Severity}: {singleLine}");
            }
        }

        diagLines.Sort(StringComparer.Ordinal);

        List<GeneratedSourceResult> generatedSources = driver
            .GetRunResult()
            .Results
            .SelectMany(r => r.GeneratedSources)
            .OrderBy(s => s.HintName, StringComparer.Ordinal)
            .ToList();

        HashSet<SyntaxTree> generatedTrees = new HashSet<SyntaxTree>(updatedCompilation.SyntaxTrees.Where(t => t != inputTree));

        List<string> errorLines = new List<string>();
        foreach (Diagnostic d in updatedCompilation.GetDiagnostics(CancellationToken.None))
        {
            if (d.Severity != DiagnosticSeverity.Error)
            {
                continue;
            }

            bool isGenerated = d.Location.IsInSource && d.Location.SourceTree is not null && generatedTrees.Contains(d.Location.SourceTree);
            string prefix = isGenerated ? "ERROR-GEN" : "ERROR";
            errorLines.Add($"{prefix} {d.Id}: {NormalizeSingleLine(d.GetMessage(CultureInfo.InvariantCulture))}");
        }

        errorLines.Sort(StringComparer.Ordinal);

        List<string> userTypeLines = DescribeUserTypes(updatedCompilation, inputTree);

        StringBuilder sb = new StringBuilder();
        foreach (string line in diagLines)
        {
            sb.AppendLine(line);
        }

        foreach (GeneratedSourceResult generated in generatedSources)
        {
            sb.AppendLine($"OUT {generated.HintName}");
            sb.AppendLine(generated.SourceText.ToString());
        }

        foreach (string line in errorLines)
        {
            sb.AppendLine(line);
        }

        foreach (string line in userTypeLines)
        {
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private static List<string> DescribeUserTypes(Compilation compilation, SyntaxTree inputTree)
    {
        SemanticModel semanticModel = compilation.GetSemanticModel(inputTree);
        SyntaxNode root = inputTree.GetRoot();
        HashSet<INamedTypeSymbol> seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        List<string> lines = new List<string>();

        IEnumerable<MemberDeclarationSyntax> typeDeclarations = root
            .DescendantNodes()
            .Where(n => n is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            .Cast<MemberDeclarationSyntax>();

        foreach (MemberDeclarationSyntax decl in typeDeclarations)
        {
            if (semanticModel.GetDeclaredSymbol(decl) is not INamedTypeSymbol symbol || !seen.Add(symbol))
            {
                continue;
            }

            List<string> attributes = symbol
                .GetAttributes()
                .Select(a => a.AttributeClass?.ToDisplayString() ?? string.Empty)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            List<string> bases = new List<string>();
            if (symbol.BaseType is not null)
            {
                bases.Add(symbol.BaseType.ToDisplayString());
            }

            foreach (INamedTypeSymbol iface in symbol.Interfaces)
            {
                bases.Add(iface.ToDisplayString());
            }

            bases.Sort(StringComparer.Ordinal);

            int memberCount = symbol.GetMembers().Length;
            lines.Add($"USER {symbol.ToDisplayString()}: attributes [{string.Join(", ", attributes)}]; bases [{string.Join(", ", bases)}]; members {memberCount}");
        }

        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    private static string[]? TryParseSymbolsDirective(string input)
    {
        int lineEnd = input.IndexOf('\n');
        string firstLine = (lineEnd >= 0 ? input.Substring(0, lineEnd) : input).TrimEnd('\r');
        const string prefix = "// AYA-SYMBOLS:";
        if (!firstLine.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return firstLine
            .Substring(prefix.Length)
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TrySplitInput(string input, out string? librarySource, out string? mainSource)
    {
        string[] lines = input.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd('\r').Trim() == "// AYA-SPLIT")
            {
                librarySource = string.Join("\n", lines.Take(i));
                mainSource = string.Join("\n", lines.Skip(i + 1));
                return true;
            }
        }

        librarySource = null;
        mainSource = null;
        return false;
    }

    private static string NormalizeSingleLine(string text)
        => text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");

    private static ISourceGenerator ToSourceGenerator(object generator) => generator switch
    {
        ISourceGenerator sg => sg,
        IIncrementalGenerator ig => ig.AsSourceGenerator(),
        _ => throw new InvalidOperationException($"Unsupported generator type: {generator.GetType()}"),
    };
}
