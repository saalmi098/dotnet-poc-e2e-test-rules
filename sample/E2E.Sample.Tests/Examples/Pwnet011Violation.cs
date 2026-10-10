// Documentation only: excluded from compilation (see csproj).
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests.Examples;

#pragma warning disable PWNET006
public class Pwnet011Violation
{
    private readonly IPage page = null!;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Checkout_for_any_customer(bool premium) // PWNET011: 1 branch -> better: two tests, or InlineData per scenario
    {
        if (premium)
        {
            await Expect(page.GetByTestId("discount")).ToBeVisibleAsync();
        }
        else
        {
            await Expect(page.GetByTestId("upsell")).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task Closes_banner_if_shown()
    {
        var banner = page.GetByRole(AriaRole.Dialog);
        if (await banner.IsVisibleAsync()) // PWNET011: path depends on timing
        {
            await banner.ClickAsync();
        }

        await Expect(page.GetByTestId("done")).ToBeVisibleAsync();
    }
}

/// <summary>Page objects are checked too, but only for branches on one-shot state reads.</summary>
public class CookieBannerPage(IPage page)
{
    public async Task DismissIfShownAsync()
    {
        var banner = page.GetByRole(AriaRole.Dialog);
        if (await banner.IsVisibleAsync()) // PWNET011: use page.AddLocatorHandlerAsync(banner, ...) instead
        {
            await banner.GetByRole(AriaRole.Button, new() { Name = "Accept" }).ClickAsync();
        }
    }
}
#pragma warning restore PWNET006
