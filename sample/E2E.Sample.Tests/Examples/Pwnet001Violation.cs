// Documentation only: excluded from compilation (see csproj).
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace E2E.Sample.Tests.Examples;

public class Pwnet001Violation
{
    private readonly IPage page = null!;

    [Fact]
    public async Task Waits_with_fixed_delay()
    {
        await Task.Delay(2000);              // PWNET001
        Thread.Sleep(2000);                  // PWNET001
        await page.WaitForTimeoutAsync(2000); // PWNET001
        Assert.True(true);
    }
}
