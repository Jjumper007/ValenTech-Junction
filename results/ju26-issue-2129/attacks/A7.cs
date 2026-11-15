// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator.Tests;
using VerifyCS = CSharpSourceGeneratorVerifier<MessagePack.SourceGenerator.MessagePackGenerator>;

public class AyaA7Attack
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaA7Attack(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task StringKeyMessagePackObject_GeneratesFormatter()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;

            [MessagePackObject]
            public class MyType
            {
                [Key("id")]
                public int Id { get; set; }

                [Key("name")]
                public string Name { get; set; }
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
    public async Task ContractlessNestedGenericMessagePackObject_GeneratesFormatter()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;

            public class Outer
            {
                [MessagePackObject(keyAsPropertyName: true)]
                public class Inner<T>
                {
                    public int Id { get; set; }
                    public T Value { get; set; }
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
    public async Task GeneratedFormatter_WithStringKeySurrogate_GeneratesPartialClass()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }

            public class MySurrogate
            {
                [Key("id")]
                public int Id { get; set; }

                [Key("name")]
                public string Name { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate))]
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
    public async Task GeneratedFormatter_PartialStructWithStringKeySurrogate_GeneratesValidFormatter()
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
                [Key("id")]
                public int Id { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate))]
            public partial struct MyFormatter : IMessagePackFormatter<MyType>
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
    public async Task GeneratedFormatter_PartialRecordWithStringKeySurrogate_GeneratesValidFormatter()
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
                [Key("id")]
                public int Id { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate))]
            public partial record MyFormatter : IMessagePackFormatter<MyType>
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
