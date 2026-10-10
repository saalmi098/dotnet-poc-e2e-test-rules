using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet008Tests
{
    private static readonly Pwnet008_PreferWebFirstAssertionsAnalyzer Analyzer = new();

    private static string Wrap(string body) => $$"""
        using System.Threading.Tasks;
        using Microsoft.Playwright;
        using Xunit;
        using static Microsoft.Playwright.Assertions;
        class T { async Task M(IPage page, ILocator loc) { {{body}} } }
        """;

    [Theory]
    [InlineData("Assert.True(await loc.IsVisibleAsync());", "IsVisibleAsync", "ToBeVisibleAsync")]
    [InlineData("Assert.Equal(\"Saved\", await loc.TextContentAsync());", "TextContentAsync", "ToHaveTextAsync")]
    [InlineData("Assert.Equal(3, await loc.CountAsync());", "CountAsync", "ToHaveCountAsync")]
    [InlineData("Assert.Equal(\"x\", await loc.GetByLabel(\"Name\").InputValueAsync());", "InputValueAsync", "ToHaveValueAsync")]
    [InlineData("Assert.Equal(\"/home\", page.Url);", "Url", "ToHaveURLAsync")]
    [InlineData("Assert.Equal(\"Home\", await page.TitleAsync());", "TitleAsync", "ToHaveTitleAsync")]
    [InlineData("Assert.NotNull(await loc.GetAttributeAsync(\"href\"));", "GetAttributeAsync", "ToHaveAttributeAsync")]
    public async Task Reports_one_shot_reads_in_plain_assertions(string body, string read, string replacement)
    {
        var d = Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body)));

        Assert.Equal("PWNET008", d.Id);
        Assert.Contains(read, d.GetMessage());
        Assert.Contains(replacement, d.GetMessage());
    }

    [Fact]
    public async Task Reports_read_stored_in_local_then_asserted()
    {
        var body = """
            var text = await loc.TextContentAsync();
            Assert.Equal("Saved", text);
            """;

        Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body)));
    }

    [Fact]
    public async Task Ignores_web_first_assertions_and_non_browser_data()
    {
        var body = """
            await Expect(loc).ToBeVisibleAsync();
            var expected = 3;
            Assert.Equal(3, expected);
            Assert.True(expected > 0);
            await loc.ClickAsync();
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body)));
    }

    [Fact]
    public async Task Edge_ignores_reads_outside_assertions_and_same_names_on_other_types()
    {
        const string source = """
            using System.Threading.Tasks;
            using Microsoft.Playwright;
            using Xunit;
            class Api { public Task<int> CountAsync() => Task.FromResult(1); public string Url => "x"; }
            class T
            {
                async Task M(ILocator loc, Api api)
                {
                    if (await loc.IsVisibleAsync()) { await loc.ClickAsync(); }
                    Assert.Equal(1, await api.CountAsync());
                    Assert.Equal("x", api.Url);
                }
            }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }
}
