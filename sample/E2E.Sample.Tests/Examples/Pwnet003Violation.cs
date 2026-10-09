// Documentation only: excluded from compilation (see csproj).
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace E2E.Sample.Tests.Examples;

public class Pwnet003Violation
{
    [Fact]
    public async Task Clicks_but_never_asserts(IPage page) // PWNET003
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
    }
}
