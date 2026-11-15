// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using MessagePack.SourceGenerator.Analyzers;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = CSharpSourceGeneratorVerifier<MessagePack.SourceGenerator.MessagePackGenerator>;

public class AyaA1Attack
{
    private readonly ITestOutputHelper testOutputHelper;

    public AyaA1Attack(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void SupportedDiagnostics_AllDescriptorsHaveUniqueIds()
    {
        var analyzer = new MsgPack00xMessagePackAnalyzer();
        var supported = analyzer.SupportedDiagnostics;

        Assert.False(supported.IsDefaultOrEmpty);
        Assert.All(supported, d => Assert.NotNull(d));
        Assert.All(supported, d => Assert.False(string.IsNullOrWhiteSpace(d.Id)));
        Assert.Equal(supported.Length, supported.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SupportedDiagnostics_AllDescriptorsHaveValidMetadataAndHelpLinks()
    {
        var analyzer = new MsgPack00xMessagePackAnalyzer();

        foreach (DiagnosticDescriptor descriptor in analyzer.SupportedDiagnostics)
        {
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Title.ToString()), $"Missing title for {descriptor.Id}");
            Assert.False(string.IsNullOrWhiteSpace(descriptor.MessageFormat.ToString()), $"Missing message format for {descriptor.Id}");
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Category), $"Missing category for {descriptor.Id}");
            Assert.False(string.IsNullOrWhiteSpace(descriptor.HelpLinkUri), $"Missing help link for {descriptor.Id}");
            Assert.True(Uri.TryCreate(descriptor.HelpLinkUri, UriKind.Absolute, out _), $"Invalid help link URI '{descriptor.HelpLinkUri}' for {descriptor.Id}");
            Assert.Contains(descriptor.Id, descriptor.HelpLinkUri, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SupportedDiagnostics_AllDescriptorsHaveConsistentCategory()
    {
        var analyzer = new MsgPack00xMessagePackAnalyzer();
        var supported = analyzer.SupportedDiagnostics;
        string expectedCategory = supported[0].Category;

        Assert.All(supported, d => Assert.Equal(expectedCategory, d.Category));
    }

    [Fact]
    public async Task MixedIntAndStringKeys_StillReportsMsgPack005()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;

            [MessagePackObject]
            public partial class MyObject
            {
                [Key(0)]
                public int Id { get; set; }

                [Key("Name")]
                public string {|#0:Name|} { get; set; }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
            ExpectedDiagnostics = { DiagnosticResult.CompilerError("MsgPack005").WithLocation(0) },
        }.RunDefaultAsync(this.testOutputHelper);
    }
}
