// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaK1Attack
{
    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        _ = typeof(MessagePackSerializer).Assembly;
        _ = typeof(KeyAttribute).Assembly;
        _ = typeof(IMessagePackFormatter<>).Assembly;

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToArray();
    }

    private static CSharpCompilation CreateCompilation(params string[] sources)
    {
        SyntaxTree[] trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
        return CSharpCompilation.Create(
            "TestAssembly",
            trees,
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void SurrogateTypeArrayDoesNotCrashOrFallbackToContractless()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int SecretId { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(int[]))]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        CSharpCompilation compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePackGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);

        Assert.DoesNotContain(generatorDiagnostics, d => d.Id == "AD0001");

        string generated = string.Join("\n", outputCompilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        Assert.DoesNotContain("SecretId", generated);
    }

    [Fact]
    public void SurrogateTypeOpenGenericDoesNotCrashOrEmitInvalidCode()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int SecretId { get; set; }
            }

            public class MySurrogate<T>
            {
                [Key(0)]
                public int SecretId { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate<>))]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        CSharpCompilation compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePackGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);

        Assert.DoesNotContain(generatorDiagnostics, d => d.Id == "AD0001");

        string generated = string.Join("\n", outputCompilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        Assert.DoesNotContain("SecretId", generated);
    }

    [Fact]
    public void IncrementalUpdateOfFormattedTypeRegeneratesFormatter()
    {
        string formatterSource = """
            using MessagePack;
            using MessagePack.Formatters;

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        string typeSourceV1 = """
            public class MyType
            {
                public int Id { get; set; }
            }
            """;

        string typeSourceV2 = """
            public class MyType
            {
                public int Id { get; set; }
                public string UpdatedProperty { get; set; }
            }
            """;

        SyntaxTree formatterTree = CSharpSyntaxTree.ParseText(formatterSource);
        SyntaxTree typeTreeV1 = CSharpSyntaxTree.ParseText(typeSourceV1);
        SyntaxTree typeTreeV2 = CSharpSyntaxTree.ParseText(typeSourceV2);

        CSharpCompilation compilation1 = CSharpCompilation.Create(
            "TestAssembly",
            new[] { formatterTree, typeTreeV1 },
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePackGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation1, out Compilation output1, out _);

        string generated1 = string.Join("\n", output1.SyntaxTrees.Skip(2).Select(t => t.ToString()));
        Assert.Contains("Id", generated1);
        Assert.DoesNotContain("UpdatedProperty", generated1);

        CSharpCompilation compilation2 = compilation1.ReplaceSyntaxTree(typeTreeV1, typeTreeV2);
        driver.RunGeneratorsAndUpdateCompilation(compilation2, out Compilation output2, out ImmutableArray<Diagnostic> diagnostics2);

        Assert.DoesNotContain(diagnostics2, d => d.Id == "AD0001");
        string generated2 = string.Join("\n", output2.SyntaxTrees.Skip(2).Select(t => t.ToString()));
        Assert.Contains("UpdatedProperty", generated2);
    }

    [Fact]
    public void PartialStructFormatterDoesNotEmitMismatchedClassDeclaration()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial struct MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        CSharpCompilation compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePackGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);

        Assert.DoesNotContain(generatorDiagnostics, d => d.Id == "AD0001");

        ImmutableArray<Diagnostic> compilerDiagnostics = outputCompilation.GetDiagnostics();
        Assert.DoesNotContain(compilerDiagnostics, d => d.Id == "CS0260" || d.Id == "CS0261");
    }
}
