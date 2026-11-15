// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.Analyzers.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = CSharpCodeFixVerifier<MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer, MessagePack.Analyzers.CodeFixes.MessagePackCodeFixProvider>;

public class AyaA1Attack
{
    private const string Preamble = @"
using MessagePack;
";

    [Fact]
    public async Task MixedStringThenIntKeyOnPropertyStillReportsMemberTypeMustBeMessagePackObject()
    {
        string input = Preamble + @"
public class Other
{
}

[MessagePackObject]
public partial class Foo
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
    public async Task MixedStringThenIntKeyOnSuppressedObjectPropertyStillReportsMemberTypeMustBeMessagePackObject()
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
    public async Task MixedStringThenIntKeyOnPropertyStillReportsGenericElementTypeMustBeMessagePackObject()
    {
        string input = Preamble + @"
public class Other
{
}

[MessagePackObject]
public partial class Foo
{
    [Key(""a"")]
    public string Member1 { get; set; }

    {|MsgPack005:[Key(0)]
    public System.Collections.Generic.List<Other> {|MsgPack003:Member2|} { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task MixedStringThenIntKeyOnStructPropertyStillReportsMemberTypeMustBeMessagePackObject()
    {
        string input = Preamble + @"
public class Other
{
}

[MessagePackObject]
public partial struct Foo
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
}
