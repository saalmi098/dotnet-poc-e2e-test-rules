using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet005Tests
{
    private static readonly AwaitPlaywrightAsyncCallsAnalyzer Analyzer = new();

    private static string Wrap(string body) => $$"""
        using System.Threading.Tasks;
        using Microsoft.Playwright;
        class T { async Task M(IPage page, ILocator loc) { {{body}} await Task.CompletedTask; } }
        """;

    [Theory]
    [InlineData("page.GotoAsync(\"/x\");")]
    [InlineData("loc.ClickAsync();")]
    [InlineData("loc.TextContentAsync();")]
    public async Task Reports_unawaited_calls(string body)
    {
        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body));

        Assert.Equal("PWNET005", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Ignores_awaited_or_observed_calls()
    {
        var body = """
            await loc.ClickAsync();
            var t = loc.ClickAsync(); await t;
            await Task.WhenAll(loc.ClickAsync(), page.GotoAsync("/x"));
            var text = await loc.TextContentAsync();
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body)));
    }

    [Fact]
    public async Task Ignores_non_Playwright_async_methods()
    {
        const string source = """
            using System.Threading.Tasks;
            class Repo { public Task SaveAsync() => Task.CompletedTask; }
            class T { void M(Repo r, SomeOther.IPage p) { r.SaveAsync(); } }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Reports_explicit_discard_but_not_returned_task()
    {
        const string source = """
            using System.Threading.Tasks;
            using Microsoft.Playwright;
            class T
            {
                void A(ILocator loc) { _ = loc.ClickAsync(); }
                Task B(ILocator loc) { return loc.ClickAsync(); }
                Task C(ILocator loc) => loc.ClickAsync();
            }
            """;

        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, source);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Reports_unawaited_web_first_assertion()
    {
        var source = """
            using System.Threading.Tasks;
            using Microsoft.Playwright;
            using static Microsoft.Playwright.Assertions;
            class T { async Task M(ILocator loc) { Expect(loc).ToBeVisibleAsync(); await Task.CompletedTask; } }
            """;

        Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Theory]
    [InlineData("page.GotoAsync(\"/x\").Wait();")]
    [InlineData("var t = loc.TextContentAsync().Result;")]
    [InlineData("loc.ClickAsync().GetAwaiter().GetResult();")]
    public async Task Reports_blocking_on_playwright_calls(string body)
    {
        var source = $$"""
            using Microsoft.Playwright;
            class T { void M(IPage page, ILocator loc) { {{body}} } }
            """;

        Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Ignores_blocking_on_non_Playwright_tasks_and_GetAwaiter_alone()
    {
        const string source = """
            using System.Threading.Tasks;
            using Microsoft.Playwright;
            class Repo { public Task<int> LoadAsync() => Task.FromResult(1); }
            class T
            {
                void M(Repo r, ILocator loc)
                {
                    var n = r.LoadAsync().Result;
                    var awaiter = loc.ClickAsync().GetAwaiter();
                    awaiter.GetResult();
                }
            }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }
}
