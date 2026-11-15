// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using MessagePack.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Xunit;
using VerifyCS = CSharpSourceGeneratorVerifier<MessagePack.SourceGenerator.MessagePackGenerator>;

public class AyaA12Attack
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaA12Attack(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void GeneratedFormatterMustBePartial_IsEnabledByDefaultWithErrorSeverity()
    {
        DiagnosticDescriptor descriptor = MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial;

        Assert.Equal("MsgPack019", descriptor.Id);
        Assert.True(descriptor.IsEnabledByDefault);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.Equal(MsgPack00xMessagePackAnalyzer.TypeMustBeMessagePackObject.Category, descriptor.Category);
    }

    [Fact]
    public void GeneratedFormatterMustBePartial_HasValidHelpLinkUri()
    {
        DiagnosticDescriptor descriptor = MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial;
        DiagnosticDescriptor baseline = MsgPack00xMessagePackAnalyzer.TypeMustBeMessagePackObject;

        Assert.False(string.IsNullOrWhiteSpace(descriptor.HelpLinkUri));
        Assert.Contains(descriptor.Id, descriptor.HelpLinkUri, StringComparison.Ordinal);
        Assert.True(Uri.TryCreate(descriptor.HelpLinkUri, UriKind.Absolute, out _));
        Assert.Equal(baseline.HelpLinkUri.Replace(baseline.Id, descriptor.Id), descriptor.HelpLinkUri);
    }

    [Fact]
    public void GeneratedFormatterMustBePartial_IsInSupportedDiagnosticsWithoutDuplicateIds()
    {
        var analyzer = new MsgPack00xMessagePackAnalyzer();

        Assert.Contains(MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial, analyzer.SupportedDiagnostics);
        Assert.Equal(
            analyzer.SupportedDiagnostics.Length,
            analyzer.SupportedDiagnostics.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GeneratedFormatterMustBePartial_HasNonEmptyTitleAndFormatsFormatterName()
    {
        DiagnosticDescriptor descriptor = MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial;

        Assert.False(string.IsNullOrWhiteSpace(descriptor.Title.ToString()));

        string formatted = string.Format(descriptor.MessageFormat.ToString(), "MyFormatter");
        Assert.Contains("MyFormatter", formatted, StringComparison.Ordinal);
        Assert.Contains("partial", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GeneratedFormatterMustBePartial_ReportsErrorForNonPartialFormatterInNamespace()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;
            using MessagePack.Formatters;

            namespace SampleNamespace
            {
                public class MyType
                {
                    public int Id { get; set; }
                }

                [GeneratedMessagePackFormatter]
                public class {|MsgPack019:MyFormatter|} : IMessagePackFormatter<MyType>
                {
                    public void Serialize(ref MessagePackWriter writer, MyType value, MessagePackSerializerOptions options) => throw new System.NotImplementedException();

                    public MyType Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new System.NotImplementedException();
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
}
