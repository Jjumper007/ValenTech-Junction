// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

public class AyaP3Probe
{
    [Fact]
    public async Task ProbeSimplePublicEnum()
    {
        const string source = """
            using MessagePack;

            public enum SimpleEnum
            {
                Zero,
                One,
                Two,
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)]
                public SimpleEnum Value { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("SimplePublicEnum", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ProbeByteBackedEnum()
    {
        const string source = """
            using MessagePack;

            public enum ByteEnum : byte
            {
                First = 1,
                Second = 2,
                Max = 255,
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)]
                public ByteEnum Value { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("ByteBackedEnum", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ProbeLongBackedEnum()
    {
        const string source = """
            using MessagePack;

            public enum LongEnum : long
            {
                Min = long.MinValue,
                Zero = 0,
                Max = long.MaxValue,
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)]
                public LongEnum Value { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("LongBackedEnum", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ProbeUShortBackedEnum()
    {
        const string source = """
            using MessagePack;

            public enum UShortEnum : ushort
            {
                Alpha = 10,
                Beta = 20,
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)]
                public UShortEnum Value { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("UShortBackedEnum", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ProbeEmptyEnum()
    {
        const string source = """
            using MessagePack;

            public enum EmptyEnum
            {
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)]
                public EmptyEnum Value { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("EmptyEnum", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ProbeFlagsEnum()
    {
        const string source = """
            using System;
            using MessagePack;

            [Flags]
            public enum Permissions
            {
                None = 0,
                Read = 1,
                Write = 2,
                Execute = 4,
                All = Read | Write | Execute,
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)]
                public Permissions Value { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("FlagsEnum", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ProbeNamespacedEnum()
    {
        const string source = """
            using MessagePack;

            namespace MyApp.Domain
            {
                public enum OrderStatus
                {
                    Pending,
                    Active,
                    Cancelled,
                }

                [MessagePackObject]
                public class Order
                {
                    [Key(0)]
                    public OrderStatus Status { get; set; }
                }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("NamespacedEnum", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ProbeNestedEnumInClass()
    {
        const string source = """
            using MessagePack;

            namespace MyApp.Models
            {
                public partial class OuterContainer
                {
                    public enum NestedKind
                    {
                        First,
                        Second,
                    }

                    [MessagePackObject]
                    public class Item
                    {
                        [Key(0)]
                        public NestedKind Kind { get; set; }
                    }
                }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("NestedEnumInClass", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ProbeDeeplyNestedEnum()
    {
        const string source = """
            using MessagePack;

            public partial class Outer
            {
                public partial class Inner
                {
                    public enum DeepEnum
                    {
                        X,
                        Y,
                    }
                }
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)]
                public Outer.Inner.DeepEnum Value { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("DeeplyNestedEnum", () => RunGeneratorAsync(source));
    }

    [Fact]
    public async Task ProbeInternalEnum()
    {
        const string source = """
            using MessagePack;

            internal enum InternalDirection
            {
                Left,
                Right,
            }

            [MessagePackObject]
            internal class Holder
            {
                [Key(0)]
                public InternalDirection Direction { get; set; }
            }

            namespace MessagePack
            {
                [GeneratedMessagePackResolver]
                partial class GeneratedMessagePackResolver
                {
                }
            }
            """;

        await AyaProbe.RecordAsync("InternalEnum", () => RunGeneratorAsync(source));
    }

    private static async Task<string> RunGeneratorAsync(string source)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaP3Probe",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();

        string trees = string.Join(
            "\n---\n",
            result.GeneratedTrees
                .OrderBy(t => t.FilePath, StringComparer.Ordinal)
                .Select(t => $"FILE: {t.FilePath.Replace('\\', '/')}\n{t.GetText()}"));

        string diagnostics = string.Join(
            "\n",
            result.Diagnostics
                .OrderBy(d => d.Id, StringComparer.Ordinal)
                .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
                .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Character)
                .Select(d => $"{d.Id} ({d.Location.GetLineSpan().StartLinePosition}): {d.GetMessage(CultureInfo.InvariantCulture)}"));

        return $"TREES:\n{trees}\nDIAGNOSTICS:\n{diagnostics}";
    }
}
