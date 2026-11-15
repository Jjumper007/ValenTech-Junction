using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aya;

/// <summary>One mechanical way of altering an input, and how many inputs it produced.</summary>
public sealed record SweepAlteration(string Label, bool FromChange, int Inputs);

/// <summary>An input of the sweep: where it comes from and, when it was altered, how and from which one.</summary>
public sealed record SweepInput(string Id, string Name, string Text, string Origin, string? Alteration, string? BasedOn);

/// <summary>
/// What the sweep did. Its inputs are not chosen by a model: they are the inputs the existing tests already hold,
/// as they are and altered mechanically, some of the alterations taken from what the change itself adds. The
/// comparison it produced is listed with the others, under the purpose "sweep".
/// </summary>
public sealed class SweepResult
{
    public string? TestProject { get; set; }

    /// <summary>Inputs found in the existing tests of that project.</summary>
    public int Found { get; set; }

    public int FromTests { get; set; }

    public int Altered { get; set; }

    public List<SweepAlteration> Alterations { get; set; } = new();

    /// <summary>compared, or why it was not.</summary>
    public string Status { get; set; } = "not-run";

    public string Detail { get; set; } = string.Empty;

    public int ChangedLines { get; set; }

    /// <summary>How many of the change's executable lines the inputs drove, measured on the new code.</summary>
    public int ChangedLinesExecuted { get; set; }

    /// <summary>Differences that appear only once an input carries an alteration: its unaltered original does not show them.</summary>
    public int OnlyWhenAltered { get; set; }

    public string? InputsFile { get; set; }

    /// <summary>What the sweep saw that is not a difference between two results: a configuration with no effect, crashes on both versions.</summary>
    public List<string> Notes { get; set; } = new();

    /// <summary>Inputs that came with the change, run on the new code alone because the old code has nothing to say about them.</summary>
    public int NewInputs { get; set; }

    public List<SweepFinding> NewFindings { get; set; } = new();
}

/// <summary>
/// An input on which the new code breaks a rule that holds for any input. crash: the code under test fell over.
/// silently-broken: what it produced does not compile and it reported no error of its own.
/// </summary>
public sealed record SweepFinding(string Input, string Kind, string Line, string Origin);

/// <summary>
/// Builds the inputs of a sweep without a model. This version knows one kind of input: C# source held in the
/// string literals of the tests, which is what code that reads source code (a generator, an analyzer) is tested
/// with. Code that takes numbers or objects would need another builder.
/// </summary>
public static partial class SweepInputs
{
    private const int MostAttributes = 3;

    /// <summary>The C# sources the tests under a folder hold in their string literals, in a stable order, without repeats.</summary>
    public static List<(string Origin, string Text)> Corpus(string folder)
    {
        var found = new List<(string Origin, string Text)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(folder))
        {
            return found;
        }

        var files = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p is "obj" or "bin" or "AyaCandidates"))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            foreach (var token in tree.GetRoot().DescendantTokens())
            {
                if (!token.IsKind(SyntaxKind.StringLiteralToken) && !token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) && !token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken))
                {
                    continue;
                }

                // Tests of an analyzer mark in their source where a diagnostic is expected ({|Id:text|}, [|text|]).
                // Without the marks it is the source a user would write, and it is the one with the defects in it.
                var raw = token.ValueText.Replace("\r", string.Empty);
                var text = Markup().Replace(raw, string.Empty);
                if (text.Length >= 40 && IsSource(text) && seen.Add(string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))))
                {
                    found.Add(($"{Path.GetFileName(file)}:{tree.GetLineSpan(token.Span).StartLinePosition.Line + 1}" + (text.Length == raw.Length ? string.Empty : ", marks removed"), text));
                }
            }
        }

        return found;
    }

    /// <summary>Whether a text is a C# source file that parses and declares at least one type.</summary>
    public static bool IsSource(string text)
    {
        if (!text.Contains("class ", StringComparison.Ordinal) && !text.Contains("struct ", StringComparison.Ordinal) &&
            !text.Contains("record ", StringComparison.Ordinal) && !text.Contains("interface ", StringComparison.Ordinal))
        {
            return false;
        }

        var tree = CSharpSyntaxTree.ParseText(text);
        return !tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error)
            && tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().Any();
    }

    /// <summary>
    /// The attributes the change makes the code write out, most frequent first: those in a template, or inside a
    /// string. An attribute the change merely uses on its own declarations says nothing about what users will
    /// receive, and is left out. So is one the change itself declares: the old code does not have it, so an
    /// input carrying it could not have worked before.
    /// </summary>
    public static List<string> Attributes(IEnumerable<string> addedLines, IReadOnlyCollection<string> introduced, IReadOnlySet<string>? templateLines = null)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in addedLines)
        {
            var template = templateLines?.Contains(line) == true;
            foreach (Match m in AttributeUse().Matches(line))
            {
                var name = m.Groups[1].Value;
                var last = name.Split('.', ':').Last();
                var quoted = line[..m.Index].Count(ch => ch == '"') % 2 == 1;
                if (last.Length < 3 || !char.IsUpper(last[0]) || introduced.Contains(last) || introduced.Contains(last + "Attribute") || !(template || quoted))
                {
                    continue;
                }

                var text = "[" + name + m.Groups[2].Value + "]";
                counts[text] = counts.GetValueOrDefault(text) + 1;
            }
        }

        return counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).Take(MostAttributes).ToList();
    }

    /// <summary>The alterations to apply: first the ones taken from the change, then the fixed ones.</summary>
    public static List<(string Label, bool FromChange, Func<string, string?> Apply)> Alterations(IEnumerable<string> attributes, IEnumerable<string>? symbols = null)
    {
        var list = new List<(string, bool, Func<string, string?>)>();
        foreach (var attribute in attributes)
        {
            list.Add(($"{attribute} put on every type, as the change writes it", true, text => WithAttribute(text, attribute)));
        }

        foreach (var symbol in symbols ?? [])
        {
            list.Add((SymbolLabel(symbol), true, text => SymbolsMark + symbol + "\n" + text));
        }

        list.AddRange(Family().Select(f => (f.Label, false, f.Apply)));
        return list;
    }

    /// <summary>
    /// The alterations that do not come from the change. They are not a list of cases: each is one of a few
    /// schemes (take one thing out, swap a thing for another of its kind, alter a literal, move a declaration,
    /// wrap a type) applied to every category of C# syntax it fits. Nothing here knows what the code under test is
    /// for, and none was written for a particular defect; the budget is shared out evenly among them.
    /// </summary>
    public static List<(string Label, Func<string, string?> Apply)> Family()
    {
        var list = new List<(string, Func<string, string?>)>();
        static IEnumerable<MemberDeclarationSyntax> Members(SyntaxNode root) => root.DescendantNodes().OfType<MemberDeclarationSyntax>().Where(m => m is not BaseTypeDeclarationSyntax and not BaseNamespaceDeclarationSyntax);
        static (int, int, string) Out(SyntaxToken t) => (t.SpanStart, t.Span.Length + (t.TrailingTrivia.ToFullString().StartsWith(' ') ? 1 : 0), string.Empty);
        static (int, int, string) Swap(SyntaxToken t, string with) => (t.SpanStart, t.Span.Length, with);
        static IEnumerable<AccessorDeclarationSyntax> Setters(SyntaxNode root) => root.DescendantNodes().OfType<AccessorDeclarationSyntax>().Where(a => a.Keyword.IsKind(SyntaxKind.SetKeyword) && a.Body is null && a.ExpressionBody is null);
        static IEnumerable<TypeSyntax> MemberTypes(SyntaxNode root) =>
            root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Select(p => p.Type)
                .Concat(root.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f => !f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword))).Select(f => f.Declaration.Type));

        // Move a declaration.
        list.Add(("the types other types use moved to a referenced library", Split));
        list.Add(("every type nested in an outer class", Nested));

        // Alter a literal.
        list.Add(("an attribute argument made to repeat its sibling's", text => Argument(text, "repeat")));
        list.Add(("an attribute argument changed to the other kind of literal", text => Argument(text, "kind")));
        list.Add(("an attribute argument made null", text => Argument(text, "null")));

        // Take one thing out: attributes, modifiers, members, base types.
        list.Add(("the attributes removed from every type another type uses", WithoutAttributesOnUsedTypes));
        list.Add(("every attribute removed from every type", text => Edit(text, root => root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().SelectMany(t => t.AttributeLists).Select(l => (l.SpanStart, l.Span.Length, string.Empty)))));
        list.Add(("every attribute removed from every member", text => Edit(text, root => Members(root).SelectMany(m => m.AttributeLists).Select(l => (l.SpanStart, l.Span.Length, string.Empty)))));
        foreach (var (kind, word) in new[]
                 {
                     (SyntaxKind.PartialKeyword, "partial"), (SyntaxKind.SealedKeyword, "sealed"), (SyntaxKind.StaticKeyword, "static"), (SyntaxKind.AbstractKeyword, "abstract"),
                     (SyntaxKind.ReadOnlyKeyword, "readonly"), (SyntaxKind.RequiredKeyword, "required"), (SyntaxKind.VirtualKeyword, "virtual"),
                 })
        {
            list.Add(($"`{word}` removed wherever it appears", text => Edit(text, root => root.DescendantNodes().OfType<MemberDeclarationSyntax>().SelectMany(m => m.Modifiers.Where(x => x.IsKind(kind))).Select(Out))));
        }

        list.Add(("the first member removed from every type", text => Edit(text, root => root.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(t => t.Members.Count > 1).Select(t => (t.Members[0].FullSpan.Start, t.Members[0].FullSpan.Length, string.Empty)))));
        list.Add(("the last member removed from every type", text => Edit(text, root => root.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(t => t.Members.Count > 1).Select(t => (t.Members[^1].FullSpan.Start, t.Members[^1].FullSpan.Length, string.Empty)))));
        list.Add(("the base types removed from every type", text => Edit(text, root => root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(t => t.BaseList is not null).Select(t => (t.BaseList!.SpanStart, t.BaseList.Span.Length, string.Empty)))));
        list.Add(("the setter removed from every property", text => Edit(text, root => Setters(root).Select(a => (a.FullSpan.Start, a.FullSpan.Length, string.Empty)))));

        // Swap a thing for another of its kind: the kind of type, the accessibility, the kind of member, the accessor.
        list.Add(("every class made a record", MadeRecord));
        list.Add(("every class made a struct", text => Edit(text, root => root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c => !c.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.AbstractKeyword))).Select(c => Swap(c.Keyword, "struct")))));
        list.Add(("every struct made a class", text => Edit(text, root => root.DescendantNodes().OfType<StructDeclarationSyntax>().Select(s => Swap(s.Keyword, "class")))));
        list.Add(("every record made a class", text => Edit(text, root => root.DescendantNodes().OfType<RecordDeclarationSyntax>().Where(r => !r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)).Select(r => Swap(r.Keyword, "class")))));
        list.Add(("every `public` type made `internal`", MadeInternal));
        list.Add(("every type that is not `public` made `public`", MadePublic));
        list.Add(("every `public` member made `private`", text => Edit(text, root => Members(root).SelectMany(m => m.Modifiers.Where(x => x.IsKind(SyntaxKind.PublicKeyword))).Select(t => Swap(t, "private")))));
        list.Add(("every `public` member made `internal`", text => Edit(text, root => Members(root).SelectMany(m => m.Modifiers.Where(x => x.IsKind(SyntaxKind.PublicKeyword))).Select(t => Swap(t, "internal")))));
        list.Add(("every `private` member made `public`", text => Edit(text, root => Members(root).SelectMany(m => m.Modifiers.Where(x => x.IsKind(SyntaxKind.PrivateKeyword))).Select(t => Swap(t, "public")))));
        list.Add(("every auto-property made a field", text => Edit(text, root => root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .Where(p => p.AccessorList is not null && p.Initializer is null && p.AccessorList.Accessors.All(a => a.Body is null && a.ExpressionBody is null))
            .Select(p => (p.AccessorList!.SpanStart, p.AccessorList.Span.Length, ";")))));
        list.Add(("every field made an auto-property", text => Edit(text, root => root.DescendantNodes().OfType<FieldDeclarationSyntax>()
            .Where(f => f.Declaration.Variables.Count == 1 && f.Declaration.Variables[0].Initializer is null && !f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword) || m.IsKind(SyntaxKind.ReadOnlyKeyword)))
            .Select(f => Swap(f.SemicolonToken, " { get; set; }")))));
        list.Add(("every setter made `init`", text => Edit(text, root => Setters(root).Select(a => Swap(a.Keyword, "init")))));
        list.Add(("every setter made `private`", text => Edit(text, root => Setters(root).Where(a => a.Modifiers.Count == 0).Select(a => (a.Keyword.SpanStart, 0, "private ")))));

        // Put a modifier on.
        list.Add(("every class that can be made `sealed`", MadeSealed));
        list.Add(("every class that can be made `abstract`", text => Edit(text, root => root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(c => !c.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword) || m.IsKind(SyntaxKind.AbstractKeyword) || m.IsKind(SyntaxKind.StaticKeyword)))
            .Select(c => (c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) ? c.Modifiers.First(m => m.IsKind(SyntaxKind.PartialKeyword)).SpanStart : c.Keyword.SpanStart, 0, "abstract ")))));

        // Wrap the type of a member.
        list.Add(("the type of every member made nullable", text => Edit(text, root => MemberTypes(root).Where(t => t is not NullableTypeSyntax).Select(t => (t.Span.End, 0, "?")))));
        list.Add(("the type of every member made an array", text => Edit(text, root => MemberTypes(root).Select(t => (t.Span.End, 0, "[]")))));
        return list;
    }

    /// <summary>Puts the types of every namespace, or of the file when it has none, inside an outer partial class.</summary>
    public static string? Nested(string text) =>
        Edit(text, root => root.DescendantNodesAndSelf()
            .Where(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .Select(n => n.ChildNodes().OfType<BaseTypeDeclarationSyntax>().ToList())
            .Where(types => types.Count > 0)
            .SelectMany(types => new[] { (types[0].SpanStart, 0, "public partial class AyaOuter {\n"), (types[^1].Span.End, 0, "\n}") }));

    /// <summary>First line of an input that must be compiled with preprocessor symbols defined.</summary>
    public const string SymbolsMark = "// AYA-SYMBOLS: ";

    /// <summary>Line that separates the part of an input compiled as a referenced library from the part that uses it.</summary>
    public const string SplitMark = "// AYA-SPLIT";

    public static string SymbolLabel(string symbol) => $"compiled with {symbol} defined, a symbol the change tests";

    /// <summary>
    /// The preprocessor symbols the change tests in the text it writes out (`#if X`), most frequent first. Under
    /// each of them the code the change emits may be a different code, or none.
    /// </summary>
    public static List<string> Symbols(IEnumerable<string> addedLines, IReadOnlySet<string>? templateLines = null)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in addedLines)
        {
            var template = templateLines?.Contains(line) == true;
            foreach (Match m in Conditional().Matches(line))
            {
                if (!template && line[..m.Index].Count(ch => ch == '"') % 2 == 0)
                {
                    continue;
                }

                foreach (Match name in SymbolName().Matches(m.Groups[1].Value))
                {
                    counts[name.Value] = counts.GetValueOrDefault(name.Value) + 1;
                }
            }
        }

        return counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).Take(2).ToList();
    }

    /// <summary>Whether a text is an input: a source file, or two of them separated by the split line.</summary>
    public static bool IsInput(string text)
    {
        var at = text.IndexOf("\n" + SplitMark + "\n", StringComparison.Ordinal);
        return at < 0 ? IsSource(text) : IsSource(text[..at]) && IsSource(text[(at + SplitMark.Length + 2)..]);
    }

    /// <summary>
    /// Moves to a library of its own the types that other types use and that use nothing of what stays, so the
    /// rest sees them as a referenced project. Null when the input has no such types.
    /// </summary>
    public static string? Split(string text)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        var top = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(t => t.Parent is not TypeDeclarationSyntax).ToList();
        static bool Uses(SyntaxNode user, BaseTypeDeclarationSyntax used) =>
            user.DescendantNodes().OfType<SimpleNameSyntax>().Any(n => n.Identifier.ValueText == used.Identifier.ValueText);
        var library = top.Where(t => top.Any(o => o != t && Uses(o, t))).ToList();
        while (library.RemoveAll(t => top.Except(library).Any(o => Uses(t, o))) > 0)
        {
        }

        if (library.Count == 0 || library.Count == top.Count)
        {
            return null;
        }

        var lib = root.RemoveNodes(top.Except(library), SyntaxRemoveOptions.KeepNoTrivia)!.ToFullString();
        var app = root.RemoveNodes(library, SyntaxRemoveOptions.KeepNoTrivia)!.ToFullString();
        return (MadePublic(lib) ?? lib).TrimEnd() + "\n" + SplitMark + "\n" + app.TrimStart('\r', '\n');
    }

    public static string? MadePublic(string text) =>
        Edit(text, root => root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .Where(t => t.Parent is not TypeDeclarationSyntax && !t.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            .Select(t => t.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.InternalKeyword)) is { RawKind: not 0 } hidden
                ? (hidden.SpanStart, hidden.Span.Length, "public")
                : (t.Modifiers.Count > 0 ? t.Modifiers[0].SpanStart : t is TypeDeclarationSyntax d ? d.Keyword.SpanStart : ((EnumDeclarationSyntax)t).EnumKeyword.SpanStart, 0, "public ")));

    /// <summary>
    /// Alters one literal argument of an attribute that two members of a type carry (`[Key(0)]`, `[Key(1)]`): the
    /// second made equal to the first, the second made the other kind of literal, or the first made null.
    /// </summary>
    public static string? Argument(string text, string how) =>
        Edit(text, root =>
        {
            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var pair = type.Members.SelectMany(m => m.AttributeLists).SelectMany(l => l.Attributes)
                    .Where(a => a.ArgumentList is { Arguments.Count: 1 } list && (list.Arguments[0].Expression.IsKind(SyntaxKind.NumericLiteralExpression) || list.Arguments[0].Expression.IsKind(SyntaxKind.StringLiteralExpression)))
                    .GroupBy(a => a.Name.ToString(), StringComparer.Ordinal)
                    .FirstOrDefault(g => g.Count() >= 2);
                if (pair is null)
                {
                    continue;
                }

                var first = pair.First().ArgumentList!.Arguments[0].Expression;
                var second = pair.ElementAt(1).ArgumentList!.Arguments[0].Expression;
                return how switch
                {
                    "repeat" => [(second.SpanStart, second.Span.Length, first.ToString())],
                    "kind" => [(second.SpanStart, second.Span.Length, second.IsKind(SyntaxKind.NumericLiteralExpression) ? "\"k\"" : "7")],
                    _ => [(first.SpanStart, first.Span.Length, "null")],
                };
            }

            return [];
        });

    public static string? WithoutAttributesOnUsedTypes(string text) =>
        Edit(text, root =>
        {
            var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();
            return types
                .Where(t => types.Any(o => o != t && !o.Ancestors().Contains(t) && o.DescendantNodes().OfType<SimpleNameSyntax>().Any(n => n.Identifier.ValueText == t.Identifier.ValueText)))
                .SelectMany(t => t.AttributeLists)
                .Select(l => (l.SpanStart, l.Span.Length, string.Empty));
        });

    public static string? MadeRecord(string text) =>
        Edit(text, root => root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(c => !c.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            .Select(c => (c.Keyword.SpanStart, c.Keyword.Span.Length, "record")));

    /// <summary>
    /// The inputs of a sweep: the ones found, as they are, and then altered ones up to the limit. The alterations
    /// taken from the change go first and over every input, because they are aimed at what the change does.
    /// </summary>
    public static List<SweepInput> Build(List<(string Origin, string Text)> corpus, List<(string Label, bool FromChange, Func<string, string?> Apply)> alterations, int max, string prefix = "i")
    {
        var inputs = new List<SweepInput>();
        var originals = Spread(corpus, Math.Max(1, alterations.Count == 0 ? max : max / 2));
        for (var i = 0; i < originals.Count; i++)
        {
            var id = $"{prefix}{i + 1:000}";
            inputs.Add(new SweepInput(id, $"{id} {originals[i].Origin}", originals[i].Text, originals[i].Origin, null, null));
        }

        var unaltered = inputs.ToList();
        bool Add(int a, SweepInput original)
        {
            string? text;
            try
            {
                text = alterations[a].Apply(original.Text);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // An alteration that cannot be applied to this input is not applied. It must never stop the sweep.
                return false;
            }

            if (text is null || text == original.Text || !IsInput(text))
            {
                return false;
            }

            var id = $"{original.Id}+a{a + 1}";
            inputs.Add(new SweepInput(id, $"{id} {original.Origin} + {alterations[a].Label}", text, original.Origin, alterations[a].Label, original.Id));
            return true;
        }

        // What comes from the change goes over every input: it is aimed at what the change does.
        for (var a = 0; a < alterations.Count; a++)
        {
            foreach (var original in unaltered.Where(_ => alterations[a].FromChange))
            {
                if (inputs.Count >= max)
                {
                    return inputs;
                }

                Add(a, original);
            }
        }

        // The rest share what is left in turns, one input each per turn, so that none is favoured: each starts
        // at a different input and takes the next one it can alter.
        var general = Enumerable.Range(0, alterations.Count).Where(a => !alterations[a].FromChange).ToList();
        var next = new int[alterations.Count];
        for (var progressed = true; progressed && inputs.Count < max;)
        {
            progressed = false;
            foreach (var a in general)
            {
                while (next[a] < unaltered.Count && inputs.Count < max)
                {
                    var original = unaltered[(next[a]++ + (a * 13)) % unaltered.Count];
                    if (Add(a, original))
                    {
                        progressed = true;
                        break;
                    }
                }
            }
        }

        return inputs;
    }

    public static string? WithAttribute(string text, string attribute) =>
        Edit(text, root => root.DescendantNodes().OfType<TypeDeclarationSyntax>().Select(t => (t.GetFirstToken().SpanStart, 0, attribute + " ")));

    public static string? WithoutPartial(string text) =>
        Edit(text, root => root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .SelectMany(t => t.Modifiers.Where(m => m.IsKind(SyntaxKind.PartialKeyword)))
            .Select(m => (m.SpanStart, m.Span.Length + (m.TrailingTrivia.ToFullString().StartsWith(' ') ? 1 : 0), string.Empty)));

    public static string? MadeInternal(string text) =>
        Edit(text, root => root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Where(t => t.Parent is not TypeDeclarationSyntax)
            .SelectMany(t => t.Modifiers.Where(m => m.IsKind(SyntaxKind.PublicKeyword)))
            .Select(m => (m.SpanStart, m.Span.Length, "internal")));

    public static string? MadeSealed(string text) =>
        Edit(text, root => root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(c => !c.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword) || m.IsKind(SyntaxKind.AbstractKeyword) || m.IsKind(SyntaxKind.StaticKeyword)))
            .Select(c => (c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) ? c.Modifiers.First(m => m.IsKind(SyntaxKind.PartialKeyword)).SpanStart : c.Keyword.SpanStart, 0, "sealed ")));

    public static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", string.Empty).Replace("\n", "\\n");

    /// <summary>Applies text edits found on the syntax tree, last first so the positions stay true. Null when there is nothing to edit.</summary>
    private static string? Edit(string text, Func<SyntaxNode, IEnumerable<(int At, int Remove, string Insert)>> find)
    {
        // An edit inside the reach of an earlier one (a member of a member that is itself taken out) is dropped:
        // the outer one already covers it, and applying both would cut past the end of what is left.
        var edits = new List<(int At, int Remove, string Insert)>();
        var covered = -1;
        foreach (var edit in find(CSharpSyntaxTree.ParseText(text).GetRoot()).Distinct().OrderBy(e => e.At).ThenByDescending(e => e.Remove))
        {
            if (edit.At < covered || edit.At < 0 || edit.At + edit.Remove > text.Length)
            {
                continue;
            }

            edits.Add(edit);
            covered = edit.At + edit.Remove;
        }

        edits.Reverse();
        if (edits.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder(text);
        foreach (var (at, remove, insert) in edits)
        {
            sb.Remove(at, remove).Insert(at, insert);
        }

        return sb.ToString();
    }

    /// <summary>Up to a number of items, taken evenly across the list so that no file of tests is left out.</summary>
    private static List<T> Spread<T>(List<T> all, int most)
    {
        if (all.Count <= most)
        {
            return all.ToList();
        }

        return Enumerable.Range(0, most).Select(i => all[(int)((long)i * all.Count / most)]).ToList();
    }

    // An attribute as it is written: after the start of the line, a space, a quote or a brace, never after a name
    // or a closing bracket, which would make it an index.
    [GeneratedRegex(@"(?<![\w\]\)\.])\[\s*((?:global::)?[A-Za-z_][\w.]*)\s*(\([^\[\]""]*\))?\s*\]")]
    private static partial Regex AttributeUse();

    [GeneratedRegex(@"#\s*(?:el)?if\s+([^""\\\r\n]+)")]
    private static partial Regex Conditional();

    [GeneratedRegex(@"\b[A-Z][A-Z0-9_]{2,}\b")]
    private static partial Regex SymbolName();

    [GeneratedRegex(@"\{\|(?:[A-Za-z0-9_]+:)?|\|\}|\[\||\|\]")]
    private static partial Regex Markup();
}

public sealed partial class Comparer
{
    /// <summary>
    /// Runs inputs nobody chose through the old and the new code. AYA builds the inputs; a model writes only the
    /// harness that feeds them to the code and records what comes out, and that harness is accepted the way a
    /// probe is: it has to run on both versions and be measured to execute the change.
    /// </summary>
    public (Comparison? Run, SweepResult Summary) Sweep(Workbench w, CodeGraph graph, ReviewResult result, string outDir, int maxInputs)
    {
        this.change = ChangeText.From(result.Files);
        var sweep = new SweepResult();
        var units = result.ChangedSymbols
            .Where(c => !c.IsTest && c.CoveredLines.Count + c.UncoveredLines.Count > 0)
            .OrderByDescending(c => c.Dependents)
            .ToList();
        sweep.ChangedLines = units.Sum(c => c.CoveredLines.Count + c.UncoveredLines.Count);
        var reached = units
            .Select(c => (Unit: c, Pattern: ModelSession.FindPattern(w, graph, result, c.Id, c.File)))
            .Where(x => x.Pattern is not null)
            .ToList();
        if (reached.Count == 0)
        {
            sweep.Detail = "no existing test reaches the changed code, so there is no test project to take inputs from";
            w.Trace.Info(sweep.Detail);
            return (null, sweep);
        }

        // The test project most of the changed units are reached from holds the inputs that matter to them.
        var home = reached.GroupBy(x => x.Pattern!.Project, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First();
        var (top, pattern) = (home.First().Unit, home.First().Pattern!);
        var testProject = w.ProjectPath(pattern.Project)!;
        sweep.TestProject = testProject;
        if (!File.Exists(Path.Combine(w.OldWork, testProject)))
        {
            sweep.Detail = "the test project does not exist in the old code, so nothing can be run there";
            w.Trace.Info(sweep.Detail);
            return (null, sweep);
        }

        // Inputs come from the old code's tests: every one of them is something the old code was already given.
        var corpus = SweepInputs.Corpus(Path.Combine(w.OldWork, Path.GetDirectoryName(testProject)!));
        sweep.Found = corpus.Count;
        if (corpus.Count == 0)
        {
            sweep.Detail = $"the tests of {testProject} hold no input of a kind AYA can build on (it looks for C# source in string literals)";
            w.Trace.Info(sweep.Detail);
            return (null, sweep);
        }

        var attributes = SweepInputs.Attributes(this.change.AddedLines, Introduced(result), this.change.TemplateLines);
        var symbols = SweepInputs.Symbols(this.change.AddedLines, this.change.TemplateLines);
        var alterations = SweepInputs.Alterations(attributes, symbols);
        var inputs = SweepInputs.Build(corpus, alterations, maxInputs);
        sweep.FromTests = inputs.Count(i => i.Alteration is null);
        sweep.Altered = inputs.Count - sweep.FromTests;
        sweep.Alterations = alterations
            .Select(a => new SweepAlteration(a.Label, a.FromChange, inputs.Count(i => i.Alteration == a.Label)))
            .Where(a => a.Inputs > 0)
            .ToList();

        var dir = Path.Combine(w.RunsDir, "probes");
        Directory.CreateDirectory(dir);
        var inputsFile = Path.Combine(dir, "S1-inputs.tsv");
        File.WriteAllLines(inputsFile, inputs.Select(i => SweepInputs.Escape(i.Name) + "\t" + SweepInputs.Escape(i.Text)), new UTF8Encoding(false));
        File.Copy(inputsFile, Path.Combine(outDir, "sweep-inputs.tsv"), overwrite: true);
        sweep.InputsFile = "sweep-inputs.tsv";

        w.Trace.Info($"{corpus.Count} input(s) found in the tests of {testProject}; {sweep.FromTests} taken as they are and {sweep.Altered} altered mechanically:");
        w.Trace.List(sweep.Alterations.Select(a => $"{a.Inputs,4} × {a.Label}" + (a.FromChange ? "   (taken from the change)" : string.Empty)), 10);

        var request = new Comparison
        {
            Request = "S1",
            Purpose = "sweep",
            Symbol = $"the change as a whole, over {inputs.Count} inputs built from the existing tests",
            SymbolId = top.Id,
            File = top.File,
            Lines = top.CoveredLines.Concat(top.UncoveredLines).OrderBy(l => l).ToList(),
            TestProject = testProject,
            InputsFile = inputsFile,
            Wider = units.GroupBy(c => c.File, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.SelectMany(c => c.CoveredLines.Concat(c.UncoveredLines)).Distinct().ToList(), StringComparer.OrdinalIgnoreCase),
        };
        request.Prompt = SweepPrompt(w, graph, request, pattern, units, inputs);
        this.RunAll(w, [request], outDir, int.MaxValue, "S", 1);

        sweep.Status = request.Status;
        sweep.Detail = request.Detail;
        sweep.ChangedLinesExecuted = request.WiderHit;
        if (request.Status == "compared")
        {
            Attribute(request, inputs);
            sweep.OnlyWhenAltered = request.Differences.Count(d => d.OnlyWhenAltered);
            w.Trace.Info($"the inputs drove {sweep.ChangedLinesExecuted} of the change's {sweep.ChangedLines} executable line(s); {request.Same} of {request.Cases} give the same result on old and new code, {request.Differences.Count} differ" +
                (sweep.OnlyWhenAltered > 0 ? $", {sweep.OnlyWhenAltered} of them only once the input is altered" : string.Empty));

            // A configuration in which the change changes nothing, while it does change the same inputs outside it.
            var differing = request.Differences.Select(d => d.Case.Split(' ')[0]).ToHashSet(StringComparer.Ordinal);
            foreach (var symbol in symbols)
            {
                var under = inputs.Where(i => i.Alteration == SweepInputs.SymbolLabel(symbol)).ToList();
                var plain = under.Count(i => differing.Contains(i.BasedOn!));
                if (under.Count > 0 && plain > 0 && !under.Any(i => differing.Contains(i.Id)))
                {
                    sweep.Notes.Add($"with {symbol} defined the change makes no difference on any of {under.Count} input(s), while {plain} of the same inputs differ without it: under that configuration the change does nothing");
                }
            }

            // Inputs on which the code under test falls over on both versions: not caused by the change, and not fixed by it.
            var fallen = request.HeadRecords.Where(kv => request.StableKeys.Contains(kv.Key) && !request.Differences.Any(d => d.Case == kv.Key) && Fell(kv.Value)).Select(kv => kv.Key).ToList();
            if (fallen.Count > 0)
            {
                sweep.Notes.Add($"on {fallen.Count} input(s) the code under test crashes on the old and on the new code alike (for example {fallen[0]})");
            }

            foreach (var note in sweep.Notes)
            {
                w.Trace.Info(note);
            }

            this.NewInputs(w, request, sweep, corpus, alterations, outDir);
        }

        return (request, sweep);
    }

    /// <summary>
    /// The inputs that came with the change have no old code to be compared against, so they are run on the new
    /// code alone and held to what must be true of any input: the code under test does not crash, and what it
    /// produces compiles unless it said itself that the input is wrong.
    /// </summary>
    private void NewInputs(Workbench w, Comparison request, SweepResult sweep, List<(string Origin, string Text)> oldCorpus, List<(string Label, bool FromChange, Func<string, string?> Apply)> alterations, string outDir)
    {
        static string Flat(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var known = oldCorpus.Select(c => Flat(c.Text)).ToHashSet(StringComparer.Ordinal);
        var fresh = SweepInputs.Corpus(Path.Combine(w.HeadWorktree, Path.GetDirectoryName(request.TestProject!)!)).Where(c => !known.Contains(Flat(c.Text))).ToList();
        if (fresh.Count == 0 || request.Code is null)
        {
            return;
        }

        var inputs = SweepInputs.Build(fresh, alterations.Where(a => !a.FromChange).ToList(), 400, "n");
        // Not the name of the run below: a run starts by deleting the file it is going to record into.
        var file = Path.Combine(w.RunsDir, "probes", "S1-inputs-of-the-change.tsv");
        File.WriteAllLines(file, inputs.Select(i => SweepInputs.Escape(i.Name) + "\t" + SweepInputs.Escape(i.Text)), new UTF8Encoding(false));
        File.Copy(file, Path.Combine(outDir, "sweep-new-inputs.tsv"), overwrite: true);
        var differential = request.InputsFile;
        request.InputsFile = file;
        var (run, records) = this.RunProbe(w, w.Work, request, request.Code, "S1-new-inputs", coverage: false);
        request.InputsFile = differential;
        sweep.NewInputs = inputs.Count;
        if (!run.Built || run.Problem is not null || records.Count == 0)
        {
            sweep.Notes.Add($"{inputs.Count} input(s) that came with the change could not be run on the new code: {run.Problem ?? "the run recorded nothing"}");
            return;
        }

        var byId = inputs.ToDictionary(i => i.Id, StringComparer.Ordinal);
        var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in records)
        {
            var lines = value.Split('\n').Select(l => l.Trim()).ToList();
            var silent = lines.Any(l => l.StartsWith("ERROR-GEN", StringComparison.Ordinal)) && !lines.Any(l => l.StartsWith("DIAG ", StringComparison.Ordinal) && l.Contains(" Error:", StringComparison.Ordinal));
            if (Fell(value) || silent)
            {
                kinds[name.Split(' ')[0]] = Fell(value) ? "crash" : "silently-broken";
            }
        }

        foreach (var (name, value) in records)
        {
            var id = name.Split(' ')[0];
            if (!kinds.TryGetValue(id, out var kind) || !byId.TryGetValue(id, out var input))
            {
                continue;
            }

            // An altered input that fails the way the input it was made from fails adds nothing.
            if (input.BasedOn is not null && kinds.TryGetValue(input.BasedOn, out var same) && same == kind)
            {
                continue;
            }

            var line = value.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("CRASH ", StringComparison.Ordinal) || l.StartsWith("EXCEPTION ", StringComparison.Ordinal) || l.StartsWith("ERROR-GEN", StringComparison.Ordinal)) ?? string.Empty;
            sweep.NewFindings.Add(new SweepFinding(
                name,
                kind,
                line.Length > 300 ? line[..300] + "…" : line,
                input.Alteration is null ? $"taken from a test that came with the change ({input.Origin})" : $"the input of a test that came with the change ({input.Origin}), altered: {input.Alteration}"));
        }

        w.Trace.Info($"{inputs.Count} input(s) that came with the change ({fresh.Count} as they are) were run on the new code alone: {sweep.NewFindings.Count(f => f.Kind == "crash")} make the code under test crash, {sweep.NewFindings.Count(f => f.Kind == "silently-broken")} get output that does not compile with no error reported");
        foreach (var f in sweep.NewFindings.Take(8))
        {
            w.Trace.Info($"      {f.Kind}: {f.Input} — {f.Line}");
        }
    }

    /// <summary>Whether a recorded result says the code under test itself fell over.</summary>
    private static bool Fell(string value) =>
        value.StartsWith("EXCEPTION ", StringComparison.Ordinal) || value.Split('\n').Any(l => l.TrimStart().StartsWith("CRASH ", StringComparison.Ordinal));

    /// <summary>
    /// Says where the input of each difference came from, and marks the ones that show up only with an alteration:
    /// the unaltered input either gives the same result on both versions or differs in another way.
    /// </summary>
    public static void Attribute(Comparison run, List<SweepInput> inputs)
    {
        var byId = inputs.ToDictionary(i => i.Id, StringComparer.Ordinal);
        var byCase = run.Differences.ToDictionary(d => d.Case, StringComparer.Ordinal);
        foreach (var d in run.Differences)
        {
            if (!byId.TryGetValue(d.Case.Split(' ')[0], out var input))
            {
                continue;
            }

            d.Input = input.Alteration is null ? $"taken from an existing test ({input.Origin})" : $"the input of an existing test ({input.Origin}), altered: {input.Alteration}";
            if (input.BasedOn is not null && byId.TryGetValue(input.BasedOn, out var original) && d.Case.StartsWith(input.Name, StringComparison.Ordinal))
            {
                // The same name with whatever the run added to it, such as the target framework.
                var twin = original.Name + d.Case[input.Name.Length..];
                d.OnlyWhenAltered = !byCase.TryGetValue(twin, out var plain) || plain.Group != d.Group;
            }
        }
    }

    private static string SweepPrompt(Workbench w, CodeGraph graph, Comparison request, Node pattern, List<ChangedSymbol> units, List<SweepInput> inputs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are writing one xUnit file for an existing C# repository. It is a harness, not a test:");
        sb.AppendLine("it does not judge the code, it runs a list of inputs through it and records what comes out of each.");
        sb.AppendLine("Reply with a single ```csharp code block containing the whole file, and nothing else.");
        sb.AppendLine();
        sb.AppendLine("## Why");
        sb.AppendLine("A change was made to the code listed below. Your harness will be run on the old version of the repository");
        sb.AppendLine("and on the new one over the same inputs, and what it records will be compared input by input.");
        sb.AppendLine($"The inputs are not yours to choose. There are {inputs.Count}: the C# sources that the existing tests of this");
        sb.AppendLine("project already give to the code, some as they are and some altered mechanically.");
        sb.AppendLine();
        sb.AppendLine("## What the change touched");
        foreach (var c in units.Take(15))
        {
            sb.AppendLine($"- {c.Display}   ({c.File})");
        }

        var entries = EntryPoints(w, units);
        if (entries.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## The generators and analyzers declared in the projects the change touched");
            foreach (var entry in entries)
            {
                sb.AppendLine($"- {entry}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## The inputs");
        sb.AppendLine("A helper named AyaProbe is already in the project. Do not declare it.");
        sb.AppendLine("`AyaProbe.Inputs()` returns a `List<KeyValuePair<string, string>>`: the name of each input and its text.");
        sb.AppendLine("Every text is a complete C# source file, like the one the test below passes to the code. The first one is:");
        sb.AppendLine("```csharp");
        sb.AppendLine(string.Join("\n", inputs[0].Text.Split('\n').Take(40)));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## What to do with each input");
        sb.AppendLine("Do with it what the existing test below does with its own source, up to the point where that test would");
        sb.AppendLine("compare against an expectation. Never compare, never assert.");
        sb.AppendLine("1. Build a compilation from the input with the same references and options the test helpers use.");
        sb.AppendLine("2. Run the code under test over it: the source generator AND the analyzers of the project, both, every time.");
        sb.AppendLine("3. Add what the generator produced to the compilation and get the compiler's diagnostics for the result.");
        sb.AppendLine("4. Record ONE text for the input, under the input's name, built in this order:");
        sb.AppendLine("   - one line per diagnostic the code under test reported (generator and analyzers), sorted: `DIAG <id> <severity>: <message>`");
        sb.AppendLine("   - if the code under test itself fell over (the compiler reports that as CS8785 for a generator and AD0001 for an");
        sb.AppendLine("     analyzer), write that diagnostic as `CRASH <id>: <first line of the message>` instead of a DIAG line");
        sb.AppendLine("   - for each file the generator produced, sorted by name: a line `OUT <name>` followed by the text of the file");
        sb.AppendLine("   - one line per compiler error in the result of step 3, sorted: `ERROR-GEN <id>: <message>` when the error is");
        sb.AppendLine("     located in a file the generator produced, `ERROR <id>: <message>` when it is located in the input");
        sb.AppendLine("   - one line per named type declared in the input (not in generated files), sorted, describing that type as it is in the");
        sb.AppendLine("     final compilation: `USER <type name>: attributes [<attribute names, sorted>]; bases [<base type and interfaces, sorted>]; members <how many>`");
        sb.AppendLine("Lines that start with `ERROR` or `CRASH` are read as \"this input does not work\". Use those prefixes for nothing");
        sb.AppendLine("else, and do not put positions, paths or line numbers in any line.");
        sb.AppendLine();
        sb.AppendLine("Two marks may appear in an input, and both must be honoured:");
        sb.AppendLine($"- A first line `{SweepInputs.SymbolsMark}A;B` means: parse the input AND the generated files with the preprocessor symbols A");
        sb.AppendLine("  and B defined (CSharpParseOptions.WithPreprocessorSymbols, passed to the generator driver as well).");
        sb.AppendLine($"- A line `{SweepInputs.SplitMark}` means: the text above it is a separate library. Compile it on its own with the same references,");
        sb.AppendLine("  emit it to a MemoryStream and add the image (MetadataReference.CreateFromStream) to the references of the text below");
        sb.AppendLine("  the line, which is the input proper: run the generator and the analyzers on that part only. If the library does not");
        sb.AppendLine("  emit, record the single line `SPLIT-FAILED` for the input.");
        sb.AppendLine("```csharp");
        sb.AppendLine("foreach (var input in AyaProbe.Inputs())");
        sb.AppendLine("{");
        sb.AppendLine("    await AyaProbe.RecordAsync(input.Key, async () => await Describe(input.Value)); // a failure is recorded, not thrown");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine("- One input that throws must not stop the others: every input goes through its own Record call.");
        sb.AppendLine("- What is recorded must not depend on time, random data, ordering of unordered collections or machine paths.");
        sb.AppendLine("- The helpers below may only offer methods that compare against expected output and throw. Do not call those:");
        sb.AppendLine("  take from them how references and options are set up, and drive the compiler yourself");
        sb.AppendLine("  (CSharpCompilation, CSharpGeneratorDriver, CompilationWithAnalyzers).");
        sb.AppendLine();
        ModelSession.AppendPattern(sb, w, pattern);
        foreach (var (file, text) in HelperFiles(w, graph, pattern))
        {
            sb.AppendLine();
            sb.AppendLine($"## A helper that test uses: {file}");
            sb.AppendLine("```csharp");
            sb.AppendLine(text);
            sb.AppendLine("```");
        }

        var project = Path.Combine(w.HeadWorktree, request.TestProject!);
        if (File.Exists(project))
        {
            sb.AppendLine();
            sb.AppendLine($"## The test project: {request.TestProject}");
            sb.AppendLine("```xml");
            sb.AppendLine(string.Join("\n", File.ReadAllLines(project).Take(90)));
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("## Rules");
        sb.AppendLine("- Your file is added to the same test project, next to the files above. It can use everything they use.");
        sb.AppendLine($"- Declare exactly one public class, named Aya{request.Request}Probe, in the same namespace as the test class above (none if it declares none), with a single [Fact] method that goes through every input.");
        sb.AppendLine("- It must compile against BOTH the old and the new version of the repository. Use only types and members that existed before the change.");
        sb.AppendLine("- Reach the code the way a caller can. Do not use reflection on private members, uninitialised objects or emitted types.");
        sb.AppendLine("- Do not depend on files that do not already exist in the repository.");
        return sb.ToString();
    }

    /// <summary>The source generators and analyzers declared in the projects that hold the changed code, with their namespaces.</summary>
    private static List<string> EntryPoints(Workbench w, List<ChangedSymbol> units)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in units.Select(c => c.File).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            for (var dir = Path.GetDirectoryName(Path.Combine(w.HeadWorktree, file)); dir is not null && dir.Length >= w.HeadWorktree.Length; dir = Path.GetDirectoryName(dir))
            {
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.csproj").Any())
                {
                    folders.Add(dir);
                    break;
                }
            }
        }

        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in folders.SelectMany(f => Directory.EnumerateFiles(f, "*.cs", SearchOption.AllDirectories)).Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(path);
            if (!text.Contains("[Generator", StringComparison.Ordinal) && !text.Contains("[DiagnosticAnalyzer", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var type in CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var marks = type.AttributeLists.SelectMany(l => l.Attributes).Select(a => a.Name.ToString().Split('.').Last()).ToList();
                var kind = marks.Any(m => m is "Generator" or "GeneratorAttribute") ? "generator" : marks.Any(m => m is "DiagnosticAnalyzer" or "DiagnosticAnalyzerAttribute") ? "analyzer" : null;
                if (kind is not null && !type.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
                {
                    var ns = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
                    found.Add($"{kind}: {(ns is null ? string.Empty : ns + ".")}{type.Identifier.ValueText}");
                }
            }
        }

        return found.Take(12).ToList();
    }

    /// <summary>The files of the test project that the pattern test leans on, nearest first: where its plumbing lives.</summary>
    private static List<(string File, string Text)> HelperFiles(Workbench w, CodeGraph graph, Node pattern)
    {
        var own = pattern.Spans[0].File;
        var files = graph.Reach([pattern.Id], reverse: false)
            .Where(kv => kv.Value is > 0 and <= 3)
            .Select(kv => (Node: graph.Nodes[kv.Key], Distance: kv.Value))
            .Where(x => x.Node.Project == pattern.Project && !x.Node.IsTest && x.Node.Spans.Count > 0)
            .OrderBy(x => x.Distance)
            .Select(x => x.Node.Spans[0].File)
            .Where(f => !string.Equals(f, own, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
        var list = new List<(string, string)>();
        foreach (var file in files)
        {
            var path = Path.Combine(w.HeadWorktree, file);
            if (File.Exists(path))
            {
                list.Add((file, string.Join("\n", File.ReadAllLines(path).Take(220))));
            }
        }

        return list;
    }
}
