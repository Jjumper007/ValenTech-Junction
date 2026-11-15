// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

/// <summary>
/// Review tests for the change that implements issue 2297 (ExcludeFromCodeCoverage on generated code).
/// Each test asserts what a user should get, so it fails while the reviewed problem is present.
/// </summary>
public class AyaA1Attack
{
    private const string ExcludeAttributeName = "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute";

    private const string DataTypes = """
        using MessagePack;

        [MessagePackObject]
        public class MyObject
        {
            [Key(0)]
            public int Value { get; set; }

            [Key(1)]
            public MyEnum E { get; set; }
        }

        public enum MyEnum
        {
            A,
            B,
        }

        [MessagePackObject(keyAsPropertyName: true)]
        public class MyStringKeyObject
        {
            public string Name { get; set; }
        }

        [Union(0, typeof(MyUnionMember))]
        public interface IMyUnion
        {
        }

        [MessagePackObject]
        public class MyUnionMember : IMyUnion
        {
            [Key(0)]
            public int Value { get; set; }
        }

        """;

    private const string PlainResolver = """

        [GeneratedMessagePackResolver]
        partial class MyResolver
        {
        }

        """;

    private const string PlainCompositeResolver = """

        [MessagePack.CompositeResolver(typeof(MessagePack.Resolvers.NativeGuidResolver))]
        partial class MyComposite
        {
        }

        """;

    /// <summary>
    /// Finding 1: a user who already put [ExcludeFromCodeCoverage] on their own generated resolver
    /// (the obvious workaround for the issue) must still compile.
    /// </summary>
    [Fact]
    public async Task F1a_ResolverThatAlreadyHasTheAttributeStillCompiles()
    {
        string source = DataTypes + """

            [GeneratedMessagePackResolver]
            [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            partial class MyResolver
            {
            }
            """;

        RunResult result = await RunAsync(source);

        AssertNoErrors(result);
    }

    /// <summary>
    /// Finding 1: same situation for a [CompositeResolver] class.
    /// </summary>
    [Fact]
    public async Task F1b_CompositeResolverThatAlreadyHasTheAttributeStillCompiles()
    {
        string source = """
            using MessagePack;
            using MessagePack.Resolvers;

            [CompositeResolver(typeof(NativeGuidResolver))]
            [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            partial class MyComposite
            {
            }
            """;

        RunResult result = await RunAsync(source);

        AssertNoErrors(result);
    }

    /// <summary>
    /// Finding 2: the generator must not put a coverage exclusion on the user's own resolver class,
    /// because it would also exclude the members the user wrote in that class.
    /// </summary>
    [Fact]
    public async Task F2a_UserResolverClassIsNotExcludedFromCoverage()
    {
        string source = DataTypes + """

            [GeneratedMessagePackResolver]
            partial class MyResolver
            {
                public static int UserWrittenLogic(int x) => x > 0 ? x * 2 : -x;
            }
            """;

        RunResult result = await RunAsync(source);

        AssertNoErrors(result);
        INamedTypeSymbol? resolver = result.Compilation.GetTypeByMetadataName("MyResolver");
        Assert.NotNull(resolver);
        Assert.Contains(resolver!.GetMembers(), m => m.Name == "UserWrittenLogic");
        Assert.False(
            IsExcluded(resolver),
            "The user's resolver class (which contains the hand-written UserWrittenLogic) carries [ExcludeFromCodeCoverage] although the user did not write it. Attributes: " + DescribeAttributes(resolver));
    }

    /// <summary>
    /// Finding 2: same for a [CompositeResolver] class with user-written members.
    /// </summary>
    [Fact]
    public async Task F2b_UserCompositeResolverClassIsNotExcludedFromCoverage()
    {
        string source = """
            using MessagePack;
            using MessagePack.Resolvers;

            [CompositeResolver(typeof(NativeGuidResolver))]
            partial class MyComposite
            {
                public static int UserWrittenLogic(int x) => x > 0 ? x * 2 : -x;
            }
            """;

        RunResult result = await RunAsync(source);

        AssertNoErrors(result);
        INamedTypeSymbol? resolver = result.Compilation.GetTypeByMetadataName("MyComposite");
        Assert.NotNull(resolver);
        Assert.Contains(resolver!.GetMembers(), m => m.Name == "UserWrittenLogic");
        Assert.False(
            IsExcluded(resolver),
            "The user's composite resolver class (which contains the hand-written UserWrittenLogic) carries [ExcludeFromCodeCoverage] although the user did not write it. Attributes: " + DescribeAttributes(resolver));
    }

    /// <summary>
    /// Finding 3: the attribute exists on netstandard2.0, so a netstandard2.0 project
    /// (which defines NETSTANDARD2_0) must get its generated types excluded from coverage too.
    /// </summary>
    [Fact]
    public async Task F3a_GeneratedTypesAreExcludedInNetStandard20Projects()
    {
        // The premise: the attribute is available on this target.
        await AssertAttributeIsAvailableAsync(ReferenceAssemblies.NetStandard.NetStandard20);

        RunResult result = await RunAsync(DataTypes + PlainResolver + PlainCompositeResolver, "NETSTANDARD", "NETSTANDARD2_0", "NETSTANDARD2_0_OR_GREATER");

        AssertNoErrors(result);
        AssertAllGeneratedTypesAreExcluded(result);
    }

    /// <summary>
    /// Finding 3: the attribute exists on .NET Framework, so an SDK-style .NET Framework project
    /// (which defines NETFRAMEWORK) must get its generated types excluded from coverage too.
    /// </summary>
    [Fact]
    public async Task F3b_GeneratedTypesAreExcludedInNetFrameworkProjects()
    {
        // The premise: the attribute is available on this target.
        await AssertAttributeIsAvailableAsync(ReferenceAssemblies.NetFramework.Net472.Default);

        RunResult result = await RunAsync(DataTypes + PlainResolver + PlainCompositeResolver, "NETFRAMEWORK", "NET472", "NET472_OR_GREATER");

        AssertNoErrors(result);
        AssertAllGeneratedTypesAreExcluded(result);
    }

    private static void AssertNoErrors(RunResult result)
    {
        string[] errors = result.Compilation.GetDiagnostics()
            .Concat(result.GeneratorDiagnostics)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToArray();
        Assert.True(errors.Length == 0, "The project no longer compiles:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    private static void AssertAllGeneratedTypesAreExcluded(RunResult result)
    {
        // Types that exist only in generated files (formatters and the resolvers' nested helpers).
        // The user's own partial resolver classes are deliberately not part of this set.
        List<INamedTypeSymbol> generatedTypes = AllTypes(result.Compilation.Assembly.GlobalNamespace)
            .Where(t => t.DeclaringSyntaxReferences.Length > 0 && t.DeclaringSyntaxReferences.All(r => !result.UserTrees.Contains(r.SyntaxTree)))
            .ToList();

        // Int-keyed, string-keyed, enum and union formatters, plus the nested helpers of both resolvers.
        Assert.Contains(generatedTypes, t => t.Name == "MyObjectFormatter");
        Assert.Contains(generatedTypes, t => t.Name == "MyStringKeyObjectFormatter");
        Assert.Contains(generatedTypes, t => t.Name == "MyEnumFormatter");
        Assert.Contains(generatedTypes, t => t.Name == "IMyUnionFormatter");
        Assert.Contains(generatedTypes, t => t.Name == "FormatterCache" && t.ContainingType?.Name == "MyResolver");
        Assert.Contains(generatedTypes, t => t.Name == "FormatterCache" && t.ContainingType?.Name == "MyComposite");

        string[] notExcluded = generatedTypes.Where(t => !IsExcluded(t)).Select(t => t.ToDisplayString()).ToArray();
        Assert.True(
            notExcluded.Length == 0,
            $"{notExcluded.Length} of {generatedTypes.Count} generated types do not carry [ExcludeFromCodeCoverage] on this target: " + string.Join(", ", notExcluded));
    }

    private static async Task AssertAttributeIsAvailableAsync(ReferenceAssemblies referenceAssemblies)
    {
        ImmutableArray<MetadataReference> references = await referenceAssemblies.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        string source = """
            [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            internal sealed class SomeFormatter
            {
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                private static class FormatterCache<T>
                {
                }
            }
            """;
        CSharpCompilation compilation = CSharpCompilation.Create(
            "AttributeAvailability",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp7_3)) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        string[] problems = compilation.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(d => d.ToString())
            .ToArray();
        Assert.True(problems.Length == 0, "ExcludeFromCodeCoverage is not usable on this target:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static bool IsExcluded(INamedTypeSymbol type)
        => type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ExcludeAttributeName);

    private static string DescribeAttributes(INamedTypeSymbol type)
        => string.Join(", ", type.GetAttributes().Select(a => a.AttributeClass?.ToDisplayString()));

    private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceOrTypeSymbol container)
    {
        foreach (ISymbol member in container.GetMembers())
        {
            if (member is INamespaceSymbol ns)
            {
                foreach (INamedTypeSymbol type in AllTypes(ns))
                {
                    yield return type;
                }
            }
            else if (member is INamedTypeSymbol namedType)
            {
                yield return namedType;
                foreach (INamedTypeSymbol type in AllTypes(namedType))
                {
                    yield return type;
                }
            }
        }
    }

    private static async Task<RunResult> RunAsync(string source, params string[] preprocessorSymbols)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        IEnumerable<MetadataReference> messagePackReferences = new[] { typeof(MessagePackObjectAttribute).Assembly.Location, typeof(MessagePackSerializer).Assembly.Location }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        // The same parse options are given to the driver so that the generated files see the same preprocessor symbols as the project.
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse, preprocessorSymbols: preprocessorSymbols);
        SyntaxTree userTree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "UserCode.cs");

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AyaA1Attack",
            syntaxTrees: new[] { userTree },
            references: frameworkReferences.Concat(messagePackReferences),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new MessagePackGenerator().AsSourceGenerator(), new CompositeResolverGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> generatorDiagnostics);

        return new RunResult(output, generatorDiagnostics, ImmutableHashSet.Create(userTree));
    }

    private sealed class RunResult
    {
        internal RunResult(Compilation compilation, ImmutableArray<Diagnostic> generatorDiagnostics, ImmutableHashSet<SyntaxTree> userTrees)
        {
            this.Compilation = compilation;
            this.GeneratorDiagnostics = generatorDiagnostics;
            this.UserTrees = userTrees;
        }

        internal Compilation Compilation { get; }

        internal ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }

        internal ImmutableHashSet<SyntaxTree> UserTrees { get; }
    }
}
