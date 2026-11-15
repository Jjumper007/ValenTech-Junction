// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MessagePack;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaP3Probe
{
    private static string RunGenerator(string source)
    {
        _ = typeof(object);
        _ = typeof(Attribute);
        _ = typeof(List<>);
        _ = typeof(Enumerable);
        _ = typeof(MessagePackObjectAttribute);
        _ = typeof(GeneratedMessagePackResolverAttribute);
        _ = typeof(MessagePackSerializer);

        var referencePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var loadedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
            {
                if (referencePaths.Add(assembly.Location))
                {
                    string name = assembly.GetName().Name;
                    if (!string.IsNullOrEmpty(name))
                    {
                        loadedAssemblyNames.Add(name);
                    }
                }
            }
        }

        void AddManagedAssembliesFromDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(directory, "*.dll").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.StartsWith("MessagePack.SourceGenerator", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("xunit", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    AssemblyName assemblyName = AssemblyName.GetAssemblyName(file);
                    if (!string.IsNullOrEmpty(assemblyName.Name) && loadedAssemblyNames.Add(assemblyName.Name))
                    {
                        referencePaths.Add(file);
                    }
                }
                catch
                {
                }
            }
        }

        AddManagedAssembliesFromDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
        AddManagedAssembliesFromDirectory(AppContext.BaseDirectory);

        MetadataReference[] references = referencePaths
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(location => MetadataReference.CreateFromFile(location))
            .ToArray();

        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "ProbeAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MessagePackGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out ImmutableArray<Diagnostic> generatorDiagnostics);

        GeneratorDriverRunResult runResult = driver.GetRunResult();

        var sb = new StringBuilder();
        sb.AppendLine("=== Generator Diagnostics ===");
        foreach (Diagnostic diag in generatorDiagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Character))
        {
            FileLinePositionSpan span = diag.Location.GetLineSpan();
            sb.AppendLine($"{diag.Id} [{diag.Severity}] ({span.StartLinePosition.Line},{span.StartLinePosition.Character}): {diag.GetMessage(CultureInfo.InvariantCulture)}");
        }

        sb.AppendLine("=== Generated Trees ===");
        foreach (SyntaxTree tree in runResult.GeneratedTrees
            .OrderBy(t => t.FilePath.Replace('\\', '/'), StringComparer.Ordinal))
        {
            sb.AppendLine($"--- {tree.FilePath.Replace('\\', '/')} ---");
            sb.AppendLine(tree.ToString());
        }

        sb.AppendLine("=== Output Compilation Errors ===");
        foreach (Diagnostic diag in outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Character))
        {
            FileLinePositionSpan span = diag.Location.GetLineSpan();
            sb.AppendLine($"{diag.Id} ({span.StartLinePosition.Line},{span.StartLinePosition.Character}): {diag.GetMessage(CultureInfo.InvariantCulture)}");
        }

        return sb.ToString();
    }

    [Fact]
    public void SimpleIntKeyClass()
    {
        const string source = """
            using MessagePack;

            [GeneratedMessagePackResolver]
            public partial class GeneratedResolver
            {
            }

            [MessagePackObject]
            public class SimpleIntKeyClass
            {
                [Key(0)]
                public int Id { get; set; }

                [Key(1)]
                public string Name { get; set; }
            }
            """;

        AyaProbe.Record("SimpleIntKeyClass", () => RunGenerator(source));
    }

    [Fact]
    public void SimpleStringKeyClass()
    {
        const string source = """
            using MessagePack;

            namespace Sample.Resolvers
            {
                [GeneratedMessagePackResolver]
                internal partial class SampleResolver
                {
                }
            }

            [MessagePackObject]
            public class SimpleStringKeyClass
            {
                [Key("id")]
                public int Id { get; set; }

                [Key("name")]
                public string Name { get; set; }
            }
            """;

        AyaProbe.Record("SimpleStringKeyClass", () => RunGenerator(source));
    }

    [Fact]
    public void ContractlessKeyAsPropertyNameClass()
    {
        const string source = """
            using MessagePack;

            [GeneratedMessagePackResolver]
            public partial class GeneratedResolver
            {
            }

            [MessagePackObject(keyAsPropertyName: true)]
            public class ContractlessClass
            {
                public int Count { get; set; }

                public string Label { get; set; }

                [IgnoreMember]
                public int Ignored { get; set; }
            }
            """;

        AyaProbe.Record("ContractlessKeyAsPropertyNameClass", () => RunGenerator(source));
    }

    [Fact]
    public void GenericClassWithConstraints()
    {
        const string source = """
            using MessagePack;

            [GeneratedMessagePackResolver]
            public partial class GeneratedResolver
            {
            }

            [MessagePackObject]
            public class GenericBox<T> where T : class, new()
            {
                [Key(0)]
                public T Value { get; set; }
            }
            """;

        AyaProbe.Record("GenericClassWithConstraints", () => RunGenerator(source));
    }

    [Fact]
    public void NestedClassInNamespaceAndOuterType()
    {
        const string source = """
            using MessagePack;

            [GeneratedMessagePackResolver]
            public partial class GeneratedResolver
            {
            }

            namespace Sample.Models
            {
                public partial class OuterContainer
                {
                    [MessagePackObject]
                    public class InnerItem
                    {
                        [Key(0)]
                        public int Code { get; set; }
                    }
                }
            }
            """;

        AyaProbe.Record("NestedClassInNamespaceAndOuterType", () => RunGenerator(source));
    }

    [Fact]
    public void ValueTypeStruct()
    {
        const string source = """
            using MessagePack;

            [GeneratedMessagePackResolver]
            public partial class GeneratedResolver
            {
            }

            [MessagePackObject]
            public struct PointStruct
            {
                [Key(0)]
                public int X { get; set; }

                [Key(1)]
                public int Y { get; set; }
            }
            """;

        AyaProbe.Record("ValueTypeStruct", () => RunGenerator(source));
    }

    [Fact]
    public void SerializationConstructorClass()
    {
        const string source = """
            using MessagePack;

            [GeneratedMessagePackResolver]
            public partial class GeneratedResolver
            {
            }

            [MessagePackObject]
            public class ImmutablePair
            {
                [Key(0)]
                public int First { get; }

                [Key(1)]
                public string Second { get; }

                [SerializationConstructor]
                public ImmutablePair(int first, string second)
                {
                    this.First = first;
                    this.Second = second;
                }
            }
            """;

        AyaProbe.Record("SerializationConstructorClass", () => RunGenerator(source));
    }

    [Fact]
    public void EmptyMessagePackObjectClass()
    {
        const string source = """
            using MessagePack;

            [GeneratedMessagePackResolver]
            public partial class GeneratedResolver
            {
            }

            [MessagePackObject]
            public class EmptyClass
            {
            }
            """;

        AyaProbe.Record("EmptyMessagePackObjectClass", () => RunGenerator(source));
    }

    [Fact]
    public void SparseIntKeysClass()
    {
        const string source = """
            using MessagePack;

            [GeneratedMessagePackResolver]
            public partial class GeneratedResolver
            {
            }

            [MessagePackObject]
            public class SparseKeysClass
            {
                [Key(0)]
                public int First { get; set; }

                [Key(5)]
                public string Sixth { get; set; }
            }
            """;

        AyaProbe.Record("SparseIntKeysClass", () => RunGenerator(source));
    }

    [Fact]
    public void UnionInterfaceAndImplementations()
    {
        const string source = """
            using MessagePack;

            [GeneratedMessagePackResolver]
            public partial class GeneratedResolver
            {
            }

            [Union(0, typeof(UnionSubA))]
            [Union(1, typeof(UnionSubB))]
            public interface IUnionBase
            {
            }

            [MessagePackObject]
            public class UnionSubA : IUnionBase
            {
                [Key(0)]
                public int A { get; set; }
            }

            [MessagePackObject]
            public class UnionSubB : IUnionBase
            {
                [Key(0)]
                public string B { get; set; }
            }
            """;

        AyaProbe.Record("UnionInterfaceAndImplementations", () => RunGenerator(source));
    }

    [Fact]
    public void MessagePackObjectWithoutResolver()
    {
        const string source = """
            using MessagePack;

            [MessagePackObject]
            public class StandaloneItem
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        AyaProbe.Record("MessagePackObjectWithoutResolver", () => RunGenerator(source));
    }
}
