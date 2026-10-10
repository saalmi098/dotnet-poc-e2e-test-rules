using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet006Tests
{
    private static readonly Pwnet006_UsePageObjectsInTestsAnalyzer Analyzer = new();

    private const string PageObjects = """
        using System.Threading.Tasks;
        using Microsoft.Playwright;
        public class LoginPage
        {
            private readonly IPage _page;
            public LoginPage(IPage page) { _page = page; }
            public Task SignInAsync() => _page.GetByRole("button").ClickAsync();
            public ILocator Banner => _page.GetByTestId("banner");
        }
        """;

    private static string TestClass(string members) => $$"""
        using System.Threading.Tasks;
        using Microsoft.Playwright;
        using Xunit;
        using static Microsoft.Playwright.Assertions;
        {{PageObjects}}
        public class LoginTests
        {
            {{members}}
        }
        """;

    [Fact]
    public async Task Reports_direct_playwright_calls_in_test_method()
    {
        var source = TestClass("""
            [Fact]
            public async Task T(IPage page) { await page.GetByRole("button").ClickAsync(); }
            """);

        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, source);

        Assert.Equal(2, diagnostics.Length); // GetByRole + ClickAsync
        Assert.All(diagnostics, d => Assert.Equal("PWNET006", d.Id));
    }

    [Fact]
    public async Task Reports_helper_method_in_test_class()
    {
        var source = TestClass("""
            [Fact]
            public void T() { Assert.True(true); }
            private Task Helper(IPage page) => page.GotoAsync("/x");
            """);

        Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Ignores_page_object_calls_and_Expect_on_page_object_members()
    {
        var source = TestClass("""
            [Fact]
            public async Task T(LoginPage login)
            {
                await login.SignInAsync();
                await Expect(login.Banner).ToBeVisibleAsync();
            }
            """);

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Ignores_playwright_calls_in_page_object_classes()
    {
        // LoginPage itself (non-test class) calls Playwright; only the test class matters.
        var source = TestClass("""
            [Fact]
            public void T() { Assert.True(true); }
            """);

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Edge_raw_locator_inside_Expect_is_still_reported_and_lambdas_count()
    {
        var source = TestClass("""
            [Fact]
            public async Task T(IPage page)
            {
                await Expect(page.GetByTestId("x")).ToBeVisibleAsync();
                System.Func<Task> f = () => page.GotoAsync("/y");
            }
            """);

        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, source);

        Assert.Equal(2, diagnostics.Length); // GetByTestId + GotoAsync; Expect/ToBeVisibleAsync allowed
    }
}
