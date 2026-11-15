using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaP2Probe
{
    [Fact]
    public async Task ProbeCompositeResolverGeneration()
    {
        ImmutableArray<MetadataReference> frameworkReferences =
            await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        MetadataReference[] references = frameworkReferences
            .Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack))
            .ToArray();

        await AyaProbe.RecordAsync("EmptyCompositeResolver", () => RunGenerator(references, """
            using MessagePack;

            namespace TestApp
            {
                [CompositeResolver]
                partial class EmptyCompositeResolver
                {
                }
            }
            """));

        await AyaProbe.RecordAsync("SingleBuiltinResolver", () => RunGenerator(references, """
            using MessagePack;
            using MessagePack.Resolvers;

            namespace TestApp
            {
                [CompositeResolver(typeof(BuiltinResolver))]
                partial class SingleBuiltinCompositeResolver
                {
                }
            }
            """));

        await AyaProbe.RecordAsync("MultipleStandardResolvers", () => RunGenerator(references, """
            using MessagePack;
            using MessagePack.Resolvers;

            namespace TestApp
            {
                [CompositeResolver(
                    typeof(NativeDateTimeResolver),
                    typeof(BuiltinResolver),
                    typeof(StandardResolver))]
                partial class MultiCompositeResolver
                {
                }
            }
            """));

        await AyaProbe.RecordAsync("CompositeResolverInGlobalNamespace", () => RunGenerator(references, """
            using MessagePack;
            using MessagePack.Resolvers;

            [CompositeResolver(typeof(BuiltinResolver))]
            partial class GlobalCompositeResolver
            {
            }
            """));

        await AyaProbe.RecordAsync("NestedCompositeResolver", () => RunGenerator(references, """
            using MessagePack;
            using MessagePack.Resolvers;

            namespace OuterNs.InnerNs
            {
                public partial class OuterContainer
                {
                    [CompositeResolver(typeof(BuiltinResolver), typeof(PrimitiveObjectResolver))]
                    partial class NestedCompositeResolver
                    {
                    }
                }
            }
            """));

        await AyaProbe.RecordAsync("PublicCompositeResolverWithGeneratedResolver", () => RunGenerator(references, """
            using MessagePack;
            using MessagePack.Resolvers;

            namespace TestApp
            {
                [MessagePackObject]
                public class SampleData
                {
                    [Key(0)]
                    public int Id { get; set; }
                }

                [GeneratedMessagePackResolver]
                partial class LocalGeneratedResolver
                {
                }

                [CompositeResolver(typeof(LocalGeneratedResolver), typeof(StandardResolver))]
                public partial class PublicAppCompositeResolver
                {
                }
            }
            """));

        await AyaProbe.RecordAsync("CompositeResolverWithCustomFormatter", () => RunGenerator(references, """
            using MessagePack;
            using MessagePack.Formatters;
            using MessagePack.Resolvers;

            namespace TestApp
            {
                public sealed class CustomIntFormatter : IMessagePackFormatter<int>
                {
                    public void Serialize(ref MessagePackWriter writer, int value, MessagePackSerializerOptions options)
                    {
                        writer.Write(value);
                    }

                    public int Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
                    {
                        return reader.ReadInt32();
                    }
                }

                [CompositeResolver(typeof(CustomIntFormatter), typeof(BuiltinResolver))]
                partial class FormatterAndResolverComposite
                {
                }
            }
            """));

        await AyaProbe.RecordAsync("CompositeResolverWithCustomResolverInstanceAndCtor", () => RunGenerator(references, """
            using MessagePack;
            using MessagePack.Formatters;
            using MessagePack.Resolvers;

            namespace TestApp
            {
                public sealed class SingletonCustomResolver : IFormatterResolver
                {
                    public static readonly SingletonCustomResolver Instance = new SingletonCustomResolver();

                    private SingletonCustomResolver() { }

                    public IMessagePackFormatter<T> GetFormatter<T>() => null;
                }

                public sealed class ConstructibleCustomResolver : IFormatterResolver
                {
                    public IMessagePackFormatter<T> GetFormatter<T>() => null;
                }

                [CompositeResolver(
                    typeof(SingletonCustomResolver),
                    typeof(ConstructibleCustomResolver),
                    typeof(BuiltinResolver))]
                partial class CustomResolversComposite
                {
                }
            }
            """));

        await AyaProbe.RecordAsync("CompositeResolverWithOnlyFormatters", () => RunGenerator(references, """
            using MessagePack;
            using MessagePack.Formatters;

            namespace TestApp
            {
                public sealed class CustomStringFormatter : IMessagePackFormatter<string>
                {
                    public void Serialize(ref MessagePackWriter writer, string value, MessagePackSerializerOptions options)
                    {
                        writer.Write(value);
                    }

                    public string Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
                    {
                        return reader.ReadString();
                    }
                }

                [CompositeResolver(typeof(CustomStringFormatter))]
                partial class FormatterOnlyCompositeResolver
                {
                }
            }
            """));

        await AyaProbe.RecordAsync("MultipleCompositeResolversInSameCompilation", () => RunGenerator(references, """
            using MessagePack;
            using MessagePack.Resolvers;

            namespace TestApp.First
            {
                [CompositeResolver(typeof(BuiltinResolver))]
                partial class FirstCompositeResolver
                {
                }
            }

            namespace TestApp.Second
            {
                [CompositeResolver(typeof(StandardResolver))]
                partial class SecondCompositeResolver
                {
                }
            }
            """));
    }

    private static Task<string> RunGenerator(MetadataReference[] references, string source)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaP2ProbeAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        ISourceGenerator[] generators = typeof(MessagePackGenerator).Assembly
            .GetExportedTypes()
            .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(Activator.CreateInstance)
            .Select(instance => instance switch
            {
                ISourceGenerator sg => sg,
                IIncrementalGenerator ig => ig.AsSourceGenerator(),
                _ => null,
            })
            .Where(g => g is not null)
            .ToArray()!;

        var driver = CSharpGeneratorDriver.Create(generators);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();

        var trees = result.GeneratedTrees
            .OrderBy(t => t.FilePath, StringComparer.Ordinal)
            .Select(t => $"=== FILE: {t.FilePath} ===\n{t.GetText()}");

        var diagnostics = result.Diagnostics
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Location.SourceSpan.Start)
            .ThenBy(d => d.GetMessage(), StringComparer.Ordinal)
            .Select(d => $"DIAG: {d.Id} [{d.Location.SourceSpan.Start}..{d.Location.SourceSpan.End}]: {d.GetMessage()}");

        string output = string.Join("\n", trees.Concat(diagnostics));
        return Task.FromResult(output);
    }
}
