// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using MessagePack;
using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Review tests for the change made for https://github.com/MessagePack-CSharp/MessagePack-CSharp/issues/2234.
/// Every input is a source string; the analyzer and the source generator run in memory.
/// </summary>
public class AyaA1Attack
{
    private const string Preamble = "using MessagePack;\n";

    private const string RunFoo = @"
public static class Probe
{
    public static string Run()
    {
        try
        {
            byte[] bytes = MessagePackSerializer.Serialize(new Foo { A = ""a"", B = ""b"" }, MessagePackSerializerOptions.Standard);
            Foo back = MessagePackSerializer.Deserialize<Foo>(bytes, MessagePackSerializerOptions.Standard);
            return ""A="" + back.A + "";B="" + back.B;
        }
        catch (System.Exception ex)
        {
            return ""THROWS:"" + ex.GetType().Name;
        }
    }
}
";

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp12);

    /// <summary>
    /// Finding 1 (the change does not address the issue): the symptom reported in the issue,
    /// "the analyzer does not work if SuppressSourceGeneration = true", is absent on the base commit too.
    /// This test is expected to pass on both the old and the new code.
    /// </summary>
    [Fact]
    public async Task F1_SuppressedTypeIsAlreadyAnalyzedOnOldCode()
    {
        string source = Preamble + @"
public class Other
{
}

[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    public string NoKey { get; set; }

    [Key(0)]
    public Other Unattributed { get; set; }

    [Key(1)]
    private string hidden;
}
";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("AyaA1F1", source);
        string all = Describe(diagnostics);

        Assert.False(diagnostics.Any(d => d.Id == "AD0001"), "The analyzer threw: " + all);
        Assert.True(diagnostics.Any(d => d.Id == "MsgPack004"), "MsgPack004 (member needs a key) expected on a suppressed type. Got: " + all);
        Assert.True(diagnostics.Any(d => d.Id == "MsgPack003"), "MsgPack003 (member type is not a MessagePackObject) expected on a suppressed type. Got: " + all);
        Assert.True(diagnostics.Any(d => d.Id == "MsgPack015"), "MsgPack015 (AllowPrivate required) expected on a suppressed type. Got: " + all);
    }

    /// <summary>
    /// Finding 2: a type with duplicate keys must never serialize with a member silently missing.
    /// Either the generator refuses the type (and the run-time resolver then throws) or every member survives.
    /// </summary>
    [Fact]
    public void F2a_DuplicateKeysDoNotSilentlyDropAMember()
    {
        AssertValidTypeRoundTrips("AyaA1F2aControl");

        string source = Preamble + @"
[MessagePackObject]
public class Foo
{
    [Key(0)]
    public string A { get; set; }

    [Key(0)]
    public string B { get; set; }
}
" + RunFoo;

        RunResult result = GenerateCompileAndRun("AyaA1F2a", source, "Probe");

        Assert.True(
            result.Outcome.StartsWith("THROWS:", StringComparison.Ordinal) || result.Outcome == "A=a;B=b",
            "Serializing { A = \"a\", B = \"b\" } must throw or keep both members, but member data was silently lost: " + result);
    }

    /// <summary>
    /// Finding 2: the same for a type that mixes an int key and a string key.
    /// </summary>
    [Fact]
    public void F2b_MixedKeysDoNotSilentlyDropAMember()
    {
        AssertValidTypeRoundTrips("AyaA1F2bControl");

        string source = Preamble + @"
[MessagePackObject]
public class Foo
{
    [Key(0)]
    public string A { get; set; }

    [Key(""b"")]
    public string B { get; set; }
}
" + RunFoo;

        RunResult result = GenerateCompileAndRun("AyaA1F2b", source, "Probe");

        Assert.True(
            result.Outcome.StartsWith("THROWS:", StringComparison.Ordinal) || result.Outcome == "A=a;B=b",
            "Serializing { A = \"a\", B = \"b\" } must throw or keep both members, but member data was silently lost: " + result);
    }

    /// <summary>
    /// Finding 2: with a deserializing constructor, a duplicate key must produce the key error only:
    /// no misleading constructor error and no generated code that fails to compile.
    /// </summary>
    [Fact]
    public async Task F2c_DuplicateKeyWithConstructorGivesNoConstructorErrorAndNoBrokenGeneratedCode()
    {
        string source = Preamble + @"
[MessagePackObject]
public class Foo
{
    [Key(0)]
    public string A { get; }

    [Key(0)]
    public string B { get; }

    public Foo(string a, string b)
    {
        this.A = a;
        this.B = b;
    }
}
";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("AyaA1F2c", source);
        string all = Describe(diagnostics);
        Assert.True(diagnostics.Any(d => d.Id == "MsgPack005"), "MsgPack005 (keys must be unique) expected. Got: " + all);
        Assert.False(
            diagnostics.Any(d => d.Id == "MsgPack007"),
            "The type has a usable public constructor; MsgPack007 is a misleading follow-on error. Got: " + all);

        GeneratorRun run = RunGenerator("AyaA1F2c", source);
        Diagnostic[] compileErrors = run.Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(
            compileErrors.Length == 0,
            "The generator must not add code that fails to compile. Errors: " + Describe(compileErrors) + " Generated: " + string.Join(", ", run.HintNames));
    }

    /// <summary>
    /// Finding 3: a property with the wrong kind of key still has its type checked (MsgPack003),
    /// as it did before the change, when the first key is a string and the later one an int.
    /// </summary>
    [Fact]
    public async Task F3_MixedKeyMemberTypeIsStillChecked()
    {
        string source = Preamble + @"
public class Other
{
}

[MessagePackObject]
public class Foo
{
    [Key(""a"")]
    public string A { get; set; }

    [Key(1)]
    public Other B { get; set; }
}
";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("AyaA1F3", source);
        string all = Describe(diagnostics);

        Assert.False(diagnostics.Any(d => d.Id == "AD0001"), "The analyzer threw: " + all);
        Assert.True(diagnostics.Any(d => d.Id == "MsgPack005"), "MsgPack005 (do not mix key kinds) expected on Foo.B. Got: " + all);
        Assert.True(diagnostics.Any(d => d.Id == "MsgPack003"), "MsgPack003 (Other is not a MessagePackObject) expected on Foo.B. Got: " + all);
    }

    /// <summary>
    /// Finding 4: a field whose key is null must give the key error without crashing the analyzer or the generator.
    /// </summary>
    [Fact]
    public async Task F4_NullKeyOnFieldDoesNotCrashAnalyzerOrGenerator()
    {
        string source = Preamble + @"
[MessagePackObject]
public class Foo
{
    [Key(null)]
    public string A;
}
";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("AyaA1F4", source);
        string all = Describe(diagnostics);
        Assert.True(diagnostics.Any(d => d.Id == "MsgPack005"), "MsgPack005 (key is null) expected. Got: " + all);
        Assert.False(diagnostics.Any(d => d.Id == "AD0001"), "The analyzer threw: " + all);

        GeneratorRun run = RunGenerator("AyaA1F4", source);
        Assert.False(run.GeneratorDiagnostics.Any(d => d.Id == "CS8785"), "The generator threw: " + Describe(run.GeneratorDiagnostics));
    }

    /// <summary>
    /// Finding 6: the README added by the change lists MsgPack012 among the checks a suppressed type must pass
    /// "because they are serialized at run time". A private nested suppressed type does serialize at run time,
    /// so the analyzer should not reject it with MsgPack012.
    /// </summary>
    [Fact]
    public async Task F6_SuppressedPrivateNestedTypeIsNotRejected()
    {
        string source = Preamble + @"
public class Outer
{
    [MessagePackObject(AllowPrivate = true, SuppressSourceGeneration = true)]
    private class Hidden
    {
        [Key(0)]
        public string A { get; set; }
    }

    public static string Run()
    {
        try
        {
            byte[] bytes = MessagePackSerializer.Serialize(new Hidden { A = ""a"" }, MessagePackSerializerOptions.Standard);
            Hidden back = MessagePackSerializer.Deserialize<Hidden>(bytes, MessagePackSerializerOptions.Standard);
            return ""A="" + back.A;
        }
        catch (System.Exception ex)
        {
            return ""THROWS:"" + ex.GetType().Name;
        }
    }
}
";

        // The type works at run time without a generated formatter.
        RunResult result = GenerateCompileAndRun("AyaA1F6", source, "Outer");
        Assert.Equal("A=a", result.Outcome);
        Assert.Equal(0, result.GeneratedFormatters);

        // So the analyzer has no reason to report it as inaccessible.
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("AyaA1F6", source);
        Assert.False(
            diagnostics.Any(d => d.Id == "MsgPack012"),
            "A suppressed type that serializes correctly at run time was rejected. Got: " + Describe(diagnostics));
    }

    private static void AssertValidTypeRoundTrips(string assemblyName)
    {
        string source = Preamble + @"
[MessagePackObject]
public class Foo
{
    [Key(0)]
    public string A { get; set; }

    [Key(1)]
    public string B { get; set; }
}
" + RunFoo;

        RunResult result = GenerateCompileAndRun(assemblyName, source, "Probe");
        Assert.Equal("A=a;B=b", result.Outcome);
        Assert.True(result.GeneratedFormatters > 0, "Control: a formatter is expected for a valid type. " + result);
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        string frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        foreach (string path in trusted.Split(Path.PathSeparator))
        {
            if (path.Length > 0 && string.Equals(Path.GetDirectoryName(path), frameworkDirectory, StringComparison.OrdinalIgnoreCase))
            {
                yield return MetadataReference.CreateFromFile(path);
            }
        }

        yield return MetadataReference.CreateFromFile(typeof(MessagePackObjectAttribute).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(MessagePackSerializer).Assembly.Location);
    }

    private static CSharpCompilation CreateCompilation(string assemblyName, string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, ParseOptions, path: assemblyName + ".cs");
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { tree },
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Diagnostic[] inputErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(inputErrors.Length == 0, "The test input itself must compile. Errors: " + Describe(inputErrors));
        return compilation;
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string assemblyName, string source)
    {
        CSharpCompilation compilation = CreateCompilation(assemblyName, source);
        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MsgPack00xMessagePackAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static GeneratorRun RunGenerator(string assemblyName, string source)
    {
        CSharpCompilation compilation = CreateCompilation(assemblyName, source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new MessagePackGenerator().AsSourceGenerator() }, parseOptions: ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> generatorDiagnostics);
        string[] hintNames = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        return new GeneratorRun(output, generatorDiagnostics, hintNames);
    }

    private static RunResult GenerateCompileAndRun(string assemblyName, string source, string entryType)
    {
        GeneratorRun run = RunGenerator(assemblyName, source);

        using MemoryStream stream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult emitResult = run.Output.Emit(stream);
        Assert.True(
            emitResult.Success,
            "The compilation with the generated code must build. Errors: " + Describe(emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        Assembly assembly = Assembly.Load(stream.ToArray());
        MethodInfo entry = assembly.GetType(entryType, throwOnError: true)!.GetMethod("Run")!;
        string outcome = (string)entry.Invoke(null, null)!;

        int generatedFormatters = run.HintNames.Count(n => n.Contains("Formatter", StringComparison.Ordinal));
        bool generatorFailed = run.GeneratorDiagnostics.Any(d => d.Id == "CS8785");
        return new RunResult(outcome, generatedFormatters, generatorFailed);
    }

    private static string Describe(IEnumerable<Diagnostic> diagnostics)
    {
        string[] lines = diagnostics
            .Select(d => d.Id + ": " + d.GetMessage().Split('\n')[0].Trim())
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToArray();
        return lines.Length == 0 ? "(none)" : "[" + string.Join(" | ", lines) + "]";
    }

    private sealed record GeneratorRun(Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics, string[] HintNames);

    private sealed record RunResult(string Outcome, int GeneratedFormatters, bool GeneratorFailed)
    {
        public override string ToString() => $"outcome '{this.Outcome}', generated formatters: {this.GeneratedFormatters}, generator failed: {this.GeneratorFailed}";
    }
}
