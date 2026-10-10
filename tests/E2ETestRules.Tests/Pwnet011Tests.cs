using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet011Tests
{
    private static readonly Pwnet011_AvoidConditionalLogicInTestsAnalyzer Analyzer = new();

    private static string TestClass(string body) => $$"""
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Microsoft.Playwright;
        using Xunit;
        class T
        {
            [Fact]
            public async Task Checkout(ILocator loc, bool premium, int n, List<int> items, string? name)
            {
                {{body}}
                await Task.CompletedTask;
            }
        }
        """;

    [Theory]
    [InlineData("if (premium) { n++; } else { n--; }")]
    [InlineData("switch (n) { case 1: break; default: break; }")]
    [InlineData("var x = premium ? 1 : 2;")]
    [InlineData("var x = n switch { 1 => \"a\", _ => \"b\" };")]
    [InlineData("foreach (var i in items) { n += i; }")]
    [InlineData("for (var i = 0; i < 3; i++) { n++; }")]
    [InlineData("while (n < 3) { n++; }")]
    public async Task Reports_branch_or_loop_in_test(string body)
    {
        var d = Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, TestClass(body)));

        Assert.Equal("PWNET011", d.Id);
        Assert.Contains("'Checkout' contains 1 branch", d.GetMessage());
    }

    [Fact]
    public async Task Reports_once_per_test_with_total_count()
    {
        var body = """
            if (premium) { n++; }
            else if (n > 2) { n--; }
            foreach (var i in items) { n += i; }
            """;

        var d = Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, TestClass(body)));

        Assert.Contains("contains 3 branch", d.GetMessage());
    }

    [Fact]
    public async Task Ignores_value_fallbacks_and_straight_line_tests()
    {
        var body = """
            var label = name ?? "guest";
            var ok = premium && n > 0 || n < -1;
            var len = name?.Length;
            await loc.ClickAsync();
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, TestClass(body)));
    }

    [Theory]
    [InlineData("if (await loc.IsVisibleAsync()) { await loc.ClickAsync(); }", "IsVisibleAsync")]
    [InlineData("var count = await loc.CountAsync(); if (count > 0) { n++; }", "CountAsync")]
    [InlineData("while (!await loc.IsVisibleAsync()) { n++; }", "IsVisibleAsync")]
    public async Task Reports_state_dependent_branch_in_test_only_once(string body, string read)
    {
        var d = Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, TestClass(body)));

        Assert.Contains("depends on browser state read once via " + read, d.GetMessage());
    }

    [Fact]
    public async Task Page_objects_report_state_dependent_branches_but_not_plain_logic()
    {
        const string source = """
            using System.Threading.Tasks;
            using Microsoft.Playwright;
            public class CheckoutPage
            {
                private readonly IPage _page;
                public CheckoutPage(IPage page) => _page = page;

                public async Task DismissCookieBannerAsync()
                {
                    var banner = _page.GetByRole("dialog");
                    if (await banner.IsVisibleAsync()) { await banner.ClickAsync(); }
                }

                public ILocator Row(int index, bool header) => header ? _page.GetByRole("columnheader") : _page.GetByRole("row");

                public async Task FillAsync(string[] values)
                {
                    foreach (var v in values) { await _page.GetByLabel(v).ClickAsync(); }
                }
            }
            """;

        var d = Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, source));

        Assert.Contains("IsVisibleAsync", d.GetMessage());
    }
}
