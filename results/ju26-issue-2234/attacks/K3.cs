// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;
using VerifyCS = CSharpCodeFixVerifier<MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer, MessagePack.Analyzers.CodeFixes.MessagePackCodeFixProvider>;

public class AyaK3Attack
{
    private const string Preamble = @"
using MessagePack;
";

    [Fact]
    public async Task MixedIntKeyPropertyAfterStringKeyWithUnserializableTypeReportsBothDiagnostics()
    {
        string input = Preamble + @"
public class Other
{
}

[MessagePackObject]
public partial class Foo
{
    [Key(""a"")]
    public string Prop1 { get; set; }

    {|MsgPack005:[Key(0)]
    public Other {|MsgPack003:Prop2|} { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task MixedIntKeyFieldAfterStringKeyWithUnserializableTypeReportsBothDiagnostics()
    {
        string input = Preamble + @"
public class Other
{
}

[MessagePackObject]
public partial class Foo
{
    [Key(""a"")]
    public string Field1;

    {|MsgPack005:[Key(0)]
    public Other {|MsgPack003:Field2|};|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task DuplicateIntKeyPropertyWithUnserializableTypeReportsBothDiagnostics()
    {
        string input = Preamble + @"
public class Other
{
}

[MessagePackObject]
public partial class Foo
{
    [Key(0)]
    public string Prop1 { get; set; }

    {|MsgPack005:[Key(0)]
    public Other {|MsgPack003:Prop2|} { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task DuplicateStringKeyFieldReportsKeysMustBeUniqueWithoutAnalyzerFailure()
    {
        string input = Preamble + @"
[MessagePackObject]
public partial class Foo
{
    [Key(""a"")]
    public string Field1;

    {|MsgPack005:[Key(""a"")]
    public string Field2;|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }
}
