// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using MessagePack.SourceGenerator.Tests;
using VerifyCS = CSharpSourceGeneratorVerifier<MessagePack.SourceGenerator.MessagePackGenerator>;

public class AyaA9Attack
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaA9Attack(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task HandWrittenPartialFormatterWithoutAttributeDoesNotTriggerGenerator()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            [MessagePackObject]
            public class MyType
            {
                [Key(0)]
                [MessagePackFormatter(typeof(MyFormatter))]
                public int Id { get; set; }
            }

            public partial class MyFormatter : IMessagePackFormatter<int>
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
    public async Task GeneratedFormatterWithInterfaceOnSecondPartialDeclaration()
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
    public async Task GeneratedFormatterWithExplicitNullSurrogateUsesContractlessMode()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = null)]
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
    public async Task GeneratedFormatterNestedInPartialClass()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
            }

            public partial class Outer
            {
                [GeneratedMessagePackFormatter]
                public partial class MyFormatter : IMessagePackFormatter<MyType>
                {
                }
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
    public async Task GeneratedFormatterAndMessagePackObjectInSameCompilation()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            [MessagePackObject]
            public class AnnotatedType
            {
                [Key(0)]
                public int Id { get; set; }
            }

            public class ExternalLikeType
            {
                public string Name { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public partial class ExternalLikeFormatter : IMessagePackFormatter<ExternalLikeType>
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
}
