// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

public class AyaP1Probe
{
    private static readonly MetadataReference[] CachedReferences = BuildMetadataReferences();

    [Fact]
    public void UnchangedWithTrivia()
    {
        string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class Sample
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        string source2 = """
            using MessagePack;

            // trivial comment
            [MessagePackObject]
            public class Sample
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        AyaProbe.Record("UnchangedWithTrivia", () => RunIncremental(source1, source2));
    }

    [Fact]
    public void ClassToStruct()
    {
        string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class Sample
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        string source2 = """
            using MessagePack;

            [MessagePackObject]
            public struct Sample
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        AyaProbe.Record("ClassToStruct", () => RunIncremental(source1, source2));
    }

    [Fact]
    public void GenericTypeParametersChanged()
    {
        string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class Box<T>
            {
                [Key(0)]
                public T Value { get; set; }
            }
            """;

        string source2 = """
            using MessagePack;

            [MessagePackObject]
            public class Box<T, U>
            {
                [Key(0)]
                public T Value { get; set; }

                [Key(1)]
                public U Extra { get; set; }
            }
            """;

        AyaProbe.Record("GenericTypeParametersChanged", () => RunIncremental(source1, source2));
    }

    [Fact]
    public void ConstructorParametersChanged()
    {
        string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class Item
            {
                [Key(0)]
                public int Id { get; }

                public Item(int id)
                {
                    this.Id = id;
                }
            }
            """;

        string source2 = """
            using MessagePack;

            [MessagePackObject]
            public class Item
            {
                [Key(0)]
                public int Id { get; }

                [Key(1)]
                public string Name { get; }

                public Item(int id, string name)
                {
                    this.Id = id;
                    this.Name = name;
                }
            }
            """;

        AyaProbe.Record("ConstructorParametersChanged", () => RunIncremental(source1, source2));
    }

    [Fact]
    public void IntKeyToStringKey()
    {
        string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class Entry
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        string source2 = """
            using MessagePack;

            [MessagePackObject]
            public class Entry
            {
                [Key("Id")]
                public int Id { get; set; }
            }
            """;

        AyaProbe.Record("IntKeyToStringKey", () => RunIncremental(source1, source2));
    }

    [Fact]
    public void MembersChanged()
    {
        string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class Person
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        string source2 = """
            using MessagePack;

            [MessagePackObject]
            public class Person
            {
                [Key(0)]
                public int Id { get; set; }

                [Key(1)]
                public string Name { get; set; }
            }
            """;

        AyaProbe.Record("MembersChanged", () => RunIncremental(source1, source2));
    }

    [Fact]
    public void MemberIgnored()
    {
        string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class Person
            {
                [Key(0)]
                public int Id { get; set; }

                [Key(1)]
                public string Name { get; set; }
            }
            """;

        string source2 = """
            using MessagePack;

            [MessagePackObject]
            public class Person
            {
                [Key(0)]
                public int Id { get; set; }

                [IgnoreMember]
                public string Name { get; set; }
            }
            """;

        AyaProbe.Record("MemberIgnored", () => RunIncremental(source1, source2));
    }

    [Fact]
    public void CallbackReceiverAdded()
    {
        string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class CallbackItem
            {
                [Key(0)]
                public int Id { get; set; }
            }
            """;

        string source2 = """
            using MessagePack;

            [MessagePackObject]
            public class CallbackItem : IMessagePackSerializationCallbackReceiver
            {
                [Key(0)]
                public int Id { get; set; }

                public void OnBeforeSerialize()
                {
                }

                public void OnAfterDeserialize()
                {
                }
            }
            """;

        AyaProbe.Record("CallbackReceiverAdded", () => RunIncremental(source1, source2));
    }

    [Fact]
    public void CallbackReceiverExplicitVsImplicit()
    {
        string source1 = """
            using MessagePack;

            [MessagePackObject]
            public class CallbackItem : IMessagePackSerializationCallbackReceiver
            {
                [Key(0)]
                public int Id { get; set; }

                public void OnBeforeSerialize()
                {
                }

                public void OnAfterDeserialize()
                {
                }
            }
            """;

        string source2 = """
            using MessagePack;

            [MessagePackObject]
            public class CallbackItem : IMessagePackSerializationCallbackReceiver
            {
                [Key(0)]
                public int Id { get; set; }

                void IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
                {
                }

                void IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()
                {
                }
            }
            """;

        AyaProbe.Record("CallbackReceiverExplicitVsImplicit", () => RunIncremental(source1, source2));
    }

    [Fact]
    public void KeyAsPropertyNameToggle()
    {
        string source1 = """
            using MessagePack;

            [MessagePackObject(false)]
            public class Config
            {
                [Key(0)]
                public int Count { get; set; }
            }
            """;

        string source2 = """
            using MessagePack;

            [MessagePackObject(true)]
            public class Config
            {
                public int Count { get; set; }
            }
            """;

        AyaProbe.Record("KeyAsPropertyNameToggle", () => RunIncremental(source1, source2));
    }

    [Fact]
    public void NamespaceChanged()
    {
        string source1 = """
            using MessagePack;

            namespace Alpha
            {
                [MessagePackObject]
                public class Sample
                {
                    [Key(0)]
                    public int Id { get; set; }
                }
            }
            """;

        string source2 = """
            using MessagePack;

            namespace Beta
            {
                [MessagePackObject]
                public class Sample
                {
                    [Key(0)]
                    public int Id { get; set; }
                }
            }
            """;

        AyaProbe.Record("NamespaceChanged", () => RunIncremental(source1, source2));
    }

    private static string RunIncremental(string source1, string source2)
    {
        SyntaxTree tree1 = CSharpSyntaxTree.ParseText(source1, path: "Test.cs");
        CSharpCompilation compilation1 = CSharpCompilation.Create(
            "ProbeAssembly",
            new[] { tree1 },
            CachedReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new MessagePack.SourceGenerator.MessagePackGenerator().AsSourceGenerator() });

        driver = driver.RunGenerators(compilation1);
        GeneratorDriverRunResult result1 = driver.GetRunResult();

        SyntaxTree tree2 = CSharpSyntaxTree.ParseText(source2, path: "Test.cs");
        CSharpCompilation compilation2 = compilation1.ReplaceSyntaxTree(tree1, tree2);

        driver = driver.RunGenerators(compilation2);
        GeneratorDriverRunResult result2 = driver.GetRunResult();

        var sb = new System.Text.StringBuilder();
        AppendResult(sb, "Run1", result1);
        AppendResult(sb, "Run2", result2);
        return sb.ToString();
    }

    private static void AppendResult(System.Text.StringBuilder sb, string label, GeneratorDriverRunResult result)
    {
        sb.AppendLine($"=== {label} ===");

        var orderedDiagnostics = result.Diagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
            .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Character)
            .ThenBy(d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal);

        foreach (Diagnostic d in orderedDiagnostics)
        {
            LinePosition pos = d.Location.GetLineSpan().StartLinePosition;
            sb.AppendLine($"DIAG {d.Id} ({pos.Line},{pos.Character}): {d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        var orderedTrees = result.GeneratedTrees
            .OrderBy(t => t.FilePath.Replace('\\', '/'), StringComparer.Ordinal);

        foreach (SyntaxTree tree in orderedTrees)
        {
            sb.AppendLine($"FILE {tree.FilePath.Replace('\\', '/')}");
            sb.AppendLine(tree.GetText().ToString());
        }
    }

    private static MetadataReference[] BuildMetadataReferences()
    {
        var refs = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

        _ = typeof(MessagePack.MessagePackObjectAttribute).Assembly;

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
        {
            foreach (string path in tpa.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    string name = Path.GetFileName(path);
                    if (!refs.ContainsKey(name))
                    {
                        refs[name] = MetadataReference.CreateFromFile(path);
                    }
                }
            }
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location) && File.Exists(asm.Location))
            {
                string name = Path.GetFileName(asm.Location);
                if (!refs.ContainsKey(name))
                {
                    refs[name] = MetadataReference.CreateFromFile(asm.Location);
                }
            }
        }

        return refs.Values.ToArray();
    }
}
