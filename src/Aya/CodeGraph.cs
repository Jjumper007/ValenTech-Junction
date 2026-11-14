using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace Aya;

/// <summary>A range of 1-based lines in a repo-relative file.</summary>
public sealed record Span(string File, int Start, int End);

public sealed class Node
{
    public required string Id { get; init; }

    /// <summary>One of: type, method, ctor, property, field, event.</summary>
    public required string Kind { get; init; }

    public required string Name { get; init; }

    public required string Display { get; init; }

    public string? ContainingType { get; init; }

    public string Namespace { get; init; } = string.Empty;

    public required string Project { get; init; }

    /// <summary>Declaration ranges. Partial types and methods have more than one.</summary>
    public List<Span> Spans { get; } = new();

    public bool IsTest { get; set; }

    /// <summary>Name as the test runner reports it, without theory arguments.</summary>
    public string? TestName { get; set; }

    public bool IsGenerated { get; set; }

    public bool IsPublicApi { get; set; }

    /// <summary>Overrides or implements a member defined outside the analysed code, so a framework calls it.</summary>
    public bool IsEntryPoint { get; set; }

    /// <summary>A compile-time constant. Its value is copied into the code that uses it, so its line never runs.</summary>
    public bool IsConstant { get; set; }

    /// <summary>0 = depends on nothing in the analysed code; otherwise 1 + the highest layer it depends on.</summary>
    public int Layer { get; set; }

    /// <summary>Strongly connected component. Nodes that depend on each other in a cycle share one.</summary>
    public int Scc { get; set; }
}

/// <param name="Kind">uses | type-ref | dispatch | entry.</param>
public sealed record Edge(string From, string To, string Kind);

public sealed class CodeGraph
{
    private Dictionary<string, List<string>>? outgoing;
    private Dictionary<string, List<string>>? incoming;

    public Dictionary<string, Node> Nodes { get; } = new(StringComparer.Ordinal);

    public HashSet<Edge> Edges { get; } = new();

    public List<string> Projects { get; } = new();

    /// <summary>Problems met while loading. A non-empty list means the graph may be missing nodes or edges.</summary>
    public List<string> Warnings { get; } = new();

    public int LayerCount { get; private set; }

    public void AddEdge(string from, string to, string kind)
    {
        if (from == to || !this.Nodes.ContainsKey(from) || !this.Nodes.ContainsKey(to))
        {
            return;
        }

        if (this.Edges.Add(new Edge(from, to, kind)))
        {
            this.outgoing = null;
            this.incoming = null;
        }
    }

    public IReadOnlyList<string> Out(string id)
    {
        this.Index();
        return this.outgoing!.TryGetValue(id, out var l) ? l : Array.Empty<string>();
    }

    public IReadOnlyList<string> In(string id)
    {
        this.Index();
        return this.incoming!.TryGetValue(id, out var l) ? l : Array.Empty<string>();
    }

    /// <summary>Breadth-first distances from the seeds. Reverse follows edges backwards: "who depends on this".</summary>
    public Dictionary<string, int> Reach(IEnumerable<string> seeds, bool reverse)
    {
        var dist = new Dictionary<string, int>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        foreach (var s in seeds)
        {
            if (this.Nodes.ContainsKey(s) && dist.TryAdd(s, 0))
            {
                queue.Enqueue(s);
            }
        }

        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            foreach (var m in reverse ? this.In(n) : this.Out(n))
            {
                if (dist.TryAdd(m, dist[n] + 1))
                {
                    queue.Enqueue(m);
                }
            }
        }

        return dist;
    }

    /// <summary>
    /// Collapses dependency cycles (Tarjan's strongly connected components) and assigns each node a layer
    /// in the resulting acyclic graph.
    /// </summary>
    public void ComputeLayers()
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var low = new Dictionary<string, int>(StringComparer.Ordinal);
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var components = new List<List<string>>();
        var next = 0;

        foreach (var start in this.Nodes.Keys)
        {
            if (index.ContainsKey(start))
            {
                continue;
            }

            // Iterative depth-first search: real call graphs are deep enough to overflow the thread stack.
            var work = new Stack<(string Node, int Child)>();
            work.Push((start, 0));
            while (work.Count > 0)
            {
                var (n, child) = work.Pop();
                if (child == 0)
                {
                    index[n] = low[n] = next++;
                    stack.Push(n);
                    onStack.Add(n);
                }

                var outs = this.Out(n);
                var descended = false;
                for (; child < outs.Count; child++)
                {
                    var w = outs[child];
                    if (!index.ContainsKey(w))
                    {
                        work.Push((n, child + 1));
                        work.Push((w, 0));
                        descended = true;
                        break;
                    }

                    if (onStack.Contains(w))
                    {
                        low[n] = Math.Min(low[n], index[w]);
                    }
                }

                if (descended)
                {
                    continue;
                }

                if (low[n] == index[n])
                {
                    var component = new List<string>();
                    string member;
                    do
                    {
                        member = stack.Pop();
                        onStack.Remove(member);
                        this.Nodes[member].Scc = components.Count;
                        component.Add(member);
                    }
                    while (member != n);
                    components.Add(component);
                }

                if (work.Count > 0)
                {
                    var parent = work.Peek().Node;
                    low[parent] = Math.Min(low[parent], low[n]);
                }
            }
        }

        // Tarjan emits components dependencies-first, so every dependency already has its layer.
        var layerOf = new int[components.Count];
        for (var c = 0; c < components.Count; c++)
        {
            var layer = 0;
            foreach (var n in components[c])
            {
                foreach (var w in this.Out(n))
                {
                    var target = this.Nodes[w].Scc;
                    if (target != c)
                    {
                        layer = Math.Max(layer, layerOf[target] + 1);
                    }
                }
            }

            layerOf[c] = layer;
            foreach (var n in components[c])
            {
                this.Nodes[n].Layer = layer;
            }
        }

        this.LayerCount = layerOf.Length == 0 ? 0 : layerOf.Max() + 1;
    }

    private void Index()
    {
        if (this.outgoing is not null)
        {
            return;
        }

        var o = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var i = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (from, to) in this.Edges.Select(e => (e.From, e.To)).Distinct())
        {
            (o.TryGetValue(from, out var a) ? a : o[from] = new()).Add(to);
            (i.TryGetValue(to, out var b) ? b : i[to] = new()).Add(from);
        }

        this.outgoing = o;
        this.incoming = i;
    }
}

/// <summary>Builds the dependency graph from the C# compiler's own semantic model, not from text matching.</summary>
public static class GraphBuilder
{
    private static readonly string[] TestAttributes = ["FactAttribute", "TheoryAttribute", "TestAttribute", "TestMethodAttribute", "TestCaseAttribute"];

    public static async Task<CodeGraph> BuildAsync(string root, IReadOnlyList<string> projectPaths, Action<string> log)
    {
        var graph = new CodeGraph();
        _ = Dotnet.Exe; // Puts an SDK on PATH for the build host before the workspace starts it.

        using var workspace = MSBuildWorkspace.Create(new Dictionary<string, string> { ["Configuration"] = "Debug" });
        workspace.WorkspaceFailed += (_, e) =>
        {
            // These two are reported as failures but do not remove code from the compilation.
            var message = e.Diagnostic.Message;
            if (message.Contains("severity vulnerability", StringComparison.Ordinal) || message.StartsWith("Duplicate source file", StringComparison.Ordinal))
            {
                return;
            }

            lock (graph.Warnings)
            {
                if (graph.Warnings.Count < 100 && !graph.Warnings.Contains(e.Diagnostic.Message))
                {
                    graph.Warnings.Add(e.Diagnostic.Message);
                }
            }
        };

        foreach (var path in projectPaths)
        {
            var full = Path.GetFullPath(path);
            if (workspace.CurrentSolution.Projects.Any(p => string.Equals(p.FilePath, full, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            log($"loading {Paths.Rel(root, full)}");
            try
            {
                await workspace.OpenProjectAsync(full);
            }
            catch (Exception ex)
            {
                graph.Warnings.Add($"could not load {Paths.Rel(root, full)}: {ex.Message}");
            }
        }

        // A multi-targeted project appears once per target framework; analyse one of them.
        var projects = workspace.CurrentSolution.Projects
            .Where(p => p.Language == LanguageNames.CSharp && p.FilePath is not null)
            .GroupBy(p => p.FilePath!, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(p => p.Name.Contains("net8.0") ? 2 : p.Name.Contains("netstandard2.0") ? 1 : 0).First())
            .ToList();

        var units = new List<(string Project, bool IsTestProject, SemanticModel Model, SyntaxNode Root, string File)>();
        foreach (var project in projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation is null)
            {
                graph.Warnings.Add($"no compilation for {project.Name}");
                continue;
            }

            var relProject = Paths.Rel(root, project.FilePath!);
            graph.Projects.Add(relProject);
            foreach (var tree in compilation.SyntaxTrees)
            {
                if (string.IsNullOrEmpty(tree.FilePath))
                {
                    continue;
                }

                var rel = Paths.Rel(root, tree.FilePath);
                if (rel.StartsWith("..", StringComparison.Ordinal) || rel.StartsWith("obj/", StringComparison.Ordinal) || rel.Contains("/obj/") || rel.StartsWith("bin/", StringComparison.Ordinal))
                {
                    continue;
                }

                units.Add((Path.GetFileNameWithoutExtension(relProject), Paths.IsTestPath(relProject), compilation.GetSemanticModel(tree), await tree.GetRootAsync(), rel));
            }
        }

        log($"indexing {units.Count} files in {graph.Projects.Count} projects");

        // Pass 1: every declared type and member becomes a node.
        var declarations = new List<(string Id, SyntaxNode Decl, SemanticModel Model, bool IsType)>();
        var types = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var unit in units)
        {
            var generated = IsGeneratedFile(root, unit.File, unit.Root);
            foreach (var (symbol, decl) in Declared(unit.Model, unit.Root))
            {
                var id = IdOf(symbol);
                if (id is null)
                {
                    continue;
                }

                if (!graph.Nodes.TryGetValue(id, out var node))
                {
                    node = new Node
                    {
                        Id = id,
                        Kind = KindOf(symbol),
                        Name = symbol.Name,
                        Display = symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                        ContainingType = symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                        Namespace = symbol.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : string.Empty,
                        Project = unit.Project,
                        IsGenerated = generated,
                        IsPublicApi = !unit.IsTestProject && IsPublic(symbol),
                        IsConstant = symbol is IFieldSymbol { IsConst: true },
                    };
                    if (symbol is IMethodSymbol method && IsTestMethod(method))
                    {
                        node.IsTest = true;
                        node.TestName = TestNameOf(method);
                    }

                    graph.Nodes[id] = node;
                }

                node.Spans.Add(SpanOf(decl, unit.File));
                declarations.Add((id, decl, unit.Model, symbol is INamedTypeSymbol));
                if (symbol is INamedTypeSymbol type)
                {
                    types[id] = type;
                }
            }
        }

        // Pass 2: what each declaration refers to.
        foreach (var (id, decl, model, isType) in declarations)
        {
            IEnumerable<SyntaxNode> scope = decl.DescendantNodes();
            if (isType)
            {
                // A type's own edges come from its base list and attributes only; members carry the rest.
                scope = decl is BaseTypeDeclarationSyntax t
                    ? (t.BaseList?.DescendantNodes() ?? []).Concat(t.AttributeLists.SelectMany(a => a.DescendantNodes()))
                    : decl.DescendantNodes();
            }

            foreach (var n in scope)
            {
                if (n is not (SimpleNameSyntax or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax
                    or ConstructorInitializerSyntax or ElementAccessExpressionSyntax or PrimaryConstructorBaseTypeSyntax))
                {
                    continue;
                }

                var info = model.GetSymbolInfo(n);
                foreach (var target in Targets(info.Symbol ?? info.CandidateSymbols.FirstOrDefault(), 0))
                {
                    if (IdOf(target) is { } targetId)
                    {
                        graph.AddEdge(id, targetId, target is INamedTypeSymbol ? "type-ref" : "uses");
                    }
                }
            }
        }

        // Pass 3: calls the compiler cannot see as direct calls.
        foreach (var (typeId, type) in types)
        {
            foreach (var member in type.GetMembers())
            {
                var memberId = IdOf(member);
                if (memberId is null || !graph.Nodes.TryGetValue(memberId, out var memberNode))
                {
                    continue;
                }

                if (member is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor })
                {
                    // Referring to a type is treated as possibly constructing it.
                    graph.AddEdge(typeId, memberId, "entry");
                }

                ISymbol? overridden = member switch
                {
                    IMethodSymbol m => m.OverriddenMethod,
                    IPropertySymbol p => p.OverriddenProperty,
                    IEventSymbol e => e.OverriddenEvent,
                    _ => null,
                };
                if (overridden is not null)
                {
                    LinkDispatch(graph, overridden.OriginalDefinition, typeId, memberNode);
                }
            }

            foreach (var iface in type.AllInterfaces)
            {
                foreach (var ifaceMember in iface.GetMembers())
                {
                    if (ifaceMember is IMethodSymbol { AssociatedSymbol: not null })
                    {
                        continue; // Accessors are covered by their property or event.
                    }

                    var impl = type.FindImplementationForInterfaceMember(ifaceMember);
                    if (impl is null || !SymbolEqualityComparer.Default.Equals(impl.ContainingType, type))
                    {
                        continue;
                    }

                    if (IdOf(impl) is { } implId && graph.Nodes.TryGetValue(implId, out var implNode))
                    {
                        LinkDispatch(graph, ifaceMember.OriginalDefinition, typeId, implNode);
                    }
                }
            }
        }

        graph.ComputeLayers();
        return graph;
    }

    /// <summary>
    /// A call through a base or interface member may land on this implementation. If the base member is outside
    /// the analysed code, a framework calls the implementation, so reaching the type reaches it.
    /// </summary>
    private static void LinkDispatch(CodeGraph graph, ISymbol baseMember, string typeId, Node implementation)
    {
        if (IdOf(baseMember) is { } baseId && graph.Nodes.ContainsKey(baseId))
        {
            graph.AddEdge(baseId, implementation.Id, "dispatch");
        }
        else
        {
            implementation.IsEntryPoint = true;
            graph.AddEdge(typeId, implementation.Id, "entry");
        }
    }

    private static IEnumerable<(ISymbol Symbol, SyntaxNode Decl)> Declared(SemanticModel model, SyntaxNode root)
    {
        foreach (var n in root.DescendantNodes())
        {
            switch (n)
            {
                case BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax:
                    if (model.GetDeclaredSymbol(n) is { } symbol)
                    {
                        yield return (symbol, n);
                    }

                    break;
                case BaseFieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                    {
                        if (model.GetDeclaredSymbol(variable) is { } fieldSymbol)
                        {
                            yield return (fieldSymbol, field);
                        }
                    }

                    break;
            }
        }
    }

    /// <summary>Normalises a referenced symbol to the declarations it stands for.</summary>
    private static IEnumerable<ISymbol> Targets(ISymbol? symbol, int depth)
    {
        if (symbol is null || depth > 8)
        {
            yield break;
        }

        switch (symbol)
        {
            case IAliasSymbol alias:
                foreach (var t in Targets(alias.Target, depth + 1))
                {
                    yield return t;
                }

                break;
            case IMethodSymbol method:
                method = method.ReducedFrom ?? method;
                if (method.MethodKind is MethodKind.LocalFunction or MethodKind.AnonymousFunction or MethodKind.BuiltinOperator)
                {
                    yield break;
                }

                if (method.AssociatedSymbol is { } owner)
                {
                    yield return owner.OriginalDefinition;
                    yield break;
                }

                yield return method.OriginalDefinition;
                if (method.MethodKind == MethodKind.Constructor && method.ContainingType is { } constructed)
                {
                    foreach (var t in Targets(constructed, depth + 1))
                    {
                        yield return t;
                    }
                }

                foreach (var arg in method.TypeArguments)
                {
                    foreach (var t in Targets(arg, depth + 1))
                    {
                        yield return t;
                    }
                }

                break;
            case IPropertySymbol or IFieldSymbol or IEventSymbol:
                yield return symbol.OriginalDefinition;
                break;
            case INamedTypeSymbol type:
                yield return type.OriginalDefinition;
                foreach (var arg in type.TypeArguments)
                {
                    foreach (var t in Targets(arg, depth + 1))
                    {
                        yield return t;
                    }
                }

                break;
            case IArrayTypeSymbol array:
                foreach (var t in Targets(array.ElementType, depth + 1))
                {
                    yield return t;
                }

                break;
        }
    }

    private static string? IdOf(ISymbol symbol)
    {
        var doc = symbol.GetDocumentationCommentId();
        return doc is null ? null : (symbol.ContainingAssembly?.Name ?? "?") + "|" + doc;
    }

    private static string KindOf(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol => "type",
        IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } => "ctor",
        IMethodSymbol => "method",
        IPropertySymbol => "property",
        IFieldSymbol => "field",
        IEventSymbol => "event",
        _ => "member",
    };

    private static bool IsPublic(ISymbol symbol)
    {
        for (var s = symbol; s is not null and not INamespaceSymbol; s = s.ContainingSymbol)
        {
            if (s.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTestMethod(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            for (var c = attribute.AttributeClass; c is not null; c = c.BaseType)
            {
                if (TestAttributes.Contains(c.Name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string TestNameOf(IMethodSymbol method)
    {
        var typeNames = new List<string>();
        for (var t = method.ContainingType; t is not null; t = t.ContainingType)
        {
            typeNames.Insert(0, t.Name);
        }

        var ns = method.ContainingNamespace is { IsGlobalNamespace: false } n ? n.ToDisplayString() + "." : string.Empty;
        return ns + string.Join("+", typeNames) + "." + method.Name;
    }

    private static Span SpanOf(SyntaxNode decl, string file)
    {
        // Start at the documentation comment, so a change to it belongs to the member it describes.
        var start = decl.SpanStart;
        foreach (var trivia in decl.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                start = trivia.FullSpan.Start;
                break;
            }
        }

        var lines = decl.SyntaxTree.GetLineSpan(TextSpan.FromBounds(start, decl.Span.End));
        return new Span(file, lines.StartLinePosition.Line + 1, lines.EndLinePosition.Line + 1);
    }

    private static bool IsGeneratedFile(string root, string rel, SyntaxNode treeRoot)
    {
        if (rel.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || rel.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (File.Exists(Path.Combine(root, Path.ChangeExtension(rel, ".tt"))))
        {
            return true; // Output of a T4 template that sits next to it.
        }

        var head = treeRoot.GetLeadingTrivia().ToFullString();
        return head.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase);
    }
}
