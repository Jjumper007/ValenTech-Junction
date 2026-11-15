// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using MessagePack;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaA6Attack
{
    [Fact]
    public void CustomTypeNamedInt32InNamespace_DoesNotCollideWithSystemInt32()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            namespace CustomTypes
            {
                [MessagePackObject]
                public class Int32
                {
                    [Key(0)]
                    public int Inner { get; set; }
                }
            }

            public class MyType
            {
                public CustomTypes.Int32 Value { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        AssertGeneratedFormatterCompiles(source);
    }

    [Fact]
    public void CustomStructNamedInt32Nullable_DoesNotCollideWithSystemNullableInt32()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            namespace CustomTypes
            {
                [MessagePackObject]
                public struct Int32
                {
                    [Key(0)]
                    public int Inner { get; set; }
                }
            }

            public class MyType
            {
                public CustomTypes.Int32? Value { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        AssertGeneratedFormatterCompiles(source);
    }

    [Fact]
    public void CustomTypeNamedStringInNamespace_DoesNotCollideWithSystemString()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            namespace CustomTypes
            {
                [MessagePackObject]
                public class String
                {
                    [Key(0)]
                    public int LengthValue { get; set; }
                }
            }

            public class MyType
            {
                public CustomTypes.String Text { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        AssertGeneratedFormatterCompiles(source);
    }

    [Fact]
    public void CustomGenericTypeNamedNullableInNamespace_DoesNotCollideWithSystemNullable()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            namespace CustomTypes
            {
                [MessagePackObject]
                public class Nullable<T>
                {
                    [Key(0)]
                    public T Item { get; set; }
                }
            }

            public class MyType
            {
                public CustomTypes.Nullable<int> Value { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        AssertGeneratedFormatterCompiles(source);
    }

    [Fact]
    public void CustomTypeNamedByteArrayInNamespace_DoesNotCollideWithSystemByteArray()
    {
        string source = """
            using MessagePack;
            using MessagePack.Formatters;

            namespace CustomTypes
            {
                [MessagePackObject]
                public class Byte
                {
                    [Key(0)]
                    public int Inner { get; set; }
                }
            }

            public class MyType
            {
                public CustomTypes.Byte[] Data { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        AssertGeneratedFormatterCompiles(source);
    }

    private static void AssertGeneratedFormatterCompiles(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        _ = typeof(MessagePackSerializer).Assembly;
        _ = typeof(KeyAttribute).Assembly;

        MetadataReference[] references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .GroupBy(a => a.Location)
            .Select(g => MetadataReference.CreateFromFile(g.First().Location))
            .ToArray();

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaA6AttackTestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePackGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out var generatorDiagnostics);

        Assert.Empty(generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.True(outputCompilation.SyntaxTrees.Count() > 1, "Expected MessagePackGenerator to emit formatter source.");

        Diagnostic[] compileErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(compileErrors);
    }
}
