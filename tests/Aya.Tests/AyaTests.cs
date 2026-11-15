using Aya;
using Xunit;

namespace Aya.Tests;

/// <summary>
/// One test for every way the tool was found to say more than it had measured. Each of these was a defect once.
/// </summary>
public class OrderingRuleTests
{
    private static Signals Unit(
        bool modified = false,
        int dependents = 0,
        bool broke = false,
        bool changed = false,
        bool gap = false,
        bool notMeasured = false,
        bool possibleDefect = false,
        bool blind = false) =>
        new(modified, dependents, broke, changed, gap, notMeasured, possibleDefect, blind);

    [Fact]
    public void A_demonstrated_break_comes_before_everything_else() =>
        Assert.Equal(1, Attention.Tier(Unit(broke: true, changed: true, gap: true, notMeasured: true)));

    [Fact]
    public void A_changed_result_comes_second() =>
        Assert.Equal(2, Attention.Tier(Unit(modified: true, dependents: 9, changed: true, gap: true)));

    [Fact]
    public void Existing_code_with_dependents_and_a_gap_comes_third() =>
        Assert.Equal(3, Attention.Tier(Unit(modified: true, dependents: 1, gap: true)));

    [Fact]
    public void What_could_not_be_measured_is_never_ranked_as_no_signal()
    {
        Assert.Equal(4, Attention.Tier(Unit(notMeasured: true)));
        Assert.Equal(4, Attention.Tier(Unit(blind: true)));
        Assert.Equal(4, Attention.Tier(Unit(possibleDefect: true)));
    }

    [Fact]
    public void A_gap_in_new_code_or_in_code_nothing_depends_on_comes_fifth()
    {
        Assert.Equal(5, Attention.Tier(Unit(gap: true)));
        Assert.Equal(5, Attention.Tier(Unit(modified: true, dependents: 0, gap: true)));
    }

    [Fact]
    public void Only_a_unit_with_nothing_against_it_reaches_the_last_tier() =>
        Assert.Equal(6, Attention.Tier(Unit(modified: true, dependents: 50)));
}

public class OldAgainstNewTests
{
    private static Dictionary<string, string> Run(params (string Case, string Value)[] rows) =>
        rows.ToDictionary(r => r.Case, r => r.Value, StringComparer.Ordinal);

    [Fact]
    public void A_case_the_old_code_records_differently_twice_is_discarded()
    {
        var sorted = Comparer.Sort(Run(("clock", "10:00"), ("sum", "4")), Run(("clock", "10:01"), ("sum", "4")), Run(("clock", "10:02"), ("sum", "4")));

        Assert.Equal(1, sorted.Unstable);
        Assert.Equal(1, sorted.Same);
        Assert.Empty(sorted.Differences);
    }

    [Fact]
    public void Each_kind_of_difference_is_told_apart()
    {
        var old = Run(("a", "1"), ("b", "2"), ("c", "EXCEPTION System.Exception: x"), ("d", "EXCEPTION System.Exception: x"), ("e", "5"), ("f", "6"));
        var head = Run(("a", "1"), ("b", "EXCEPTION System.Exception: y"), ("c", "3"), ("d", "EXCEPTION System.Exception: z"), ("e", "50"), ("g", "7"));

        var sorted = Comparer.Sort(old, old, head);
        var kinds = sorted.Differences.ToDictionary(d => d.Case, d => d.Kind);

        Assert.Equal(1, sorted.Same);
        Assert.Equal("now-fails", kinds["b"]);
        Assert.Equal("now-works", kinds["c"]);
        Assert.Equal("fails-differently", kinds["d"]);
        Assert.Equal("changed", kinds["e"]);
        Assert.Equal("missing-on-new", kinds["f"]);
        Assert.Equal(1, sorted.OnlyOnNew);
    }

    [Fact]
    public void The_same_alteration_on_different_inputs_falls_in_one_group()
    {
        var old = Run(("x", "class A\n{\n}"), ("y", "class B\n{\n}"), ("z", "1"));
        var head = Run(("x", "[Attr]\nclass A\n{\n}"), ("y", "[Attr]\nclass B\n{\n}"), ("z", "2"));

        var sorted = Comparer.Sort(old, old, head);

        Assert.Equal(3, sorted.Differences.Count);
        Assert.Equal(sorted.Differences.Single(d => d.Case == "x").Group, sorted.Differences.Single(d => d.Case == "y").Group);
        Assert.NotEqual(sorted.Differences.Single(d => d.Case == "x").Group, sorted.Differences.Single(d => d.Case == "z").Group);
    }

    [Fact]
    public void A_stack_trace_that_only_moved_lines_is_not_a_difference()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "net8\tboom\tEXCEPTION X: bad, see T.cs:line 41\\n   at T.Run() in T.cs:line 41\nnet8\tboom2\tEXCEPTION X: bad, see T.cs:line 97\\n   at T.<Run>b__3() in T.cs:line 97\n");
            var records = Comparer.Read(file, "C:\\none");

            Assert.Equal(records["boom"], records["boom2"]);
            Assert.Contains("EXCEPTION X: bad, see T.cs:line N", records["boom"]);
            Assert.DoesNotContain("T.Run()", records["boom"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Machine_paths_do_not_make_two_copies_of_the_code_look_different()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "net8\tcase one\tC:\\\\wt\\\\old\\\\src\\\\a.cs(3,1): error\nnet8\tcase one\tsecond\n");
            var records = Comparer.Read(file, "C:\\wt\\old");

            Assert.Equal("<repo>\\src\\a.cs(3,1): error", records["case one"]);
            Assert.Equal("second", records["case one #2"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void A_folder_given_with_mixed_slashes_and_a_build_stamped_version_do_not_make_a_difference()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                file,
                "net8\ttrace\tfailed in C:\\\\wt\\\\old\\\\src\\\\a.cs:line 31\n" +
                "net8\tclash\texists in 'Gen, Version=3.0.287.12837, Culture=neutral'\n" +
                "net8\tframes\tSystem.InvalidCastException: no\\n   at Gen.<>c__DisplayClass0_0.<Initialize>g__Collect|8(ITypeSymbol t) in C:\\\\wt\\\\old\\\\src\\\\g.cs:line 9\\n   at Gen.Run()\\nlook at this (please)\n");
            var records = Comparer.Read(file, "C:/wt\\old");

            Assert.Equal("failed in <repo>\\src\\a.cs:line N", records["trace"]);
            Assert.Equal("exists in 'Gen, Version=N, Culture=neutral'", records["clash"]);
            Assert.Equal("System.InvalidCastException: no\nlook at this (please)", records["frames"]);
        }
        finally
        {
            File.Delete(file);
        }
    }
}

public class SimilarIsNotIdenticalTests
{
    private const string Diff = """
        diff --git a/one.cs b/one.cs
        index 111..222 100644
        --- a/one.cs
        +++ b/one.cs
        @@ -1,0 +2,1 @@
        +[Attr]
        diff --git a/two.cs b/two.cs
        index 111..222 100644
        --- a/two.cs
        +++ b/two.cs
        @@ -1,0 +2,1 @@
        +[Attr]
        @@ -9,0 +11,1 @@
        +    [Attr]
        """;

    [Fact]
    public void A_file_with_the_edit_once_and_one_with_it_twice_are_similar_but_two_variants()
    {
        var files = DiffParser.Parse(Diff.Replace("\r", string.Empty));
        var groups = DiffParser.GroupPatterns(files);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Files.Count);
        Assert.Equal(2, group.Variants.Count);
        Assert.NotEqual(files[0].ExactPattern, files[1].ExactPattern);
    }
}

public class TestOutcomeTests
{
    private static TestRun RunWith(params string[] outcomes) => new()
    {
        Label = "head",
        Project = "p",
        Built = true,
        Results = outcomes.Select((o, i) => new TestResult("p", "T" + i, o, 1, null)).ToList(),
    };

    [Fact]
    public void A_skipped_test_is_neither_passed_nor_failed()
    {
        var run = RunWith("NotExecuted");

        Assert.Equal(0, run.PassedCount);
        Assert.Equal(0, run.FailedCount);
        Assert.Equal(1, run.SkippedCount);
        Assert.Equal(0, run.Summary().Passed);
    }

    [Fact]
    public void Outcomes_are_counted_from_each_result_and_not_by_subtraction()
    {
        var run = RunWith("Passed", "Failed", "NotExecuted", "Inconclusive", "Passed");

        Assert.Equal(2, run.PassedCount);
        Assert.Equal(1, run.FailedCount);
        Assert.Equal(2, run.SkippedCount);
    }

    [Fact]
    public void A_test_run_for_several_frameworks_fails_if_it_fails_on_any_of_them()
    {
        var merged = TestRunner.Merge(
        [
            new TestResult("p", "T.Works", "Passed", 1, null),
            new TestResult("p", "T.Works", "Passed", 1, null),
            new TestResult("p", "T.Breaks", "Passed", 1, null),
            new TestResult("p", "T.Breaks", "Failed", 1, "only on the second framework"),
            new TestResult("p", "T.Sometimes", "NotExecuted", 1, null),
            new TestResult("p", "T.Sometimes", "Passed", 1, null),
            new TestResult("p", "T.Never", "NotExecuted", 1, null),
        ]);

        Assert.Equal(4, merged.Count);
        Assert.True(merged.Single(r => r.Name == "T.Works").Passed);
        Assert.True(merged.Single(r => r.Name == "T.Breaks").Failed);
        Assert.True(merged.Single(r => r.Name == "T.Sometimes").Passed);
        Assert.False(merged.Single(r => r.Name == "T.Never").Passed);
    }

    [Fact]
    public void A_run_stopped_at_the_time_limit_is_not_a_compile_failure()
    {
        var run = new TestRun { Label = "head", Project = "p", Built = true, TimedOut = true, ExitCode = -1 };

        Assert.True(run.Built);
        Assert.Equal("stopped at the time limit", run.Problem);
    }

    [Fact]
    public void A_run_that_ends_in_error_without_results_is_reported_as_such()
    {
        var run = new TestRun { Label = "head", Project = "p", Built = true, ExitCode = 1 };

        Assert.True(run.Crashed);
        Assert.NotNull(run.Problem);
    }

    [Fact]
    public void Trx_outcomes_are_read_as_written()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                  <Results>
                    <UnitTestResult testName="A.Skipped" outcome="NotExecuted" duration="00:00:00.0010000" />
                    <UnitTestResult testName="A.Ok" outcome="Passed" duration="00:00:00.0010000" />
                  </Results>
                </TestRun>
                """);
            var results = TestRunner.ParseTrx(file, "p").ToList();

            Assert.False(results.Single(r => r.Name == "A.Skipped").Passed);
            Assert.False(results.Single(r => r.Name == "A.Skipped").Failed);
            Assert.True(results.Single(r => r.Name == "A.Ok").Passed);
        }
        finally
        {
            File.Delete(file);
        }
    }
}

public class ModelWrittenCodeTests
{
    [Theory]
    [InlineData("var o = RuntimeHelpers.GetUninitializedObject(type);")]
    [InlineData("type.GetMethod(\"X\", BindingFlags.Instance | BindingFlags.NonPublic)")]
    [InlineData("using System.Reflection.Emit;")]
    public void Code_that_forces_an_unreachable_state_is_refused(string code) =>
        Assert.NotNull(ModelSession.Trick(code));

    [Fact]
    public void Ordinary_test_code_is_not_refused() =>
        Assert.Null(ModelSession.Trick("var assembly = typeof(Foo).Assembly; Assert.Equal(1, Foo.Bar());"));
}

public class AttackArbitrationTests
{
    private static AttackResult Request(string id) => new() { Request = id, Symbol = "s", SymbolId = "i", File = "f.cs", Lines = [1] };

    [Fact]
    public void A_test_result_belongs_to_the_request_whose_class_it_is_in()
    {
        Assert.True(Attacker.Owns(Request("A1"), "AyaA1Attack.Breaks"));
        Assert.True(Attacker.Owns(Request("A1"), "Some.Namespace.AyaA1Attack.Breaks(x: 1)"));
        Assert.False(Attacker.Owns(Request("A1"), "Some.Namespace.AyaA11Attack.Breaks"));
        Assert.False(Attacker.Owns(Request("A1"), "AyaA2Attack.Breaks"));
    }

    [Fact]
    public void A_compiler_error_is_pinned_on_the_file_it_names_and_no_other()
    {
        const string error = @"tests\P\AyaCandidates\A1.cs(12,5): error CS0103: The name 'x' does not exist";

        Assert.True(Attacker.Mentions(error, "A1"));
        Assert.False(Attacker.Mentions(error.Replace("A1.cs", "A11.cs"), "A1"));
        Assert.False(Attacker.Mentions(error, "A2"));
    }
}

public class NewOrModifiedTests
{
    private const string Old = """
        class Collector
        {
            public Collector(int a) { }
            public int Count { get; set; }
            int Find(string name) => 0;
            int Find(string name, bool exact) => 0;
            class Inner { void Run() { } }
        }
        record Item(string Key);
        """;

    [Theory]
    [InlineData("Collector.Find(string)", "method", true)]
    [InlineData("Collector.Find(string, bool)", "method", true)]
    [InlineData("Collector.Find(string, bool, int)", "method", false)]
    [InlineData("Collector.Report(Action<Diagnostic>, Diagnostic)", "method", false)]
    [InlineData("Collector.Collector(int)", "ctor", true)]
    [InlineData("Collector.Count", "property", true)]
    [InlineData("Collector.Total", "property", false)]
    [InlineData("Collector.Inner.Run()", "method", true)]
    [InlineData("Collector", "type", true)]
    [InlineData("Item.Key", "property", true)]
    [InlineData("Item.Item(string)", "ctor", true)]
    [InlineData("Brand", "type", false)]
    public void A_unit_existed_before_only_if_the_old_file_declares_it(string display, string kind, bool existed)
    {
        var before = Declarations.In(Old.Replace("\r", string.Empty));

        Assert.Equal(existed, before.Contains(Declarations.KeyOf(display, kind)!));
    }

    [Fact]
    public void A_parameterless_constructor_written_for_the_first_time_is_new()
    {
        var before = Declarations.In("class Shop { public Shop(int size) { } }");

        Assert.False(Declarations.Declares(before, Declarations.KeyOf("Shop.Shop()", "ctor")!));
        Assert.True(Declarations.Declares(before, Declarations.KeyOf("Shop.Shop(int)", "ctor")!));
    }

    [Fact]
    public void A_member_shown_under_an_interface_name_is_still_found_in_its_class()
    {
        var before = Declarations.In("class Shop : IOpen { void IOpen.Open(int hour) { } }");

        Assert.True(Declarations.Declares(before, Declarations.KeyOf("Shop.IOpen.Open(int)", "method")!));
    }

    [Theory]
    [InlineData("Money.operator +(Money, Money)", "method")]
    [InlineData("Table.this[int]", "property")]
    public void Names_that_syntax_cannot_match_with_confidence_conclude_nothing(string display, string kind) =>
        Assert.Null(Declarations.KeyOf(display, kind));
}

public class DeliberateBreakTests
{
    private const string Code = """
        class C
        {
            int F(int a, bool b)
            {
                if (a > 0 && b)
                {
                    return G(a);
                }

                return a + 1;
            }

            int G(int x) => x * 2;
        }
        """;

    [Fact]
    public void Each_changed_line_gets_the_breaks_it_can_take()
    {
        var text = Code.Replace("\r", string.Empty);
        var breaks = Mutator.Generate(text, "c.cs", new HashSet<int> { 5, 7, 10, 13 }).Select(m => $"{m.Mutant.Line}: {m.Mutant.Description}").ToList();

        Assert.Contains("5: && → ||", breaks);
        Assert.Contains("5: a && b → a", breaks);
        Assert.Contains("5: a && b → b", breaks);
        Assert.Contains("7: return x → return default", breaks);
        Assert.Contains("10: + → -", breaks);
        Assert.Contains("13: => x → => default", breaks);
    }

    [Fact]
    public void A_break_is_only_made_on_the_lines_asked_for()
    {
        var breaks = Mutator.Generate(Code.Replace("\r", string.Empty), "c.cs", new HashSet<int> { 13 });

        Assert.All(breaks, m => Assert.Equal(13, m.Mutant.Line));
    }

    [Fact]
    public void Every_break_changes_the_text()
    {
        var text = Code.Replace("\r", string.Empty);

        Assert.All(Mutator.Generate(text, "c.cs", new HashSet<int> { 5, 7, 10, 13 }), m => Assert.NotEqual(text, m.Text));
    }
}

/// <summary>What "explained by the change's own text" may and may not say.</summary>
public class ChangeTextTests
{
    private static readonly ChangeText Change = new(
        ["        [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]", "    return value == true;"],
        ["    private const string OldBanner = \"generated by version one\";"]);

    private static Dictionary<string, string> Run(params (string Case, string Value)[] rows) =>
        rows.ToDictionary(r => r.Case, r => r.Value, StringComparer.Ordinal);

    [Fact]
    public void A_difference_made_only_of_a_line_the_change_added_is_the_change_showing_through() =>
        Assert.True(Change.Explains("class A\n{\n}", "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]\nclass A\n{\n}"));

    [Fact]
    public void A_line_that_went_must_be_one_the_change_removed()
    {
        Assert.True(Change.Explains("private const string OldBanner = \"generated by version one\";\nclass A { }", "class A { }"));
        Assert.False(Change.Explains("class A { }\nMsgPack003 the type must be annotated", "class A { }"));
    }

    [Fact]
    public void One_line_the_change_did_not_write_leaves_the_whole_difference_unexplained() =>
        Assert.False(Change.Explains("class A\n{\n}", "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]\nclass A : IDisposable\n{\n}"));

    [Fact]
    public void A_short_value_is_never_explained_even_if_the_change_contains_it()
    {
        // "true" is inside a line the change added, and so is almost any short value inside almost any change.
        Assert.False(Change.Explains("false", "true"));
        Assert.False(Change.Explains("{", "}"));
    }

    [Fact]
    public void A_short_line_is_explained_only_when_the_change_has_that_very_line()
    {
        var guarded = new ChangeText(["#if !NETSTANDARD2_0 && !NETFRAMEWORK", "    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]", "#endif", "if (done) { return true; }"], []);

        Assert.True(guarded.Explains("class A { }", "#if !NETSTANDARD2_0 && !NETFRAMEWORK\n[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]\n#endif\nclass A { }"));
        Assert.False(guarded.Explains("class A { }", "#endif\nreturn\nclass A { }"));
    }

    [Fact]
    public void The_same_edit_repeated_a_different_number_of_times_is_one_alteration_shown_once()
    {
        var old = Run(("five", "class A\nclass B"), ("seven", "class A\nclass B\nclass C"), ("z", "1"));
        var head = Run(("five", "[Attr]\nclass A\n\t[Attr]\nclass B"), ("seven", "[Attr]\nclass A\n[Attr]\nclass B\n[Attr]\nclass C\nERROR CS0579: Duplicate"), ("z", "1"));

        var differences = Comparer.Sort(old, old, head).Differences;

        Assert.Equal("+ [Attr]   (×2)", differences.Single(d => d.Case == "five").Delta);
        Assert.StartsWith("+ ERROR CS0579: Duplicate", differences.Single(d => d.Case == "seven").Delta);
        Assert.NotEqual(differences[0].Group, differences[1].Group);
        Assert.Equal(
            Comparer.Sort(old, old, Run(("five", "[Attr]\nclass A\n[Attr]\nclass B"), ("seven", "[Attr]\nclass A\n[Attr]\nclass B\n[Attr]\nclass C"), ("z", "1"))).Differences.Select(d => d.Group).Distinct().Count(),
            1);
    }

    [Fact]
    public void Only_a_result_that_became_another_result_can_be_explained()
    {
        var old = Run(("a", "class A\n{\n}"), ("b", "class B { }"), ("c", "3"));
        var head = Run(("a", "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]\nclass A\n{\n}"), ("b", "EXCEPTION System.Exception: [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]"), ("c", "4"));

        var by = Comparer.Sort(old, old, head, Change).Differences.ToDictionary(d => d.Case, d => d.ByChange);

        Assert.Equal("explained", by["a"]);
        Assert.Null(by["b"]);
        Assert.Equal("unexplained", by["c"]);
    }

    [Fact]
    public void In_a_sweep_an_input_that_gains_an_error_line_no_longer_works()
    {
        var old = Run(("ok", "OUT a.g.cs\nclass A { }"), ("bad", "ERROR CS0246: not found"), ("worse", "ERROR CS0246: not found"), ("same", "OUT a.g.cs\nclass A { }"));
        var head = Run(("ok", "OUT a.g.cs\nclass A { }\nERROR CS0579: Duplicate attribute"), ("bad", "OUT a.g.cs"), ("worse", "ERROR CS0103: no such name"), ("same", "OUT a.g.cs\nclass A { } // v2"));

        var kinds = Comparer.Sort(old, old, head, Change, errorLines: true).Differences.ToDictionary(d => d.Case, d => d.Kind);

        Assert.Equal("now-fails", kinds["ok"]);
        Assert.Equal("now-works", kinds["bad"]);
        Assert.Equal("fails-differently", kinds["worse"]);
        Assert.Equal("changed", kinds["same"]);
    }
}

/// <summary>The inputs of a sweep are built by rule, and every one says where it came from.</summary>
public class SweepInputTests
{
    private const string Source = """
        using MessagePack;

        [MessagePackObject]
        public partial class Outer
        {
            [Key(0)]
            public int[] Values { get; set; } = new int[0];

            public class Inner { }
        }

        public static class Helpers { }
        """;

    [Fact]
    public void The_attributes_come_from_the_lines_the_change_added_and_nothing_else()
    {
        var found = SweepInputs.Attributes(
            [
                "sb.AppendLine(\"[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]\");",
                "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]",
                "var first = values[Index] + other[0];",
                "sb.Append(\"[BrandNewThing(3)] public int X;\");",
                "[Obsolete] void M() { }",
                "[AttributeUsage(AttributeTargets.Class)]",
                "[Serializable]",
            ],
            ["BrandNewThingAttribute"],
            new HashSet<string> { "[Serializable]" });

        // Written out by the code (inside a string, or a line of a template): kept. Used by the change on its own
        // declarations ([Obsolete], [AttributeUsage]) or declared by it ([BrandNewThing]): not what users receive.
        Assert.Equal(["[Serializable]", "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]"], found);
    }

    [Fact]
    public void An_attribute_is_put_on_every_type_and_the_result_is_still_source()
    {
        var altered = SweepInputs.WithAttribute(Source.Replace("\r", string.Empty), "[System.Obsolete]")!;

        Assert.Equal(3, altered.Split("[System.Obsolete] ").Length - 1);
        Assert.Contains("[System.Obsolete] [MessagePackObject]", altered);
        Assert.Contains("[System.Obsolete] public class Inner", altered);
        Assert.True(SweepInputs.IsSource(altered));
    }

    [Fact]
    public void The_fixed_alterations_touch_only_what_they_name()
    {
        var text = Source.Replace("\r", string.Empty);

        Assert.Contains("public class Outer", SweepInputs.WithoutPartial(text));
        Assert.Contains("internal partial class Outer", SweepInputs.MadeInternal(text));
        Assert.Contains("public class Inner", SweepInputs.MadeInternal(text));
        Assert.Contains("public sealed partial class Outer", SweepInputs.MadeSealed(text));
        Assert.Contains("public static class Helpers", SweepInputs.MadeSealed(text));
        Assert.Null(SweepInputs.WithoutPartial("public class A { }"));
    }

    [Fact]
    public void Text_that_is_not_a_source_file_is_not_an_input()
    {
        Assert.False(SweepInputs.IsSource("the class of 1999 was a good one"));
        Assert.False(SweepInputs.IsSource("public class {|MsgPack003:A|} { }"));
        Assert.False(SweepInputs.IsSource("int x = 3;"));
    }

    [Fact]
    public void Inputs_are_taken_as_they_are_first_then_altered_and_never_past_the_limit()
    {
        var corpus = new List<(string Origin, string Text)> { ("A.cs:3", "public partial class A { }"), ("B.cs:9", "public class B { }") };
        var inputs = SweepInputs.Build(corpus, SweepInputs.Alterations(["[System.Obsolete]"]), 5);

        Assert.Equal(["i001", "i002", "i001+a1", "i002+a1"], inputs.Take(4).Select(i => i.Id));
        Assert.Equal(5, inputs.Count);
        Assert.Equal("i001 A.cs:3", inputs[0].Name);
        Assert.Null(inputs[0].Alteration);
        Assert.Equal("i001", inputs[2].BasedOn);
        Assert.StartsWith("[System.Obsolete] ", inputs[2].Text);
        Assert.NotNull(inputs[4].Alteration);
        Assert.NotEqual(inputs[4].Alteration, inputs[2].Alteration);
    }

    [Fact]
    public void Sources_are_found_in_the_string_literals_of_the_tests()
    {
        var folder = Path.Combine(Path.GetTempPath(), "aya-corpus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "obj"));
        try
        {
            var q = new string('"', 3);
            File.WriteAllText(Path.Combine(folder, "T.cs"), $"class T {{ string a = {q}\n    public partial class FromTest {{ public int Value; }}\n    {q}; string b = \"not a source file at all, just some words in a row\"; }}");
            File.WriteAllText(Path.Combine(folder, "obj", "G.cs"), "class G { string a = \"public class Generated { public int Value; public int Other; }\"; }");

            var corpus = SweepInputs.Corpus(folder);

            Assert.Single(corpus);
            Assert.Equal("T.cs:1", corpus[0].Origin);
            Assert.Contains("class FromTest", corpus[0].Text);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_difference_seen_only_on_the_altered_input_is_marked()
    {
        var inputs = SweepInputs.Build([("A.cs:3", "public partial class A { }"), ("B.cs:9", "public partial class B { }")], SweepInputs.Alterations(["[System.Obsolete]"]), 4);
        var old = inputs.ToDictionary(i => i.Name, _ => "OUT a.g.cs\nclass G { }", StringComparer.Ordinal);
        var head = inputs.ToDictionary(i => i.Name, i => i.Id switch
        {
            "i001" => "OUT a.g.cs\nclass G { }",
            "i001+a1" => "OUT a.g.cs\nclass G { }\nERROR CS0579: Duplicate attribute",
            _ => "OUT a.g.cs\nclass G2 { }",
        }, StringComparer.Ordinal);
        var run = new Comparison { Request = "S1", Purpose = "sweep", Symbol = "s", SymbolId = "s", File = "f", Lines = [] };
        run.Differences = Comparer.Sort(old, old, head, errorLines: true).Differences;

        Comparer.Attribute(run, inputs);
        var by = run.Differences.ToDictionary(d => d.Case.Split(' ')[0]);

        Assert.True(by["i001+a1"].OnlyWhenAltered);
        Assert.Equal("now-fails", by["i001+a1"].Kind);
        Assert.StartsWith("the input of an existing test (A.cs:3), altered:", by["i001+a1"].Input);
        Assert.False(by["i002+a1"].OnlyWhenAltered);
        Assert.Equal("taken from an existing test (B.cs:9)", by["i002"].Input);
    }
}

/// <summary>A model's reading is kept only for what it can show, and never as more than a reading.</summary>
public class JudgeTests
{
    private const string Purpose = "### Commit message(s)\nMark all generated code as excluded from code coverage.\n";
    private const string Diff = "+        if (symbol.Locations.Length == 0) { return; }";

    [Fact]
    public void A_reading_of_intended_stands_only_on_words_the_change_says_about_itself()
    {
        var answer = """
            Here is my reading.
            ```
            [
              {"group": "aaa111", "verdict": "intended", "quote": "Mark all generated code as excluded   from code coverage", "reason": "It is the purpose."},
              {"group": "bbb222", "verdict": "intended", "quote": "if (symbol.Locations.Length == 0) { return; }", "reason": "The code does it."},
              {"group": "ccc333", "verdict": "intended", "quote": "the issue asks for this", "reason": "Made up."},
              {"group": "ddd444", "verdict": "not-intended", "quote": "if (symbol.Locations.Length == 0) { return; }", "reason": "A side effect."},
              {"group": "eee555", "verdict": "not-intended", "quote": "", "reason": "Nothing asks for it."},
              {"group": "fff666", "verdict": "certainly fine", "quote": "", "reason": "?"}
            ]
            ```
            """;

        var readings = Judge.Parse(answer, Purpose, Diff, out var problem);

        Assert.Null(problem);
        Assert.Equal("intended", readings["aaa111"].Verdict);
        Assert.Equal("quote-not-found", readings["bbb222"].Verdict);
        Assert.Equal("quote-not-found", readings["ccc333"].Verdict);
        Assert.Equal("not-intended", readings["ddd444"].Verdict);
        Assert.NotEqual(string.Empty, readings["ddd444"].Quote);
        Assert.Equal("not-intended", readings["eee555"].Verdict);
        Assert.False(readings.ContainsKey("fff666"));
    }

    [Fact]
    public void An_answer_that_is_not_the_asked_for_list_gives_no_reading()
    {
        Assert.Empty(Judge.Parse("I think it is all fine.", Purpose, Diff, out var none));
        Assert.NotNull(none);
        Assert.Empty(Judge.Parse("[{\"group\": \"a\", \"verdict\": }]", Purpose, Diff, out var broken));
        Assert.NotNull(broken);
    }
}

/// <summary>Putting one part of a change back as it was must give exactly the old lines in that place.</summary>
public class UndoTests
{
    private const string New = "a\nb2\nb3\nc\nd";

    [Fact]
    public void A_replaced_part_gets_its_old_lines_back()
    {
        var hunk = new Hunk { OldStart = 2, OldLen = 1, NewStart = 2, NewLen = 2 };
        hunk.Removed.Add("b");

        Assert.Equal("a\nb\nc\nd", MutationRunner.Undone(New, hunk));
    }

    [Fact]
    public void An_added_part_is_taken_out_and_a_deleted_part_is_put_back_after_the_line_the_diff_names()
    {
        var added = new Hunk { OldStart = 1, OldLen = 0, NewStart = 2, NewLen = 2 };
        var deleted = new Hunk { OldStart = 4, OldLen = 2, NewStart = 4, NewLen = 0 };
        deleted.Removed.AddRange(["x", "y"]);

        Assert.Equal("a\nc\nd", MutationRunner.Undone(New, added));
        Assert.Equal("a\nb2\nb3\nc\nx\ny\nd", MutationRunner.Undone(New, deleted));
    }
}

/// <summary>A reading of the requirement is kept only where it rests on the requirement's words and on a measured test.</summary>
public class RequirementTests
{
    [Fact]
    public void Demonstrated_stands_only_on_a_test_that_failed_on_the_old_code()
    {
        var answer = """
            [
              {"asks": "the analyzer must not   crash", "status": "demonstrated", "test": "CrashTests.NoCrash", "reason": "It fails on the old code."},
              {"asks": "the resolver is generated", "status": "demonstrated", "test": "CrashTests.PassesAnyway", "reason": "Named a weak test."},
              {"asks": "words that are not in the requirement", "status": "not-addressed", "test": "", "reason": "Invented."},
              {"asks": "the resolver is generated", "status": "splendid", "test": "", "reason": "Unknown status."}
            ]
            """;

        var list = Judge.ParseFit(answer, "The analyzer must not crash, and the resolver is generated.".ToLowerInvariant(), ["Ns.CrashTests.NoCrash"], out var dropped);

        Assert.Equal(2, list.Count);
        Assert.Equal(("demonstrated", "Ns.CrashTests.NoCrash"), (list[0].Status, list[0].Test));
        Assert.Equal("not-demonstrated", list[1].Status);
        Assert.Equal(1, dropped);
    }
}

/// <summary>The alterations that take an input to where the tests of a change rarely go.</summary>
public class InputLayerTests
{
    private const string Two = """
        using MessagePack;

        namespace App
        {
            [MessagePackObject]
            public class Holder
            {
                [Key(0)]
                public Item<int> First { get; set; }

                [Key(1)]
                public string Second { get; set; }
            }

            [MessagePackObject]
            internal struct Item<T>
            {
                [Key(0)]
                public T Value;
            }

            [GeneratedMessagePackResolver]
            partial class MyResolver { }
        }
        """;

    [Fact]
    public void The_types_other_types_use_go_to_a_library_and_become_visible_from_outside()
    {
        var split = SweepInputs.Split(Two.Replace("\r", string.Empty))!;
        var parts = split.Split("\n" + SweepInputs.SplitMark + "\n");

        Assert.Equal(2, parts.Length);
        Assert.Contains("public struct Item<T>", parts[0]);
        Assert.DoesNotContain("Holder", parts[0]);
        Assert.Contains("class Holder", parts[1]);
        Assert.Contains("MyResolver", parts[1]);
        Assert.DoesNotContain("struct Item", parts[1]);
        Assert.True(SweepInputs.IsInput(split));
        Assert.Null(SweepInputs.Split("public class Alone { }"));
    }

    [Fact]
    public void An_attribute_argument_is_repeated_changed_of_kind_or_made_null()
    {
        var text = Two.Replace("\r", string.Empty);

        Assert.Contains("[Key(0)]\n        public string Second", SweepInputs.Argument(text, "repeat"));
        Assert.Contains("[Key(\"k\")]\n        public string Second", SweepInputs.Argument(text, "kind"));
        Assert.Contains("[Key(null)]\n        public Item<int> First", SweepInputs.Argument(text, "null"));
        Assert.Null(SweepInputs.Argument("public class A { [Key(0)] public int X; }", "repeat"));
    }

    [Fact]
    public void A_type_another_type_uses_loses_its_attributes_and_the_others_keep_theirs()
    {
        var altered = SweepInputs.WithoutAttributesOnUsedTypes(Two.Replace("\r", string.Empty))!;

        Assert.DoesNotContain("[MessagePackObject]\n    internal struct Item", altered);
        Assert.Contains("[MessagePackObject]\n    public class Holder", altered);
        Assert.Contains("[GeneratedMessagePackResolver]", altered);
    }

    [Fact]
    public void Symbols_are_taken_from_the_conditions_the_change_writes_out_not_from_its_own()
    {
        var found = SweepInputs.Symbols(
            ["this.Write(\"#if !NETSTANDARD2_0 && !NETFRAMEWORK\r\n\");", "#if !NETSTANDARD2_0 && !NETFRAMEWORK", "#if NET8_0_OR_GREATER"],
            new HashSet<string> { "#if !NETSTANDARD2_0 && !NETFRAMEWORK" });

        Assert.Equal(["NETFRAMEWORK", "NETSTANDARD2_0"], found);
    }

    [Fact]
    public void The_marks_of_an_analyzer_test_are_taken_out_and_what_is_left_is_an_input()
    {
        var folder = Path.Combine(Path.GetTempPath(), "aya-marks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var q = new string('"', 3);
            File.WriteAllText(Path.Combine(folder, "T.cs"), $"class T {{ string a = {q}\n    public class Foo {{ public {{|MsgPack003:Bar|}} Member; [|public int X;|] }}\n    {q}; }}");

            var corpus = SweepInputs.Corpus(folder);

            Assert.Single(corpus);
            Assert.Contains("public Bar Member; public int X;", corpus[0].Text);
            Assert.EndsWith("marks removed", corpus[0].Origin);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

/// <summary>The general alterations hold for any C# source: each leaves an input that still parses, or declines.</summary>
public class FamilyTests
{
    private const string Source = """
        using System;

        namespace N
        {
            [Serializable]
            public abstract partial class A : IDisposable
            {
                [Obsolete]
                public int X { get; set; }

                private string y;

                public required virtual string Z { get; private set; } = "z";

                public void Dispose() { }
            }

            public readonly struct S { public readonly int V; }

            public sealed record R(int P);

            public static class H { public static int F; }

            public partial class Outer
            {
                public partial class Inner
                {
                    public class Innermost { public int A { get; set; } public int B; }

                    public int C { get; set; }
                }

                public int D;
            }
        }
        """;

    [Fact]
    public void Every_alteration_either_declines_or_leaves_a_source_that_parses_and_differs()
    {
        var text = Source.Replace("\r", string.Empty);
        var applied = 0;
        foreach (var (label, apply) in SweepInputs.Family())
        {
            var altered = apply(text);
            if (altered is null)
            {
                continue;
            }

            applied++;
            Assert.True(altered != text, label);
            Assert.True(SweepInputs.IsInput(altered), label + "\n" + altered);
        }

        Assert.True(applied >= 25, $"only {applied} alterations applied to a source that has one of everything");
    }

    [Fact]
    public void No_alteration_names_anything_but_csharp()
    {
        // The family must not know the library under test: a label that names one of its concepts would give it away.
        Assert.All(SweepInputs.Family(), f => Assert.DoesNotContain("MessagePack", f.Label, StringComparison.OrdinalIgnoreCase));
        Assert.All(SweepInputs.Family(), f => Assert.DoesNotContain("Key", f.Label, StringComparison.Ordinal));
        Assert.Equal(SweepInputs.Family().Count, SweepInputs.Family().Select(f => f.Label).Distinct().Count());
    }

    [Fact]
    public void The_budget_is_shared_among_the_general_alterations_instead_of_going_to_the_first()
    {
        var corpus = Enumerable.Range(0, 30).Select(i => ($"T.cs:{i}", Source.Replace("\r", string.Empty).Replace("class A", $"class A{i}"))).ToList();
        var inputs = SweepInputs.Build(corpus, SweepInputs.Alterations([]), 60);

        var per = inputs.Where(i => i.Alteration is not null).GroupBy(i => i.Alteration).Select(g => g.Count()).ToList();
        Assert.Equal(60, inputs.Count);
        Assert.True(per.Count >= 20, $"{per.Count} alterations got a share");
        Assert.True(per.Max() - per.Min() <= 1, "shares differ by more than one");
    }
}

/// <summary>The old code cannot judge a test about something it does not have.</summary>
public class EmptyPassTests
{
    private const string File_ = """
        public class AyaA1Attack
        {
            [Fact]
            public void OldBehaviourStillHolds()
            {
                // NewThingAttribute is mentioned here only in a comment.
                Assert.Equal(2, Diagnose("class A { }").Count);
            }

            [Fact]
            public void NewFeatureFallsBackSilently()
            {
                Assert.DoesNotContain("Secret", Generate("[NewThing(typeof(S[]))] class F { }"));
            }
        }
        """;

    private static AttackResult Attack(params (string Test, string OnOld)[] failing) => new()
    {
        Request = "A1", Symbol = "s", SymbolId = "s", File = "f", Lines = [], TestFile = "attacks/A1.cs", Status = "regression-demonstrated",
        Failing = failing.Select(f => new AttackFailure(f.Test, f.OnOld, "m")).ToList(),
    };

    private static void Run(AttackResult a)
    {
        var dir = Path.Combine(Path.GetTempPath(), "aya-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "attacks"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "attacks", "A1.cs"), File_);
            Attacker.WithoutEmptyPasses([a], dir, ["NewThingAttribute", "NewThing"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void A_pass_on_the_old_code_of_a_test_about_a_new_feature_is_taken_back()
    {
        var a = Attack(("AyaA1Attack.NewFeatureFallsBackSilently", "Passed"));
        Run(a);

        Assert.Equal("possible-defect", a.Status);
        Assert.Equal("not-arbitrated", a.Failing[0].OnOld);
    }

    [Fact]
    public void A_test_about_behaviour_that_existed_keeps_its_verdict_even_next_to_one_that_is_not()
    {
        var a = Attack(("AyaA1Attack.OldBehaviourStillHolds", "Passed"), ("AyaA1Attack.NewFeatureFallsBackSilently", "Passed"));
        Run(a);

        Assert.Equal("regression-demonstrated", a.Status);
        Assert.Equal(["Passed", "not-arbitrated"], a.Failing.Select(f => f.OnOld));
    }
}
