// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator.Tests;
using VerifyCS = CSharpSourceGeneratorVerifier<MessagePack.SourceGenerator.MessagePackGenerator>;

public class AyaR5Tests
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaR5Tests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    private const string Members = """
            public void Serialize(ref MessagePackWriter writer, MyType value, MessagePackSerializerOptions options) => throw new System.NotImplementedException();

            public MyType Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new System.NotImplementedException();
        """;

    [Fact]
    public async Task SurrogateMatchesPublicReadWriteFieldOnFormattedType()
    {
        string testSource = /* lang=c#-test */ $$"""
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id;
            }

            public class MySurrogate
            {
                [Key(0)]
                public int Id { get; set; }

                [Key(1)]
                public int {|MsgPack022:Missing|} { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate))]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            {{Members}}
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
    public async Task SurrogateRejectsReadOnlyFieldOnFormattedType()
    {
        string testSource = /* lang=c#-test */ $$"""
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public readonly int ReadOnlyId = 1;
            }

            public class MySurrogate
            {
                [Key(0)]
                public int {|MsgPack022:ReadOnlyId|} { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate))]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            {{Members}}
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
    public async Task SurrogateRejectsConstFieldOnFormattedType()
    {
        string testSource = /* lang=c#-test */ $$"""
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public const int ConstId = 2;
            }

            public class MySurrogate
            {
                [Key(0)]
                public int {|MsgPack022:ConstId|} { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate))]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            {{Members}}
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
    public async Task SurrogateIgnoresNonPropertyNonFieldMemberOnFormattedType()
    {
        string testSource = /* lang=c#-test */ $$"""
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public void SomeMethod() { }
            }

            public class MySurrogate
            {
                [Key(0)]
                public int {|MsgPack022:SomeMethod|} { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate))]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            {{Members}}
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
