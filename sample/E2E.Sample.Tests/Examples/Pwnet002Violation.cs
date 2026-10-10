// Documentation only: excluded from compilation (see csproj).
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests.Examples;

public class Pwnet002Violation
{
    private readonly IPage page = null!;

    [Fact]
    public async Task Uses_brittle_selectors()
    {
        await page.Locator("css=button.primary").ClickAsync(); // PWNET002
        await page.Locator("//div[@id='main']").ClickAsync();  // PWNET002
        await page.Locator("#submit").ClickAsync();            // PWNET002
        await page.Locator("tr:nth-child(2) td").ClickAsync(); // PWNET002 (positional)
        await page.Locator("[data-testid=save]").ClickAsync(); // OK: explicit test id

#pragma warning disable PWNET002 // deliberate, documented exception
        await page.Locator("canvas.legacy-chart").ClickAsync();
#pragma warning restore PWNET002

        await Expect(page.GetByTestId("done")).ToBeVisibleAsync();
    }
}
