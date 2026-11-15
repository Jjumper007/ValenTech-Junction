// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.Analyzers.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = CSharpCodeFixVerifier<MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer, MessagePack.Analyzers.CodeFixes.MessagePackCodeFixProvider>;

public class AyaR1Tests
{
    private const string Preamble = @"
using MessagePack;
";

    [Fact]
    public async Task MixedFieldKeyKindsAreReported()
    {
        string input = Preamble + @"
[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    [Key(0)]
    public string Field1;

    [Key(""Field2"")]
    public string {|MsgPack005:Field2|};
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task DuplicateFieldStringKeysAreReported()
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

    [Fact]
    public async Task DistinctFieldStringKeysAreAccepted()
    {
        string input = Preamble + @"
[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    [Key(""a"")]
    public string Field1;

    [Key(""b"")]
    public string Field2;
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
        }.RunAsync();
    }
}
