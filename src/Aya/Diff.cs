using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aya;

public sealed class Hunk
{
    public int OldStart { get; init; }

    public int OldLen { get; init; }

    public int NewStart { get; init; }

    public int NewLen { get; init; }

    [JsonIgnore]
    public List<string> Removed { get; } = new();

    [JsonIgnore]
    public List<string> Added { get; } = new();
}

public sealed class FileChange
{
    public string Path { get; set; } = string.Empty;

    public string? OldPath { get; set; }

    /// <summary>A (added), M (modified), D (deleted) or R (renamed).</summary>
    public string Status { get; set; } = "M";

    /// <summary>source | test | test-snapshot | template | generated | docs | build-config | other.</summary>
    public string Category { get; set; } = "other";

    public int Added { get; set; }

    public int Deleted { get; set; }

    public bool Binary { get; set; }

    /// <summary>Identifier of the similar-edit group this file belongs to. See <see cref="PatternGroup"/>.</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>Identifier of the exact edit: the same lines, in the same order, the same number of times.</summary>
    public string ExactPattern { get; set; } = string.Empty;

    [JsonIgnore]
    public List<Hunk> Hunks { get; } = new();

    /// <summary>Line numbers, in the new version, that this change added or rewrote.</summary>
    public IEnumerable<int> AddedLines() => this.Hunks.SelectMany(h => Enumerable.Range(h.NewStart, h.NewLen));
}

/// <summary>
/// Files whose changes use the same distinct line edits, ignoring indentation, order and repetition. That makes
/// them similar, not identical: one file may apply the edit once and another three times. The exact variants are
/// listed so nobody reads one file and assumes the rest.
/// </summary>
public sealed class PatternGroup
{
    public required string Id { get; init; }

    public required string Category { get; init; }

    public required List<string> Files { get; init; }

    /// <summary>The distinct edits, as "-removed" and "+added" lines with indentation stripped.</summary>
    public required List<string> Edits { get; init; }

    /// <summary>The files split by exact edit, largest first. One variant means the files really are identical edits.</summary>
    public List<PatternVariant> Variants { get; } = new();
}

/// <summary>Files that received exactly the same edit: same lines, same order, same number of times.</summary>
public sealed class PatternVariant
{
    public required string Id { get; init; }

    public required int EditedLines { get; init; }

    public required List<string> Files { get; init; }
}

/// <summary>
/// What a C# file declares, read from its syntax alone, so that the old and the new version of a file can be
/// compared without building the old one. A declaration is known by the type it is in, its name and, for
/// methods and constructors, how many parameters it takes.
/// </summary>
public static class Declarations
{
    /// <summary>The key of every declaration in a source text.</summary>
    public static HashSet<string> In(string source)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes())
        {
            switch (node)
            {
                case BaseTypeDeclarationSyntax type:
                    keys.Add(Key(Owner(type), type.Identifier.Text, -1));
                    if (type is TypeDeclarationSyntax { ParameterList: { } primary })
                    {
                        // A record or a type with a primary constructor declares that constructor and, for a
                        // record, one property per parameter, without a declaration of their own.
                        keys.Add(Key(type.Identifier.Text, type.Identifier.Text, primary.Parameters.Count));
                        foreach (var parameter in primary.Parameters)
                        {
                            keys.Add(Key(type.Identifier.Text, parameter.Identifier.Text, -1));
                        }
                    }
                    else if (type is TypeDeclarationSyntax declared and (ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax)
                        && !declared.Members.OfType<ConstructorDeclarationSyntax>().Any(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword)))
                    {
                        // The constructor the compiler supplies when none is written.
                        keys.Add(Key(type.Identifier.Text, type.Identifier.Text, 0));
                    }

                    break;
                case DelegateDeclarationSyntax @delegate:
                    keys.Add(Key(Owner(@delegate), @delegate.Identifier.Text, -1));
                    break;
                case MethodDeclarationSyntax method:
                    keys.Add(Key(Owner(method), method.Identifier.Text, method.ParameterList.Parameters.Count));
                    break;
                case ConstructorDeclarationSyntax constructor:
                    keys.Add(Key(Owner(constructor), constructor.Identifier.Text, constructor.ParameterList.Parameters.Count));
                    break;
                case PropertyDeclarationSyntax property:
                    keys.Add(Key(Owner(property), property.Identifier.Text, -1));
                    break;
                case EventDeclarationSyntax @event:
                    keys.Add(Key(Owner(@event), @event.Identifier.Text, -1));
                    break;
                case EnumMemberDeclarationSyntax member:
                    keys.Add(Key(Owner(member), member.Identifier.Text, -1));
                    break;
                case VariableDeclaratorSyntax variable when variable.Parent?.Parent is FieldDeclarationSyntax or EventFieldDeclarationSyntax:
                    keys.Add(Key(Owner(variable), variable.Identifier.Text, -1));
                    break;
            }
        }

        return keys;
    }

    /// <summary>
    /// The key of a unit of the graph, from the name it is shown by, or null when that name cannot be matched
    /// against syntax with confidence (operators, indexers, finalizers), in which case nothing is concluded.
    /// </summary>
    public static string? KeyOf(string display, string kind)
    {
        var paren = display.IndexOf('(');
        var head = paren >= 0 ? display[..paren] : display;
        var parts = SplitTopLevel(head, '.');
        var name = WithoutTypeArguments(parts[^1]);
        if (name.Length == 0 || name.Contains(' ') || name.StartsWith('~') || name.StartsWith("this", StringComparison.Ordinal) || name.StartsWith("operator", StringComparison.Ordinal))
        {
            return null;
        }

        var owner = parts.Count > 1 ? WithoutTypeArguments(parts[^2]) : string.Empty;
        if (kind is "method" or "ctor")
        {
            var close = display.LastIndexOf(')');
            if (paren < 0 || close < paren)
            {
                return null;
            }

            var arguments = display[(paren + 1)..close];
            return Key(owner, name, string.IsNullOrWhiteSpace(arguments) ? 0 : SplitTopLevel(arguments, ',').Count);
        }

        return Key(owner, name, -1);
    }

    /// <summary>
    /// Whether the old file declared the unit. When the owner does not match but the same name with the same
    /// number of parameters exists (an explicit interface implementation is shown under the interface's name),
    /// it is taken as existing: calling something modified sends more attention to it, not less.
    /// </summary>
    public static bool Declares(HashSet<string> before, string key) =>
        before.Contains(key) || before.Any(k => k.EndsWith(key[key.IndexOf('.')..], StringComparison.Ordinal));

    private static string Key(string owner, string name, int parameters) => $"{owner}.{name}/{parameters}";

    private static string Owner(SyntaxNode node) =>
        node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? string.Empty;

    private static string WithoutTypeArguments(string name) => name.IndexOf('<') is var i and > 0 ? name[..i] : name;

    /// <summary>Splits on a separator that is not inside angle brackets, parentheses or square brackets.</summary>
    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch is '<' or '(' or '[')
            {
                depth++;
            }
            else if (ch is '>' or ')' or ']')
            {
                depth--;
            }
            else if (ch == separator && depth == 0)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }

        parts.Add(text[start..].Trim());
        return parts;
    }
}

public static partial class DiffParser
{
    public static List<FileChange> Parse(string diff)
    {
        var files = new List<FileChange>();
        FileChange? file = null;
        Hunk? hunk = null;

        foreach (var raw in diff.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                file = new FileChange();
                var b = line.LastIndexOf(" b/", StringComparison.Ordinal);
                if (b > 0)
                {
                    file.Path = line[(b + 3)..];
                }

                files.Add(file);
                hunk = null;
                continue;
            }

            if (file is null)
            {
                continue;
            }

            if (line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                var m = HunkHeader().Match(line);
                if (m.Success)
                {
                    hunk = new Hunk
                    {
                        OldStart = int.Parse(m.Groups[1].Value),
                        OldLen = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1,
                        NewStart = int.Parse(m.Groups[3].Value),
                        NewLen = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 1,
                    };
                    file.Hunks.Add(hunk);
                }

                continue;
            }

            if (hunk is null)
            {
                // Still in the per-file header.
                if (line.StartsWith("new file mode", StringComparison.Ordinal))
                {
                    file.Status = "A";
                }
                else if (line.StartsWith("deleted file mode", StringComparison.Ordinal))
                {
                    file.Status = "D";
                }
                else if (line.StartsWith("rename from ", StringComparison.Ordinal))
                {
                    file.Status = "R";
                    file.OldPath = line["rename from ".Length..];
                }
                else if (line.StartsWith("rename to ", StringComparison.Ordinal))
                {
                    file.Path = line["rename to ".Length..];
                }
                else if (line.StartsWith("Binary files ", StringComparison.Ordinal))
                {
                    file.Binary = true;
                }

                continue;
            }

            if (line.StartsWith('+'))
            {
                hunk.Added.Add(line[1..]);
                file.Added++;
            }
            else if (line.StartsWith('-'))
            {
                hunk.Removed.Add(line[1..]);
                file.Deleted++;
            }
        }

        return files;
    }

    /// <summary>Classifies a changed file by its role, which decides what kind of evidence applies to it.</summary>
    public static string Categorize(string worktree, string path)
    {
        var p = path.Replace('\\', '/');
        var ext = System.IO.Path.GetExtension(p).ToLowerInvariant();
        var isTest = Paths.IsTestPath(p);

        if (isTest && p.Contains("/Resources/", StringComparison.Ordinal))
        {
            return "test-snapshot";
        }

        switch (ext)
        {
            case ".tt":
                return "template";
            case ".cs":
                if (File.Exists(System.IO.Path.Combine(worktree, System.IO.Path.ChangeExtension(p, ".tt"))))
                {
                    return "generated";
                }

                return isTest ? "test" : "source";
            case ".md" or ".txt":
                return "docs";
            case ".csproj" or ".props" or ".targets" or ".sln" or ".json" or ".yml" or ".yaml" or ".config" or ".editorconfig":
                return "build-config";
            default:
                return "other";
        }
    }

    /// <summary>
    /// Groups files by the set of distinct line edits they received, ignoring indentation and how many times
    /// each edit repeats, then splits every group into the variants that are exactly the same edit.
    /// </summary>
    public static List<PatternGroup> GroupPatterns(List<FileChange> files)
    {
        var groups = new Dictionary<string, PatternGroup>(StringComparer.Ordinal);
        foreach (var f in files)
        {
            if (f.Binary || f.Hunks.Count == 0)
            {
                continue;
            }

            var edits = f.Hunks
                .SelectMany(h => h.Removed.Select(l => "-" + l.Trim()).Concat(h.Added.Select(l => "+" + l.Trim())))
                .Where(l => l.Length > 1)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(l => l, StringComparer.Ordinal)
                .ToList();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(f.Category + "\n" + string.Join("\n", edits))))[..10].ToLowerInvariant();
            f.Pattern = hash;
            if (!groups.TryGetValue(hash, out var g))
            {
                groups[hash] = g = new PatternGroup { Id = hash, Category = f.Category, Files = new(), Edits = edits };
            }

            g.Files.Add(f.Path);

            // The exact edit keeps what the similar one throws away: order, repetition and indentation.
            var exact = f.Hunks.SelectMany(h => h.Removed.Select(l => "-" + l.TrimEnd('\r')).Concat(h.Added.Select(l => "+" + l.TrimEnd('\r')))).ToList();
            f.ExactPattern = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", exact))))[..10].ToLowerInvariant();
            var variant = g.Variants.FirstOrDefault(v => v.Id == f.ExactPattern);
            if (variant is null)
            {
                g.Variants.Add(variant = new PatternVariant { Id = f.ExactPattern, EditedLines = exact.Count, Files = new() });
            }

            variant.Files.Add(f.Path);
        }

        foreach (var g in groups.Values)
        {
            g.Variants.Sort((a, b) => b.Files.Count != a.Files.Count ? b.Files.Count.CompareTo(a.Files.Count) : string.CompareOrdinal(a.Id, b.Id));
        }

        return groups.Values.OrderByDescending(g => g.Files.Count).ThenBy(g => g.Id, StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@")]
    private static partial Regex HunkHeader();
}

/// <summary>Finds which projects a change touches and which test projects depend on them.</summary>
public static partial class ProjectMap
{
    public static string? NearestProject(string worktree, string relFile)
    {
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.Combine(worktree, relFile));
        var root = System.IO.Path.GetFullPath(worktree);
        while (dir is not null && dir.Length >= root.Length)
        {
            if (Directory.Exists(dir))
            {
                var project = Directory.EnumerateFiles(dir, "*.csproj").FirstOrDefault();
                if (project is not null)
                {
                    return Paths.Rel(worktree, project);
                }
            }

            dir = System.IO.Path.GetDirectoryName(dir);
        }

        return null;
    }

    /// <summary>Project → projects it references, read from the ProjectReference items of every csproj.</summary>
    public static Dictionary<string, List<string>> References(string worktree)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in Directory.EnumerateFiles(worktree, "*.csproj", SearchOption.AllDirectories))
        {
            var rel = Paths.Rel(worktree, project);
            if (rel.StartsWith("bin/", StringComparison.Ordinal) || rel.StartsWith("obj/", StringComparison.Ordinal) || rel.Contains("/obj/") || rel.Contains("/bin/"))
            {
                continue;
            }

            var refs = new List<string>();
            foreach (Match m in ProjectReference().Matches(File.ReadAllText(project)))
            {
                var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(project)!, m.Groups[1].Value.Replace('\\', System.IO.Path.DirectorySeparatorChar)));
                refs.Add(Paths.Rel(worktree, target));
            }

            map[rel] = refs;
        }

        return map;
    }

    /// <summary>Projects that reference any of the given ones, directly or through other projects.</summary>
    public static HashSet<string> Dependents(Dictionary<string, List<string>> references, IEnumerable<string> projects)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var frontier = new Queue<string>(projects);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var (project, refs) in references)
            {
                if (refs.Contains(current, StringComparer.OrdinalIgnoreCase) && result.Add(project))
                {
                    frontier.Enqueue(project);
                }
            }
        }

        return result;
    }

    [GeneratedRegex("<ProjectReference\\s+Include=\"([^\"]+)\"")]
    private static partial Regex ProjectReference();
}
