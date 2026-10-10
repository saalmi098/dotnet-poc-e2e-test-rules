// Documentation only: excluded from compilation (see csproj).
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests.Examples;

public class Pwnet008Violation
{
    private readonly IPage page = null!;

    [Fact]
    public async Task Reads_state_once()
    {
        var status = page.GetByTestId("status");

        Assert.True(await status.IsVisibleAsync());            // PWNET008 -> Expect(status).ToBeVisibleAsync()
        Assert.Equal("Saved", await status.TextContentAsync()); // PWNET008 -> Expect(status).ToHaveTextAsync("Saved")
        Assert.EndsWith("/done", page.Url);                     // PWNET008 -> Expect(page).ToHaveURLAsync(...)

        var count = await page.GetByRole(AriaRole.Row).CountAsync();
        Assert.Equal(3, count);                                 // PWNET008 -> Expect(rows).ToHaveCountAsync(3)

        await Expect(status).ToHaveTextAsync("Saved");          // OK: retries until it matches
    }
}
