// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using MessagePack;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaA8Attack
{
    private static void AssertGeneratesCompilableFormatter(string source)
    {
        _ = typeof(MessagePackSerializer).Assembly;
        _ = typeof(GeneratedMessagePackFormatterAttribute).Assembly;

        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest));

        MetadataReference[] references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToArray();

        CSharpCompilation compilation = CSharpCompilation.Create(
            "AyaA8AttackAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePackGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out ImmutableArray<Diagnostic> generatorDiagnostics);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        GeneratorDriverRunResult runResult = driver.GetRunResult();
        Assert.NotEmpty(runResult.GeneratedTrees);

        ImmutableArray<Diagnostic> compileErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        Assert.Empty(compileErrors);
    }

    [Fact]
    public void Contractless_InitOnlyProperty_CompilesCleanly()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; init; }
                public string Name { get; set; } = string.Empty;
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        AssertGeneratesCompilableFormatter(source);
    }

    [Fact]
    public void Contractless_IndexerIsIgnored_CompilesCleanly()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }

                public int this[int index]
                {
                    get => index;
                    set { }
                }
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        AssertGeneratesCompilableFormatter(source);
    }

    [Fact]
    public void Contractless_OverriddenAndShadowedBaseMembers_CompilesCleanly()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class BaseType
            {
                public virtual int Id { get; set; }
                public string Name { get; set; } = string.Empty;
                public int BaseOnly { get; set; }
            }

            public class DerivedType : BaseType
            {
                public override int Id { get; set; }
                public new string Name { get; set; } = string.Empty;
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<DerivedType>
            {
            }
            """;

        AssertGeneratesCompilableFormatter(source);
    }

    [Fact]
    public void Contractless_RequiredProperty_CompilesCleanly()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public required int Id { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        AssertGeneratesCompilableFormatter(source);
    }

    [Fact]
    public void Contractless_StructWithMixedMembers_CompilesCleanly()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public struct MyStruct
            {
                public int Id;
                public string Name { get; set; }
                public readonly int ReadOnlyField;
                public int ReadOnlyProp => Id;
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyStruct>
            {
            }
            """;

        AssertGeneratesCompilableFormatter(source);
    }

    [Fact]
    public void Contractless_NoSerializableMembers_CompilesCleanly()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            public class EmptyType
            {
                public int ReadOnlyProp { get; }
                private int PrivateProp { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<EmptyType>
            {
            }
            """;

        AssertGeneratesCompilableFormatter(source);
    }
}
