// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator.Tests;
using VerifyCS = CSharpSourceGeneratorVerifier<MessagePack.SourceGenerator.MessagePackGenerator>;

public class AyaA5Attack
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaA5Attack(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task PartialFormatterWithoutAttributeIsNotGeneratedInto()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
            }

            public partial class MyCustomFormatter : IMessagePackFormatter<MyType>
            {
                public void Serialize(ref MessagePackWriter writer, MyType value, MessagePackSerializerOptions options)
                {
                    writer.Write(value.Id);
                }

                public MyType Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
                {
                    return new MyType { Id = reader.ReadInt32() };
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
    public async Task PartialCustomFormatterUsedViaMessagePackFormatterAttributeIsNotGeneratedInto()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            [MessagePackObject]
            [MessagePackFormatter(typeof(MyCustomFormatter))]
            public class MyType
            {
                [Key(0)]
                public int Id { get; set; }
            }

            public partial class MyCustomFormatter : IMessagePackFormatter<MyType>
            {
                public void Serialize(ref MessagePackWriter writer, MyType value, MessagePackSerializerOptions options)
                {
                    writer.Write(value.Id);
                }

                public MyType Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
                {
                    return new MyType { Id = reader.ReadInt32() };
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
    public async Task GenericPartialMessagePackObjectGeneratesFormatterNormally()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;

            namespace TestNamespace
            {
                [MessagePackObject]
                public partial class GenericContainer<T>
                {
                    [Key(0)]
                    public T Value { get; set; } = default!;
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
    public async Task NestedPartialTypeAndPartialFormatterCoexistWithoutDiagnostics()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            public partial class Outer
            {
                [MessagePackObject]
                public partial class Inner
                {
                    [Key(0)]
                    public int Id { get; set; }
                }

                public partial class InnerFormatter : IMessagePackFormatter<Inner>
                {
                    public void Serialize(ref MessagePackWriter writer, Inner value, MessagePackSerializerOptions options)
                    {
                        writer.Write(value.Id);
                    }

                    public Inner Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
                    {
                        return new Inner { Id = reader.ReadInt32() };
                    }
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
    public async Task GenericPartialFormatterWithoutAttributeIsNotGeneratedInto()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            public class Box<T>
            {
                public T Value { get; set; } = default!;
            }

            public partial class BoxFormatter<T> : IMessagePackFormatter<Box<T>>
            {
                public void Serialize(ref MessagePackWriter writer, Box<T> value, MessagePackSerializerOptions options)
                {
                    writer.WriteNil();
                }

                public Box<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
                {
                    reader.ReadNil();
                    return new Box<T>();
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
    public async Task AbstractPartialFormatterForAbstractTypeWithoutAttributeReportsNoDiagnostics()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            public abstract class BaseType
            {
            }

            public abstract partial class BaseFormatter : IMessagePackFormatter<BaseType>
            {
                public abstract void Serialize(ref MessagePackWriter writer, BaseType value, MessagePackSerializerOptions options);

                public abstract BaseType Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options);
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
