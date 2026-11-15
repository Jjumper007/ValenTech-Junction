// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using MessagePack;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaA2Attack
{
    private static ImmutableArray<Diagnostic> RunGenerator(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

        MetadataReference[] references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Concat(new[]
            {
                typeof(object).Assembly.Location,
                typeof(MessagePackObjectAttribute).Assembly.Location,
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();

        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Diagnostic[] compileErrors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(compileErrors);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> diagnostics);
        return diagnostics;
    }

    [Fact]
    public void PropertyWithNullStringKey_ReportsDiagnosticAtPropertyIdentifier()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public class MyObject
    {
        [Key((string)null)]
        public int MyProperty { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class MyResolver { }
}
""";

        ImmutableArray<Diagnostic> diagnostics = RunGenerator(source);

        Diagnostic diag = Assert.Single(diagnostics, d => d.GetMessage().Contains("MyProperty", StringComparison.Ordinal));
        Assert.True(diag.Location.IsInSource);
        string spanText = diag.Location.SourceTree!.GetText().ToString(diag.Location.SourceSpan);
        Assert.Equal("MyProperty", spanText);
    }

    [Fact]
    public void GenericObjectPropertyWithNullStringKey_ReportsDiagnosticAtPropertyIdentifier()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public class GenericObject<T>
    {
        [Key((string)null)]
        public T Value { get; set; }
    }

    [MessagePackObject]
    public class Holder
    {
        [Key(0)]
        public GenericObject<int> Item { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class MyResolver { }
}
""";

        ImmutableArray<Diagnostic> diagnostics = RunGenerator(source);

        Diagnostic diag = Assert.Single(diagnostics, d => d.GetMessage().Contains("Value", StringComparison.Ordinal));
        Assert.True(diag.Location.IsInSource);
        string spanText = diag.Location.SourceTree!.GetText().ToString(diag.Location.SourceSpan);
        Assert.Equal("Value", spanText);
    }

    [Fact]
    public void PublicPropertyMissingKey_ReportsDiagnosticAtPropertyDeclaration()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public class MyObject
    {
        [Key(0)]
        public int Id { get; set; }

        public string MissingKeyProp { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class MyResolver { }
}
""";

        ImmutableArray<Diagnostic> diagnostics = RunGenerator(source);

        Diagnostic diag = Assert.Single(diagnostics, d => d.GetMessage().Contains("MissingKeyProp", StringComparison.Ordinal));
        Assert.True(diag.Location.IsInSource);
        string spanText = diag.Location.SourceTree!.GetText().ToString(diag.Location.SourceSpan);
        Assert.Contains("MissingKeyProp", spanText, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicFieldMissingKey_ReportsDiagnosticAtFieldDeclarator()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public class MyObject
    {
        [Key(0)]
        public int Id;

        public string MissingKeyField;
    }

    [GeneratedMessagePackResolver]
    partial class MyResolver { }
}
""";

        ImmutableArray<Diagnostic> diagnostics = RunGenerator(source);

        Diagnostic diag = Assert.Single(diagnostics, d => d.GetMessage().Contains("MissingKeyField", StringComparison.Ordinal));
        Assert.True(diag.Location.IsInSource);
        string spanText = diag.Location.SourceTree!.GetText().ToString(diag.Location.SourceSpan);
        Assert.Contains("MissingKeyField", spanText, StringComparison.Ordinal);
    }

    [Fact]
    public void NonPublicPropertyMissingKey_WhenNonPublicMemberIsSerialized_ReportsDiagnosticInSource()
    {
        const string source = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public class MyObject
    {
        [Key(0)]
        internal int InternalWithKey { get; set; }

        internal string InternalWithoutKey { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class MyResolver { }
}
""";

        ImmutableArray<Diagnostic> diagnostics = RunGenerator(source);

        Diagnostic diag = Assert.Single(diagnostics, d => d.GetMessage().Contains("InternalWithoutKey", StringComparison.Ordinal));
        Assert.True(diag.Location.IsInSource);
        string spanText = diag.Location.SourceTree!.GetText().ToString(diag.Location.SourceSpan);
        Assert.Contains("InternalWithoutKey", spanText, StringComparison.Ordinal);
    }
}
