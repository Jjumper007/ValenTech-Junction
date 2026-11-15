// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator.Tests;
using VerifyCS = CSharpSourceGeneratorVerifier<MessagePack.SourceGenerator.MessagePackGenerator>;

public class AyaR1Tests
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaR1Tests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    private const string Members = """
            public void Serialize(ref MessagePackWriter writer, MyType value, MessagePackSerializerOptions options) => throw new System.NotImplementedException();

            public MyType Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new System.NotImplementedException();
        """;

    [Fact]
    public async Task IndexerOnFormattedTypeIsIgnored()
    {
        string testSource = /* lang=c#-test */ $$"""
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }

                public int this[int index]
                {
                    get => 0;
                    set { }
                }
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
}
