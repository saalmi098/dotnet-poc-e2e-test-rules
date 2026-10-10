using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet002Tests
{
    private static readonly Pwnet002_PreferSemanticLocatorsAnalyzer Analyzer = new();

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

    [Theory]
    [InlineData("page.Locator(\"li:nth-child(3)\");")]
    [InlineData("page.Locator(\"tr:last-child td\");")]
    [InlineData("page.Locator(\"text=Save >> nth=1\");")]
    [InlineData("page.Locator(\"[data-testid=row] >> td\");")]
    public async Task Reports_structural_and_chained_selectors(string body)
    {
        Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body)));
    }

    [Theory]
    [InlineData("page.Locator(\"[data-testid='save']\");")]
    [InlineData("page.Locator(\"[data-test-id=save-button]\");")]
    [InlineData("page.Locator(\"data-testid=save\");")]
    [InlineData("page.Locator(\"text=Save changes.\");")]
    [InlineData("page.Locator(\"role=button[name='Save']\");")]
    public async Task Ignores_test_id_and_user_facing_engine_selectors(string body)
    {
        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body)));
    }

    [Fact]
    public async Task Edge_test_id_selector_with_extra_css_is_still_reported()
    {
        Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap("page.Locator(\"div [data-testid=save]\");")));
    }
}
