// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.Analyzers.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = CSharpCodeFixVerifier<MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer, MessagePack.Analyzers.CodeFixes.MessagePackCodeFixProvider>;

public class AyaK2Attack
{
    private const string Preamble = @"
using MessagePack;
";

    [Fact]
    public async Task MixedKeyKindPropertyStillReportsUnserializableType_Generated()
    {
        string input = Preamble + @"
public class Other
{
}

[MessagePackObject]
public class Foo
{
    [Key(""a"")]
    public string Member1 { get; set; }

    {|MsgPack005:[Key(0)]
    public Other {|MsgPack003:Member2|} { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task MixedKeyKindPropertyStillReportsUnserializableType_Suppressed()
    {
        string input = Preamble + @"
public class Other
{
}

[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    [Key(""a"")]
    public string Member1 { get; set; }

    {|MsgPack005:[Key(0)]
    public Other {|MsgPack003:Member2|} { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task MixedKeyKindFieldStillReportsUnserializableType_Generated()
    {
        string input = Preamble + @"
public class Other
{
}

[MessagePackObject]
public class Foo
{
    [Key(""a"")]
    public string Field1;

    [Key(0)]
    public Other {|MsgPack005:{|MsgPack003:Field2|}|};
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task DuplicateStringKeysOnFieldsAreReportedWithoutAnalyzerFailure()
    {
        string input = Preamble + @"
[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    [Key(""a"")]
    public string Field1;

    [Key(""a"")]
    public string {|MsgPack005:Field2|};
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }
}
