// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = CSharpSourceGeneratorVerifier<MessagePack.SourceGenerator.MessagePackGenerator>;

public class AyaA11Attack
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaA11Attack(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void GeneratedFormatterDataTypeNotSupported_IsEnabledErrorWithExpectedId()
    {
        var descriptor = MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported;

        Assert.Equal("MsgPack021", descriptor.Id);
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault);
    }

    [Fact]
    public void GeneratedFormatterDataTypeNotSupported_HasValidHelpLinkUri()
    {
        var descriptor = MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported;

        Assert.False(string.IsNullOrWhiteSpace(descriptor.HelpLinkUri));
        Assert.True(System.Uri.TryCreate(descriptor.HelpLinkUri, System.UriKind.Absolute, out _));
        Assert.Contains(descriptor.Id, descriptor.HelpLinkUri, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratedFormatterDataTypeNotSupported_IsInSupportedDiagnostics()
    {
        var analyzer = new MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer();
        var descriptor = MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported;

        var matching = analyzer.SupportedDiagnostics.Where(d => d.Id == descriptor.Id).ToArray();
        var single = Assert.Single(matching);
        Assert.Same(descriptor, single);
    }

    [Fact]
    public void GeneratedFormatterDataTypeNotSupported_HasValidTitleCategoryAndMessageFormat()
    {
        var descriptor = MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported;
        var referenceDescriptor = MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer.TypeMustBeMessagePackObject;

        Assert.False(string.IsNullOrWhiteSpace(descriptor.Title.ToString()));
        Assert.Equal(referenceDescriptor.Category, descriptor.Category);

        string formatted = string.Format(descriptor.MessageFormat.ToString(), "MyFormatter", "MyType");
        Assert.Contains("MyFormatter", formatted, System.StringComparison.Ordinal);
        Assert.Contains("MyType", formatted, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task FormattedTypeMustNotBeGeneric()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType<T>
            {
            }

            [GeneratedMessagePackFormatter]
            public partial class {|MsgPack021:MyFormatter|} : IMessagePackFormatter<MyType<int>>
            {
                public void Serialize(ref MessagePackWriter writer, MyType<int> value, MessagePackSerializerOptions options) => throw new System.NotImplementedException();

                public MyType<int> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new System.NotImplementedException();
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
