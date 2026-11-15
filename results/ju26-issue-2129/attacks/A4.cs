// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator.Tests;
using VerifyCS = CSharpSourceGeneratorVerifier<MessagePack.SourceGenerator.MessagePackGenerator>;

public class AyaA4Attack
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaA4Attack(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task GeneratedFormatterUsedByMessagePackObject()
    {
        string testSource = /* lang=c#-test */ """
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

            [MessagePackObject]
            public class Outer
            {
                [Key(0)]
                public MyType Value { get; set; }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
        }.RunDefaultAsync(this.testOutputHelper);
    }

    [Fact]
    public async Task GeneratedFormatterAndHandWrittenFormatterInSameCompilation()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            public class TargetA
            {
                public int Id { get; set; }
            }

            public class TargetB
            {
                public string Name { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class GeneratedFormatter : IMessagePackFormatter<TargetA>
            {
            }

            public class HandWrittenFormatter : IMessagePackFormatter<TargetB>
            {
                public void Serialize(ref MessagePackWriter writer, TargetB value, MessagePackSerializerOptions options)
                {
                    writer.Write(value?.Name);
                }

                public TargetB Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
                {
                    return new TargetB { Name = reader.ReadString() };
                }
            }

            [MessagePackObject]
            public class Holder
            {
                [Key(0)]
                public TargetA A { get; set; }

                [Key(1)]
                public TargetB B { get; set; }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
        }.RunDefaultAsync(this.testOutputHelper);
    }

    [Fact]
    public async Task GeneratedFormatterWithKnownFormatterAssemblyAttribute()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            [assembly: MessagePackKnownFormatter(typeof(MyFormatter))]

            public class MyType
            {
                public int Id { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
        }.RunDefaultAsync(this.testOutputHelper);
    }

    [Fact]
    public async Task GeneratedFormatterSplitAcrossPartialDeclarations()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class MyFormatter
            {
            }

            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
        }.RunDefaultAsync(this.testOutputHelper);
    }

    [Fact]
    public async Task GeneratedFormatterWithSurrogateUsedByMessagePackObject()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
            }

            public class MySurrogate
            {
                [Key(0)]
                public int Id { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate))]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            }

            [MessagePackObject]
            public class Outer
            {
                [Key(0)]
                public MyType Value { get; set; }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
        }.RunDefaultAsync(this.testOutputHelper);
    }
}
