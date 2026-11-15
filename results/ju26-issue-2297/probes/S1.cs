// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Globalization;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;

public class AyaS1Probe
{
    private const string SymbolsPrefix = "// AYA-SYMBOLS:";
    private const string SplitMarker = "// AYA-SPLIT";

    [Fact]
    public async Task RunAll()
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences
            .ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        ImmutableArray<MetadataReference> baseReferences = frameworkReferences
            .Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack))
            .ToImmutableArray();

        ImmutableArray<DiagnosticAnalyzer> analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new MsgPack001SpecifyOptionsAnalyzer(),
            new MsgPack002UseConstantOptionsAnalyzer(),
            new MsgPack00xMessagePackAnalyzer());

        foreach (var input in AyaProbe.Inputs())
        {
            await AyaProbe.RecordAsync(input.Key, async () => await DescribeAsync(input.Value, baseReferences, analyzers));
        }
    }

    private static async Task<string> DescribeAsync(
        string source,
        ImmutableArray<MetadataReference> baseReferences,
        ImmutableArray<DiagnosticAnalyzer> analyzers)
    {
        string normalized = source.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] allLines = normalized.Split('\n');

        CSharpParseOptions parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        if (allLines.Length > 0 && allLines[0].StartsWith(SymbolsPrefix, StringComparison.Ordinal))
        {
            string[] symbols = allLines[0]
                .Substring(SymbolsPrefix.Length)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            parseOptions = parseOptions.WithPreprocessorSymbols(symbols);
        }

        IEnumerable<MetadataReference> references = baseReferences;
        string inputSource = normalized;

        int splitIndex = Array.FindIndex(allLines, line => line.Trim() == SplitMarker);
        if (splitIndex >= 0)
        {
            string libSource = string.Join("\n", allLines, 0, splitIndex);
            inputSource = string.Join("\n", allLines, splitIndex + 1, allLines.Length - splitIndex - 1);

            SyntaxTree libTree = CSharpSyntaxTree.ParseText(libSource, parseOptions, path: "SplitLib.cs");
            CSharpCompilation libCompilation = CSharpCompilation.Create(
                assemblyName: "SplitLib",
                syntaxTrees: new[] { libTree },
                references: baseReferences,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using MemoryStream libStream = new MemoryStream();
            EmitResult emitResult = libCompilation.Emit(libStream);
            if (!emitResult.Success)
            {
                return "SPLIT-FAILED";
            }

            libStream.Position = 0;
            references = baseReferences.Add(MetadataReference.CreateFromStream(libStream));
        }

        SyntaxTree inputTree = CSharpSyntaxTree.ParseText(inputSource, parseOptions, path: "Input.cs");
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "ProbeAssembly",
            syntaxTrees: new[] { inputTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new CompositeResolverGenerator(),
            new MessagePackGenerator())
            .WithUpdatedParseOptions(parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation updatedCompilation,
            out ImmutableArray<Diagnostic> generatorDiagnostics);

        ImmutableArray<Diagnostic> analyzerDiagnostics = await compilation
            .WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync();

        List<string> outputLines = new List<string>();

        List<string> diagLines = new List<string>();
        foreach (Diagnostic d in generatorDiagnostics.Concat(analyzerDiagnostics))
        {
            string message = d.GetMessage(CultureInfo.InvariantCulture).Replace("\r\n", "\n").Replace("\r", "\n");
            if (d.Id == "CS8785" || d.Id == "AD0001")
            {
                int newlineIndex = message.IndexOf('\n');
                string firstLine = newlineIndex >= 0 ? message.Substring(0, newlineIndex) : message;
                diagLines.Add($"CRASH {d.Id}: {firstLine}");
            }
            else
            {
                diagLines.Add($"DIAG {d.Id} {d.Severity}: {message}");
            }
        }

        diagLines.Sort(StringComparer.Ordinal);
        outputLines.AddRange(diagLines);

        GeneratorDriverRunResult runResult = driver.GetRunResult();
        HashSet<SyntaxTree> generatedTrees = new HashSet<SyntaxTree>(runResult.GeneratedTrees);

        foreach (var gen in runResult.GeneratedTrees
            .Select(t => (Name: t.FilePath.Replace('\\', '/'), Text: t.GetText().ToString().Replace("\r\n", "\n").Replace("\r", "\n")))
            .OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            outputLines.Add($"OUT {gen.Name}");
            outputLines.Add(gen.Text);
        }

        List<string> errorLines = new List<string>();
        foreach (Diagnostic d in updatedCompilation.GetDiagnostics())
        {
            if (d.Severity != DiagnosticSeverity.Error)
            {
                continue;
            }

            string prefix = d.Location.IsInSource && d.Location.SourceTree != null && generatedTrees.Contains(d.Location.SourceTree)
                ? "ERROR-GEN"
                : "ERROR";
            string message = d.GetMessage(CultureInfo.InvariantCulture).Replace("\r\n", "\n").Replace("\r", "\n");
            errorLines.Add($"{prefix} {d.Id}: {message}");
        }

        errorLines.Sort(StringComparer.Ordinal);
        outputLines.AddRange(errorLines);

        SemanticModel semanticModel = updatedCompilation.GetSemanticModel(inputTree);
        SyntaxNode root = await inputTree.GetRootAsync();
        HashSet<INamedTypeSymbol> userTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

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

        List<string> userLines = new List<string>();
        foreach (INamedTypeSymbol symbol in userTypes)
        {
            string typeName = symbol.ToDisplayString();

            List<string> attributes = symbol.GetAttributes()
                .Select(a => a.AttributeClass?.ToDisplayString() ?? string.Empty)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

            List<string> bases = new List<string>();
            if (symbol.BaseType != null)
            {
                bases.Add(symbol.BaseType.ToDisplayString());
            }

            foreach (INamedTypeSymbol iface in symbol.Interfaces)
            {
                bases.Add(iface.ToDisplayString());
            }

            bases.Sort(StringComparer.Ordinal);

            int memberCount = symbol.GetMembers().Length;
            userLines.Add($"USER {typeName}: attributes [{string.Join(", ", attributes)}]; bases [{string.Join(", ", bases)}]; members {memberCount}");
        }

        userLines.Sort(StringComparer.Ordinal);
        outputLines.AddRange(userLines);

        return string.Join("\n", outputLines);
    }
}
