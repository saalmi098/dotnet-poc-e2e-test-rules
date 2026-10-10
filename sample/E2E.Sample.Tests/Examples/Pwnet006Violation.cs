// Documentation only: excluded from compilation (see csproj).
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests.Examples;

public class Pwnet006Violation
{
    private readonly IPage page = null!;

    [Fact]
    public async Task Talks_to_playwright_directly()
    {
        await page.GotoAsync("/login");                          // PWNET006
        await page.GetByTestId("user").FillAsync("alice");       // PWNET006 (x2)
        await Expect(page.GetByTestId("done")).ToBeVisibleAsync(); // PWNET006 (GetByTestId only; Expect is allowed)
    }

    private Task Helper() => page.ReloadAsync();       // PWNET006: whole test class is checked

    [Fact]
    public async Task Talks_to_playwright_via_page_object()
    {
        // uses page object model to avoid talking to Playwright directly -> no PWNET006 violation
        var loginPage = await LoginPage.OpenAsync(page, "/login");
        await loginPage.SignInAsync();
        Assert.True(await loginPage.IsDoneVisible());
        await Expect(loginPage.DoneButton).ToBeVisibleAsync();
    }
}
