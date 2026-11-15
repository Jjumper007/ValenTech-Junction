using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

public class AyaS1Probe
{
    [Fact]
    public async Task Run()
    {
        ImmutableArray<MetadataReference> baseReferences =
            (await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None))
            .AddRange(ReferencesHelper.GetReferences(ReferencesSet.MessagePack));

        ImmutableArray<ISourceGenerator> generators = ImmutableArray.Create(
            ToSourceGenerator(new CompositeResolverGenerator()),
            ToSourceGenerator(new MessagePackGenerator()));

        ImmutableArray<DiagnosticAnalyzer> analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new MsgPack001SpecifyOptionsAnalyzer(),
            new MsgPack002UseConstantOptionsAnalyzer(),
            new MsgPack00xMessagePackAnalyzer());

        CSharpCompilationOptions compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithAllowUnsafe(false)
            .WithWarningLevel(99)
            .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                ["CS1591"] = ReportDiagnostic.Suppress,
            });

        foreach (var input in AyaProbe.Inputs())
        {
            await AyaProbe.RecordAsync(input.Key, async () => await DescribeAsync(input.Value, baseReferences, generators, analyzers, compilationOptions));
        }
    }

    private static async Task<string> DescribeAsync(
        string source,
        ImmutableArray<MetadataReference> references,
        ImmutableArray<ISourceGenerator> generators,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        CSharpCompilationOptions compilationOptions)
    {
        CSharpParseOptions parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        string[] lines = source.Split('\n');
        if (lines.Length > 0)
        {
            string firstLine = lines[0].TrimEnd('\r');
            const string symbolsPrefix = "// AYA-SYMBOLS:";
            if (firstLine.StartsWith(symbolsPrefix, StringComparison.Ordinal))
            {
                string[] symbols = firstLine
                    .Substring(symbolsPrefix.Length)
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                parseOptions = parseOptions.WithPreprocessorSymbols(symbols);
            }
        }

        string appSource = source;
        int splitIndex = Array.FindIndex(lines, static l => string.Equals(l.Trim(), "// AYA-SPLIT", StringComparison.Ordinal));
        if (splitIndex >= 0)
        {
            string libSource = string.Join("\n", lines.Take(splitIndex));
            appSource = string.Join("\n", lines.Skip(splitIndex + 1));

            SyntaxTree libTree = CSharpSyntaxTree.ParseText(libSource, parseOptions, path: "SplitLib.cs");
            CSharpCompilation libCompilation = CSharpCompilation.Create(
                "SplitLib",
                new[] { libTree },
                references,
                compilationOptions);

            using var ms = new MemoryStream();
            var emitResult = libCompilation.Emit(ms);
            if (!emitResult.Success)
            {
                return "SPLIT-FAILED";
            }

            ms.Position = 0;
            references = references.Add(MetadataReference.CreateFromStream(ms));
        }

        SyntaxTree inputTree = CSharpSyntaxTree.ParseText(appSource, parseOptions, path: "Input.cs");
        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { inputTree },
            references,
            compilationOptions);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updatedCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);

        var compilationWithAnalyzers = updatedCompilation.WithAnalyzers(
            analyzers,
            new CompilationWithAnalyzersOptions(
                options: null,
                onAnalyzerException: null,
                concurrentAnalysis: false,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: false));

        ImmutableArray<Diagnostic> analyzerDiagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        var diagLines = new List<string>();
        foreach (Diagnostic d in generatorDiagnostics.Concat(analyzerDiagnostics))
        {
            string message = d.GetMessage(CultureInfo.InvariantCulture);
            if (d.Id == "CS8785" || d.Id == "AD0001")
            {
                diagLines.Add($"CRASH {d.Id}: {FirstLine(message)}");
            }
            else
            {
                diagLines.Add($"DIAG {d.Id} {d.Severity}: {SingleLine(message)}");
            }
        }

        diagLines.Sort(StringComparer.Ordinal);

        var generatedSources = driver.GetRunResult().Results
            .SelectMany(static r => r.GeneratedSources)
            .OrderBy(static s => s.HintName, StringComparer.Ordinal)
            .ToList();

        var errorLines = new List<string>();
        foreach (Diagnostic d in updatedCompilation.GetDiagnostics())
        {
            if (d.Severity != DiagnosticSeverity.Error)
            {
                continue;
            }

            string prefix = d.Location.IsInSource && d.Location.SourceTree != null && d.Location.SourceTree != inputTree
                ? "ERROR-GEN"
                : "ERROR";
            string message = SingleLine(d.GetMessage(CultureInfo.InvariantCulture));
            errorLines.Add($"{prefix} {d.Id}: {message}");
        }

        errorLines.Sort(StringComparer.Ordinal);

        SemanticModel semanticModel = updatedCompilation.GetSemanticModel(inputTree);
        SyntaxNode root = await inputTree.GetRootAsync();
        var userTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (SyntaxNode node in root.DescendantNodes())
        {
            if (node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            {
                if (semanticModel.GetDeclaredSymbol(node) is INamedTypeSymbol symbol)
                {
                    userTypes.Add(symbol);
                }
            }
        }

        var userLines = new List<string>(userTypes.Count);
        foreach (INamedTypeSymbol type in userTypes)
        {
            string attributes = string.Join(
                ", ",
                type.GetAttributes()
                    .Select(static a => a.AttributeClass?.ToDisplayString() ?? "?")
                    .OrderBy(static s => s, StringComparer.Ordinal));

            var baseList = new List<string>();
            if (type.BaseType != null)
            {
                baseList.Add(type.BaseType.ToDisplayString());
            }

            foreach (INamedTypeSymbol iface in type.Interfaces)
            {
                baseList.Add(iface.ToDisplayString());
            }

            baseList.Sort(StringComparer.Ordinal);
            string bases = string.Join(", ", baseList);

            int memberCount = type.GetMembers().Length;
            userLines.Add($"USER {type.ToDisplayString()}: attributes [{attributes}]; bases [{bases}]; members {memberCount}");
        }

        userLines.Sort(StringComparer.Ordinal);

        var sb = new StringBuilder();
        foreach (string line in diagLines)
        {
            sb.AppendLine(line);
        }

        foreach (GeneratedSourceResult gen in generatedSources)
        {
            sb.AppendLine($"OUT {gen.HintName}");
            string text = gen.SourceText.ToString();
            sb.Append(text);
            if (text.Length > 0 && !text.EndsWith("\n", StringComparison.Ordinal))
            {
                sb.AppendLine();
            }
        }

        foreach (string line in errorLines)
        {
            sb.AppendLine(line);
        }

        foreach (string line in userLines)
        {
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private static ISourceGenerator ToSourceGenerator(object generator) => generator switch
    {
        ISourceGenerator sg => sg,
        IIncrementalGenerator ig => ig.AsSourceGenerator(),
        _ => throw new InvalidOperationException($"Unsupported generator type: {generator.GetType()}"),
    };

    private static string FirstLine(string text)
    {
        int nl = text.IndexOfAny(new[] { '\r', '\n' });
        return nl >= 0 ? text.Substring(0, nl) : text;
    }

    private static string SingleLine(string text)
    {
        return text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
    }
}
