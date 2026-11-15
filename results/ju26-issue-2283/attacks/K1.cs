// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MessagePack;
using MessagePack.SourceGenerator.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class AyaK1Attack
{
    private static readonly MetadataReference[] BaseReferences = GetBaseReferences();

    private static MetadataReference[] GetBaseReferences()
    {
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        var references = new List<MetadataReference>();
        foreach (var path in trustedAssemblies)
        {
            var fileName = Path.GetFileName(path);
            if (fileName is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "System.Collections.dll" or "netstandard.dll" or "System.Memory.dll")
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        references.Add(MetadataReference.CreateFromFile(typeof(MessagePackObjectAttribute).Assembly.Location));
        return references.ToArray();
    }

    private static CSharpCompilation CreateCompilation(string assemblyName, string source, params MetadataReference[] additionalReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        return CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            BaseReferences.Concat(additionalReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static MetadataReference CompileToPortableExecutableReference(Compilation compilation)
    {
        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);
        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics));
        ms.Position = 0;
        return MetadataReference.CreateFromStream(ms);
    }

    /// <summary>
    /// When an invalid generic type in a referenced assembly (portable executable metadata reference, so Location.None)
    /// is closed and used as a member of a [MessagePackObject] type in the current compilation,
    /// Collect must still report a diagnostic so the user knows why serialization/generation is invalid,
    /// rather than silently dropping the diagnostic because its location is not in a syntax tree of the current compilation.
    /// </summary>
    [Fact]
    public void InvalidGenericTypeInMetadataReference_UsedAsMember_ReportsDiagnostic()
    {
        string libSource = """
using MessagePack;

namespace Lib;

[MessagePackObject]
public class BrokenGeneric<T>
{
    // Missing [Key] or [IgnoreMember] attribute on public property
    public T Value { get; set; }
}
""";

        string appSource = """
using MessagePack;
using Lib;

namespace App;

[MessagePackObject]
public class Holder
{
    [Key(0)]
    public BrokenGeneric<int> Item { get; set; }
}
""";

        var libCompilation = CreateCompilation("Lib", libSource);
        var libRef = CompileToPortableExecutableReference(libCompilation);
        var appCompilation = CreateCompilation("App", appSource, libRef);

        Assert.True(ReferenceSymbols.TryCreate(appCompilation, out var referenceSymbols));
        var holderSymbol = appCompilation.GetTypeByMetadataName("App.Holder");
        Assert.NotNull(holderSymbol);

        var diagnostics = new List<Diagnostic>();
        var options = new AnalyzerOptions();

        TypeCollector.Collect(
            appCompilation,
            options,
            referenceSymbols,
            diagnostics.Add,
            holderSymbol!,
            CancellationToken.None);

        Assert.NotEmpty(diagnostics);
    }

    /// <summary>
    /// Same scenario as above, but using a cross-compilation reference (CompilationReference) where the referenced type
    /// has a Location in the referenced compilation's SyntaxTree (not the current compilation's SyntaxTrees).
    /// Collect must report the diagnostic instead of swallowing it.
    /// </summary>
    [Fact]
    public void CrossCompilationReference_WithBrokenGenericTypeMember_ReportsDiagnostic()
    {
        string libSource = """
using MessagePack;

namespace Lib;

[MessagePackObject]
public class BrokenGeneric<T>
{
    // Missing [Key] attribute
    public T UnkeyedProperty { get; set; }
}
""";

        string appSource = """
using MessagePack;
using Lib;

namespace App;

[MessagePackObject]
public class Container
{
    [Key(0)]
    public BrokenGeneric<string> Field { get; set; }
}
""";

        var libCompilation = CreateCompilation("Lib", libSource);
        var appCompilation = CreateCompilation("App", appSource, libCompilation.ToMetadataReference());

        Assert.True(ReferenceSymbols.TryCreate(appCompilation, out var referenceSymbols));
        var containerSymbol = appCompilation.GetTypeByMetadataName("App.Container");
        Assert.NotNull(containerSymbol);

        var diagnostics = new List<Diagnostic>();
        var options = new AnalyzerOptions();

        TypeCollector.Collect(
            appCompilation,
            options,
            referenceSymbols,
            diagnostics.Add,
            containerSymbol!,
            CancellationToken.None);

        Assert.NotEmpty(diagnostics);
    }

    /// <summary>
    /// When TypeCollector.Collect is invoked directly with a targetType whose declaration is in a referenced compilation
    /// (or metadata) and that targetType has a MessagePack diagnostic (e.g. private/inaccessible or unkeyed member),
    /// the caller-supplied reportAnalyzerDiagnostic callback must receive the diagnostic.
    /// </summary>
    [Fact]
    public void Collect_DirectlyOnBrokenClosedGenericTypeFromReferencedCompilation_ReportsDiagnostic()
    {
        string libSource = """
using MessagePack;

namespace Lib;

[MessagePackObject]
public class InvalidGenericRecord<T>
{
    public T MissingKey { get; set; }
}
""";

        string appSource = """
using Lib;

namespace App;

public class Dummy
{
    public InvalidGenericRecord<int>? Field;
}
""";

        var libCompilation = CreateCompilation("Lib", libSource);
        var appCompilation = CreateCompilation("App", appSource, libCompilation.ToMetadataReference());

        Assert.True(ReferenceSymbols.TryCreate(appCompilation, out var referenceSymbols));
        var dummySymbol = appCompilation.GetTypeByMetadataName("App.Dummy");
        Assert.NotNull(dummySymbol);
        var closedGenericType = dummySymbol!.GetMembers("Field").OfType<IFieldSymbol>().Single().Type;

        var diagnostics = new List<Diagnostic>();
        var options = new AnalyzerOptions();

        TypeCollector.Collect(
            appCompilation,
            options,
            referenceSymbols,
            diagnostics.Add,
            closedGenericType,
            CancellationToken.None);

        Assert.NotEmpty(diagnostics);
    }

    /// <summary>
    /// When a type in the current compilation derives from an invalid generic base class in a referenced assembly
    /// (e.g., base class has an unkeyed public property or colliding key), collecting the derived type in the current
    /// compilation must still report the diagnostic.
    /// </summary>
    [Fact]
    public void DerivedTypeInCurrentCompilation_WithBrokenGenericBaseInMetadataReference_ReportsDiagnostic()
    {
        string libSource = """
using MessagePack;

namespace Lib;

[MessagePackObject]
public class BaseGeneric<T>
{
    [Key(0)]
    public T First { get; set; }

    // Unannotated public property on base type in referenced assembly
    public int MissingKeyOnBase { get; set; }
}
""";

        string appSource = """
using MessagePack;
using Lib;

namespace App;

[MessagePackObject]
public class Derived : BaseGeneric<string>
{
    [Key(1)]
    public string Second { get; set; }
}
""";

        var libCompilation = CreateCompilation("Lib", libSource);
        var libRef = CompileToPortableExecutableReference(libCompilation);
        var appCompilation = CreateCompilation("App", appSource, libRef);

        Assert.True(ReferenceSymbols.TryCreate(appCompilation, out var referenceSymbols));
        var derivedSymbol = appCompilation.GetTypeByMetadataName("App.Derived");
        Assert.NotNull(derivedSymbol);

        var diagnostics = new List<Diagnostic>();
        var options = new AnalyzerOptions();

        TypeCollector.Collect(
            appCompilation,
            options,
            referenceSymbols,
            diagnostics.Add,
            derivedSymbol!,
            CancellationToken.None);

        Assert.NotEmpty(diagnostics);
    }
}
