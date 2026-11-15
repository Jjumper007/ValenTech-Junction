// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public class AyaA2Attack
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaA2Attack(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddAssembly(Assembly assembly)
        {
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                locations.Add(assembly.Location);
            }
        }

        AddAssembly(typeof(object).Assembly);
        AddAssembly(typeof(Enumerable).Assembly);
        AddAssembly(typeof(MessagePackObjectAttribute).Assembly);
        AddAssembly(typeof(MessagePackSerializer).Assembly);
        AddAssembly(typeof(IMessagePackFormatter<>).Assembly);

        string? runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (runtimeDir != null)
        {
            foreach (string name in new[] { "System.Runtime.dll", "System.Collections.dll", "netstandard.dll" })
            {
                string path = Path.Combine(runtimeDir, name);
                if (File.Exists(path))
                {
                    locations.Add(path);
                }
            }
        }

        return locations.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToImmutableArray();
    }

    private static (GeneratorDriverRunResult First, GeneratorDriverRunResult Second) RunIncremental(string source1, string source2)
    {
        SyntaxTree tree1 = CSharpSyntaxTree.ParseText(source1);
        CSharpCompilation compilation1 = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { tree1 },
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new MessagePackGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation1);
        GeneratorDriverRunResult first = driver.GetRunResult();

        SyntaxTree tree2 = CSharpSyntaxTree.ParseText(source2);
        CSharpCompilation compilation2 = compilation1.ReplaceSyntaxTree(tree1, tree2);
        driver = driver.RunGenerators(compilation2);
        GeneratorDriverRunResult second = driver.GetRunResult();

        return (first, second);
    }

    private static string CombineGeneratedText(GeneratorDriverRunResult result)
    {
        return string.Join("\n", result.GeneratedTrees.Select(t => t.ToString()));
    }

    [Fact]
    public void IncrementalRun_UnchangedMessagePackObject_ProducesIdenticalOutput()
    {
        const string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class MyType
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        const string source2 = """
            using MessagePack;

            [MessagePackObject]
            public class MyType
            {
                [Key(0)]
                public int Id { get; set; }
            }

            public class Unrelated
            {
                public int X { get; set; }
            }
            """;

        (GeneratorDriverRunResult first, GeneratorDriverRunResult second) = RunIncremental(source1, source2);

        Assert.Empty(first.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(second.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(first.GeneratedTrees);
        Assert.Equal(CombineGeneratedText(first), CombineGeneratedText(second));
    }

    [Fact]
    public void IncrementalRun_AddedMemberToMessagePackObject_UpdatesGeneratedFormatter()
    {
        const string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class MyType
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        const string source2 = """
            using MessagePack;

            [MessagePackObject]
            public class MyType
            {
                [Key(0)]
                public int Id { get; set; }

                [Key(1)]
                public string Name { get; set; } = string.Empty;
            }
            """;

        (GeneratorDriverRunResult first, GeneratorDriverRunResult second) = RunIncremental(source1, source2);

        Assert.Empty(first.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(second.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        string generated1 = CombineGeneratedText(first);
        string generated2 = CombineGeneratedText(second);

        Assert.DoesNotContain("Name", generated1);
        Assert.Contains("Name", generated2);
        Assert.NotEqual(generated1, generated2);
    }

    [Fact]
    public void IncrementalRun_ClassChangedToStruct_UpdatesGeneratedFormatter()
    {
        const string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class MyType
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        const string source2 = """
            using MessagePack;

            [MessagePackObject]
            public struct MyType
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        (GeneratorDriverRunResult first, GeneratorDriverRunResult second) = RunIncremental(source1, source2);

        Assert.Empty(first.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(second.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        string generated1 = CombineGeneratedText(first);
        string generated2 = CombineGeneratedText(second);

        Assert.NotEmpty(generated1);
        Assert.NotEmpty(generated2);
        Assert.NotEqual(generated1, generated2);
    }

    [Fact]
    public void IncrementalRun_SwitchedToUserDeclaredFormatter_UpdatesGeneratedFormatter()
    {
        const string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class MyType
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        const string source2 = """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        (GeneratorDriverRunResult first, GeneratorDriverRunResult second) = RunIncremental(source1, source2);

        Assert.Empty(first.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(second.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        string generated1 = CombineGeneratedText(first);
        string generated2 = CombineGeneratedText(second);

        Assert.DoesNotContain("partial class MyFormatter", generated1);
        Assert.Contains("partial class MyFormatter", generated2);
        Assert.NotEqual(generated1, generated2);
    }
}
