// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator.Tests;
using VerifyCS = CSharpSourceGeneratorVerifier<MessagePack.SourceGenerator.MessagePackGenerator>;

public class AyaA3Attack
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaA3Attack(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task StandardMessagePackObjectFormatterCompiles()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;

            namespace TestNamespace
            {
                [MessagePackObject]
                public class MyType
                {
                    [Key(0)]
                    public int Id { get; set; }
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
    public async Task GenericMessagePackObjectWithConstraintsCompiles()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;

            namespace TestNamespace
            {
                [MessagePackObject]
                public class MyGenericType<T> where T : class
                {
                    [Key(0)]
                    public T? Value { get; set; }
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
    public async Task UserDeclaredSealedPartialClassFormatterCompiles()
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
            public sealed partial class MyFormatter : IMessagePackFormatter<MyType>
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
    public async Task UserDeclaredPartialStructFormatterCompiles()
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
    public async Task UserDeclaredPartialRecordFormatterCompiles()
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
