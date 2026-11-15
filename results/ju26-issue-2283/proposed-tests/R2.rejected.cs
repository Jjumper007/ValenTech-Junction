// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public class AyaR2Tests
{
    [Fact]
    public void ClosedGenericRecordStruct_FromCompilationReference_DoesNotReportDiagnosticsInReferencingCompilation()
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

        CSharpParseOptions parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        MetadataReference[] metadataReferences = GetMetadataReferences();

        CSharpCompilation libCompilation = CSharpCompilation.Create(
            "Lib",
            new[] { CSharpSyntaxTree.ParseText(libSource, parseOptions) },
            metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpCompilation appCompilation = CSharpCompilation.Create(
            "App",
            new[] { CSharpSyntaxTree.ParseText(appSource, parseOptions) },
            metadataReferences.Concat(new[] { libCompilation.ToMetadataReference() }),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new MessagePack.SourceGenerator.MessagePackGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(appCompilation, out _, out ImmutableArray<Diagnostic> generatorDiagnostics);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        Assert.Empty(generatorDiagnostics);
        Assert.Contains(runResult.GeneratedTrees, t => t.FilePath.EndsWith("App.AppResolver.g.cs", StringComparison.Ordinal));
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
        {
            foreach (string path in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                paths.Add(path);
            }
        }
        else
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                {
                    paths.Add(assembly.Location);
                }
            }
        }

        paths.Add(typeof(MessagePack.MessagePackObjectAttribute).Assembly.Location);
        paths.Add(typeof(MessagePack.MessagePackSerializer).Assembly.Location);

        return paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
    }
}
