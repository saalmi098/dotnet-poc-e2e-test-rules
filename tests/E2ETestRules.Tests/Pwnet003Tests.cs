using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet003Tests
{
    private static readonly Pwnet003_RequireAssertionInTestAnalyzer Analyzer = new();

    [Theory]
    [InlineData("using Xunit;", "[Fact]")]
    [InlineData("using Xunit;", "[Theory]")]
    public async Task Reports_test_without_assertion(string usings, string attribute)
    {
        var source = $$"""
            {{usings}}
            class T { {{attribute}} public void Works() { var x = 1; } }
            """;

        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, source);

        Assert.Equal("PWNET003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Ignores_test_with_assertions()
    {
        const string source = """
            using Xunit;
            using System.Threading.Tasks;
            using Microsoft.Playwright;
            using static Microsoft.Playwright.Assertions;
            class T
            {
                [Fact] public void A() { Assert.True(true); }
                [Fact] public async Task B(ILocator l) { await Expect(l).ToBeVisibleAsync(); }
                [Fact] public void C() { ShouldBeFine(); }
                void ShouldBeFine() { }
            }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Ignores_non_test_methods()
    {
        const string source = "class T { public void NotATest() { var x = 1; } }";

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Reports_empty_test_bodies()
    {
        const string source = """
            using Xunit;
            class T
            {
                [Fact] public void Empty() { }
                [Fact] public void OnlyComment()
                {
                    // TODO: implement
                }
            }
            """;

        Assert.Equal(2, (await AnalyzerTestHelper.RunAsync(Analyzer, source)).Length);
    }

    [Fact]
    public async Task Ignores_skipped_and_expression_bodied_assertion()
    {
        const string source = """
            using Xunit;
            class T
            {
                [Fact(Skip = "later")] public void Empty() { }
                [Fact(Skip = "later")] public void Skipped() { var x = 1; }
                [Fact] public void SkipByName_SkipMe() { var x = 1; }
                [Fact] public void Expr() => Assert.True(true);
            }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Ignores_Assert_Skip_and_non_xUnit_test_attributes()
    {
        const string source = """
            using Xunit;
            class T
            {
                [Fact] public void A() { var x = 1; Assert.Skip("not ready"); }
                [OtherFramework.Test] public void B() { var x = 1; }
            }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }
}
