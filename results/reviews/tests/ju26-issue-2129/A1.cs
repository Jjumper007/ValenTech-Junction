// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Behaviour that existed before <c>[GeneratedMessagePackFormatter]</c> (issue 2129) and must not change:
/// the input of these tests does not use the new attribute.
/// </summary>
public class AyaA1Attack
{
    private const string Preamble = "using System; using System.Collections.Generic; using MessagePack; using MessagePack.Formatters; using MessagePack.Resolvers;\n";

    /// <summary>
    /// One input that exercises every template the generator has: int-keyed and string-keyed objects, an enum,
    /// a union, array and generic members, a nested type, a struct, and a hand-written formatter of this compilation.
    /// </summary>
    private const string MixedInput = """
        namespace App.Model
        {
            public enum Color { Red, Green }

            [Union(0, typeof(Circle))]
            public interface IShape { }

            [MessagePackObject]
            public class Circle : IShape
            {
                [Key(0)] public int Radius { get; set; }
                [Key(2)] public Color Color { get; set; }
            }

            [MessagePackObject(true)]
            public class Named
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public Color[] Colors { get; set; }
                public List<Circle> Circles { get; set; }
            }

            [MessagePackObject]
            public struct Point
            {
                [Key(0)] public int X;
                [Key(1)] public int Y;
            }

            public partial class Outer
            {
                [MessagePackObject]
                public class Inner
                {
                    [Key(0)] public IShape Shape { get; set; }
                    [Key(1)] public Plain Plain { get; set; }
                }
            }

            public class Plain { public int Id { get; set; } }

            public class PlainFormatter : IMessagePackFormatter<Plain>
            {
                public void Serialize(ref MessagePackWriter writer, Plain value, MessagePackSerializerOptions options) => writer.Write(value.Id);
                public Plain Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => new Plain { Id = reader.ReadInt32() };
            }
        }
        """;

    [Fact]
    public void NoRegression_MessagePackObjectFormattersKeepTheirShapeAndRoundTrip()
    {
        RunResult r = Run(
            """
            [MessagePackObject]
            public class IntKeyed
            {
                [Key(0)] public int Id { get; set; }
                [Key(1)] public string Name { get; set; }
            }

            [MessagePackObject(true)]
            public class StringKeyed
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }

            public static class Entry
            {
                public static string Run()
                {
                    // Default options: the generated formatters are found without naming the generated resolver.
                    byte[] a = MessagePackSerializer.Serialize(new IntKeyed { Id = 7, Name = "seven" });
                    byte[] b = MessagePackSerializer.Serialize(new StringKeyed { Id = 7, Name = "seven" });
                    IntKeyed a2 = MessagePackSerializer.Deserialize<IntKeyed>(a);
                    StringKeyed b2 = MessagePackSerializer.Deserialize<StringKeyed>(b);
                    return MessagePackSerializer.ConvertToJson(a) + "|" + MessagePackSerializer.ConvertToJson(b)
                        + "|" + a2.Id + a2.Name + "|" + b2.Id + b2.Name
                        + "|" + StandardResolver.Instance.GetFormatter<IntKeyed>().GetType().FullName
                        + "|" + StandardResolver.Instance.GetFormatter<StringKeyed>().GetType().FullName;
                }
            }
            """);

        Assert.Empty(r.GeneratorDiagnostics);
        Assert.Null(r.GeneratorException);
        Assert.True(r.CompilerErrors.IsEmpty, string.Join(Environment.NewLine, r.CompilerErrors));

        string generated = string.Join("\n", r.GeneratedTrees.Select(t => t.GetText().ToString()));
        Assert.Contains("internal sealed class IntKeyedFormatter : MsgPack::Formatters.IMessagePackFormatter<global::IntKeyed>", generated);
        Assert.Contains("internal sealed class StringKeyedFormatter : global::MessagePack.Formatters.IMessagePackFormatter<global::StringKeyed>", generated);

        Assert.Equal(
            "[7,\"seven\"]|{\"Id\":7,\"Name\":\"seven\"}|7seven|7seven"
            + "|MessagePack.GeneratedMessagePackResolver+IntKeyedFormatter"
            + "|MessagePack.GeneratedMessagePackResolver+StringKeyedFormatter",
            Invoke(r));
    }

    [Fact]
    public void NoRegression_HandWrittenFormatterIsStillRegisteredInGeneratedResolver()
    {
        // The change added a filter to the search for IMessagePackFormatter<T> implementations of the compilation.
        // A formatter without the new attribute must still be picked up.
        RunResult r = Run(
            """
            public class Plain { public int Id { get; set; } }

            public class PlainFormatter : IMessagePackFormatter<Plain>
            {
                public void Serialize(ref MessagePackWriter writer, Plain value, MessagePackSerializerOptions options) => writer.Write(value.Id);
                public Plain Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => new Plain { Id = reader.ReadInt32() };
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)] public Plain Plain { get; set; }
            }

            public static class Entry
            {
                public static string Run()
                {
                    string formatter = MessagePack.GeneratedMessagePackResolver.Instance.GetFormatter<Plain>()?.GetType().FullName ?? "(none)";
                    byte[] bytes = MessagePackSerializer.Serialize(new Holder { Plain = new Plain { Id = 42 } });
                    return formatter + "|" + MessagePackSerializer.ConvertToJson(bytes) + "|" + MessagePackSerializer.Deserialize<Holder>(bytes).Plain.Id;
                }
            }
            """);

        Assert.Empty(r.GeneratorDiagnostics);
        Assert.Null(r.GeneratorException);
        Assert.True(r.CompilerErrors.IsEmpty, string.Join(Environment.NewLine, r.CompilerErrors));
        Assert.Equal("PlainFormatter|[42]|42", Invoke(r));
    }

    [Fact]
    public void NoRegression_GeneratedTextForExistingFeaturesIsUnchanged()
    {
        RunResult r = Run(MixedInput);

        Assert.Empty(r.GeneratorDiagnostics);
        Assert.Null(r.GeneratorException);
        Assert.True(r.CompilerErrors.IsEmpty, string.Join(Environment.NewLine, r.CompilerErrors));

        // File names and full text of everything the generator emits, in a stable order.
        StringBuilder all = new();
        foreach (SyntaxTree tree in r.GeneratedTrees.OrderBy(t => Path.GetFileName(t.FilePath), StringComparer.Ordinal))
        {
            all.Append("### ").Append(Path.GetFileName(tree.FilePath)).Append('\n');
            all.Append(tree.GetText().ToString().Replace("\r\n", "\n")).Append('\n');
        }

        string text = all.ToString();
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        // One resolver, four object formatters, one enum formatter and one union formatter; all formatters are 'internal sealed'.
        Assert.Equal(7, r.GeneratedTrees.Length);
        Assert.Equal(6, CountOccurrences(text, "internal sealed class "));

        // Measured on the commit before the change (32258fdb). Any difference in the emitted text changes it.
        Assert.True(
            hash == "C66AC275DF741003A9EA15BD70242FC316C385B90B329C0C08C1C7FE242B97C7",
            "The generated text changed. SHA-256 is now " + hash);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Emits the compilation, loads it and calls the public static <c>Entry.Run()</c> that the test source declares.
    /// </summary>
    private static object? Invoke(RunResult r)
    {
        using MemoryStream stream = new();
        Assert.True(r.Output.Emit(stream).Success, "Emit failed.");

        AssemblyLoadContext context = new("AyaA1Attack");
        Assembly assembly = context.LoadFromStream(new MemoryStream(stream.ToArray()));
        MethodInfo run = assembly.GetType("Entry", throwOnError: true)!.GetMethod("Run")!;
        try
        {
            return run.Invoke(null, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static RunResult Run(string source)
    {
        // The framework the tests run on, so that what is compiled here can also be loaded and run here.
        string frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        ImmutableArray<MetadataReference>.Builder references = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (string path in trusted.Split(Path.PathSeparator))
        {
            if (string.Equals(Path.GetDirectoryName(path), frameworkDirectory, StringComparison.OrdinalIgnoreCase))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        references.Add(MetadataReference.CreateFromFile(typeof(MessagePack.MessagePackObjectAttribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(MessagePack.MessagePackSerializer).Assembly.Location));

        CSharpParseOptions parseOptions = new(LanguageVersion.CSharp12);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Consumer",
            new[] { CSharpSyntaxTree.ParseText(Preamble + source, parseOptions, path: "Test.cs", encoding: Encoding.UTF8) },
            references.ToImmutable(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new MessagePackGenerator().AsSourceGenerator() }, parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> generatorDiagnostics);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        return new RunResult(
            output,
            generatorDiagnostics,
            runResult.Results.Select(result => result.Exception).FirstOrDefault(exception => exception is not null),
            output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray(),
            runResult.GeneratedTrees);
    }

    private sealed record RunResult(
        Compilation Output,
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        Exception? GeneratorException,
        ImmutableArray<Diagnostic> CompilerErrors,
        ImmutableArray<SyntaxTree> GeneratedTrees);
}
