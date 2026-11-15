// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.Analyzers.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = CSharpCodeFixVerifier<MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer, MessagePack.Analyzers.CodeFixes.MessagePackCodeFixProvider>;

public class AyaK1Attack
{
    private const string Preamble = @"
using MessagePack;
";

    /// <summary>
    /// String key first, then int key on a property: the old code reported MsgPack005 and still went on to
    /// validate the member's type (reporting MsgPack003). The new `continue` skips that validation.
    /// </summary>
    [Fact]
    public async Task StringThenIntKeyPropertyStillHasItsTypeValidated()
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

    {|MsgPack005:[Key(1)]
    public Other {|MsgPack003:Member2|} { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    /// <summary>
    /// Same as above but for a type that is source generated.
    /// </summary>
    [Fact]
    public async Task StringThenIntKeyPropertyStillHasItsTypeValidatedForGeneratedTypes()
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

    {|MsgPack005:[Key(1)]
    public Other {|MsgPack003:Member2|} { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    /// <summary>
    /// A duplicate-keyed member must still have its type validated.
    /// </summary>
    [Fact]
    public async Task DuplicateIntKeyPropertyStillHasItsTypeValidated()
    {
        string input = Preamble + @"
public class Other
{
}

[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    [Key(0)]
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

    /// <summary>
    /// String-then-int key on a plain property reports only MsgPack005.
    /// </summary>
    [Fact]
    public async Task StringThenIntKeyPropertyReportsMixedKeys()
    {
        string input = Preamble + @"
[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    [Key(""a"")]
    public string Member1 { get; set; }

    {|MsgPack005:[Key(1)]
    public string Member2 { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    /// <summary>
    /// Duplicate string keys on fields (the uncovered field path) are reported, without an analyzer failure.
    /// </summary>
    [Fact]
    public async Task DuplicateStringKeysOnFieldsAreReported()
    {
        string input = Preamble + @"
[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    [Key(""a"")]
    public string member1;

    [Key(""a"")]
    public string {|MsgPack005:member2|};
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }
}
