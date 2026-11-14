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
