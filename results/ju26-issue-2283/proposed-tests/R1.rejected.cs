// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MessagePack;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaR1Tests
{
    [Fact]
    public void DiagnosticInCompilationReference_IsNotReported()
    {
        List<Assembly> explicitAssemblies =
        [
            typeof(object).Assembly,
            typeof(Attribute).Assembly,
            typeof(Enumerable).Assembly,
            typeof(MessagePackObjectAttribute).Assembly,
            typeof(MessagePackSerializer).Assembly,
            typeof(GeneratedMessagePackResolverAttribute).Assembly,
        ];

        foreach (string assemblyName in new[] { "netstandard", "System.Runtime" })
        {
            try
            {
                explicitAssemblies.Add(Assembly.Load(assemblyName));
            }
            catch
            {
            }
        }

        MetadataReference[] baseReferences = AppDomain.CurrentDomain.GetAssemblies()
            .Concat(explicitAssemblies)
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location))
            .ToArray();

        CSharpParseOptions parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        CSharpCompilationOptions compilationOptions = new(OutputKind.DynamicallyLinkedLibrary);

        string libSource = """
using MessagePack;

namespace Lib;

[MessagePackObject]
public partial class LibObject
{
    public int MissingKey { get; set; }
}
""";

        string appSource = """
using MessagePack;
using Lib;

namespace App;

[MessagePackObject]
public partial class ValidObject
{
    [Key(0)]
    public int Value { get; set; }
}

[MessagePackObject]
public partial class AppObject
{
    [Key(0)]
    public LibObject Member { get; set; }
}

[GeneratedMessagePackResolver]
partial class AppResolver { }
""";

        CSharpCompilation libCompilation = CSharpCompilation.Create(
            "Lib",
            [CSharpSyntaxTree.ParseText(libSource, parseOptions)],
            baseReferences,
            compilationOptions);

        Assert.Empty(libCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        CSharpCompilation appCompilation = CSharpCompilation.Create(
            "App",
            [CSharpSyntaxTree.ParseText(appSource, parseOptions)],
            [.. baseReferences, libCompilation.ToMetadataReference()],
            compilationOptions);

        Assert.Empty(appCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(appCompilation, out Compilation outputCompilation, out var generatorDiagnostics);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        Assert.Empty(generatorDiagnostics);
        Assert.NotEmpty(runResult.GeneratedTrees);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }
}
