using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet002Tests
{
    private static readonly PreferSemanticLocatorsAnalyzer Analyzer = new();

    private static string Wrap(string body) => $$"""
        using Microsoft.Playwright;
        class T { void M(IPage page, ILocator loc, IFrame frame) { {{body}} } }
        """;

    [Theory]
    [InlineData("page.Locator(\"css=button\");")]
    [InlineData("page.Locator(\"xpath=//div\");")]
    [InlineData("page.Locator(\"//div[1]\");")]
    [InlineData("loc.Locator(\"#submit\");")]
    [InlineData("frame.Locator(\".btn\");")]
    [InlineData("page.Locator(\"ul > li\");")]
    public async Task Reports_brittle_selectors(string body)
    {
        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body));

        Assert.Equal("PWNET002", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Ignores_semantic_locators()
    {
        var body = """
            page.GetByRole("button"); page.GetByTestId("#id"); page.GetByLabel(".x");
            page.GetByText("[a]"); page.GetByPlaceholder("a > b");
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body)));
    }

    [Fact]
    public async Task Ignores_plain_tag_selector_and_non_literal_argument()
    {
        var body = """
            page.Locator("button");
            var s = "#dynamic"; page.Locator(s);
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body)));
    }

    [Fact]
    public async Task Ignores_Locator_on_non_Playwright_type()
    {
        const string source = """
            class T { void M(SomeOther.IPage page) { page.Locator("#x"); } }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Can_be_suppressed_with_pragma()
    {
        const string source = """
            using Microsoft.Playwright;
            class T
            {
                void M(IPage page)
                {
            #pragma warning disable PWNET002
                    page.Locator("#x");
            #pragma warning restore PWNET002
                }
            }
            """;
        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, source);

        // Pragma suppression is applied by Roslyn: nothing left that is active.
        Assert.DoesNotContain(diagnostics, d => !d.IsSuppressed);
    }
}
