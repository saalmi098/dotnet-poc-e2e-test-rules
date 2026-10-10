using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet009Tests
{
    private static readonly AvoidBypassingActionabilityAnalyzer Analyzer = new();

    private static string Wrap(string body) => $$"""
        using System.Threading.Tasks;
        using Microsoft.Playwright;
        class T { async Task M(IPage page, ILocator loc) { {{body}} } }
        """;

    [Theory]
    [InlineData("await loc.ClickAsync(new() { Force = true });", "Force = true")]
    [InlineData("var o = new LocatorClickOptions(); o.Force = true; await loc.ClickAsync(o);", "Force = true")]
    [InlineData("await loc.DispatchEventAsync(\"click\");", "DispatchEventAsync")]
    [InlineData("await loc.EvaluateAsync(\"el => el.click()\");", "EvaluateAsync")]
    [InlineData("await page.EvaluateAsync(\"document.querySelector('#save').dispatchEvent(new Event('click'))\");", "EvaluateAsync")]
    public async Task Reports_bypassed_interactions(string body, string expected)
    {
        var d = Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body)));

        Assert.Equal("PWNET009", d.Id);
        Assert.Contains(expected, d.GetMessage());
    }

    [Fact]
    public async Task Ignores_normal_clicks_Force_false_and_read_only_scripts()
    {
        var body = """
            await loc.ClickAsync();
            await loc.ClickAsync(new() { Force = false });
            await page.EvaluateAsync("() => window.scrollY");
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body)));
    }

    [Fact]
    public async Task Edge_ignores_Force_on_non_Playwright_types_and_suppression_works()
    {
        const string source = """
            using System.Threading.Tasks;
            using Microsoft.Playwright;
            class GitOptions { public bool Force { get; set; } }
            class T
            {
                async Task M(ILocator loc)
                {
                    var g = new GitOptions { Force = true };
            #pragma warning disable PWNET009 // canvas widget ignores pointer events by design
                    await loc.ClickAsync(new() { Force = true });
            #pragma warning restore PWNET009
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, source);

        Assert.DoesNotContain(diagnostics, d => !d.IsSuppressed);
    }
}
