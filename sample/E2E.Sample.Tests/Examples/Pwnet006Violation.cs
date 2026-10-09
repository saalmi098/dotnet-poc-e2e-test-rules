// Documentation only: excluded from compilation (see csproj).
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests.Examples;

public class Pwnet006Violation
{
    [Fact]
    public async Task Talks_to_playwright_directly(IPage page)
    {
        await page.GotoAsync("/login");                          // PWNET006
        await page.GetByTestId("user").FillAsync("alice");       // PWNET006 (x2)
        await Expect(page.GetByTestId("done")).ToBeVisibleAsync(); // PWNET006 (GetByTestId only; Expect is allowed)
    }

    private Task Helper(IPage page) => page.ReloadAsync();       // PWNET006: whole test class is checked
}
