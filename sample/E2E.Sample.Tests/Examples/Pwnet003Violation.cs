// Documentation only: excluded from compilation (see csproj).
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace E2E.Sample.Tests.Examples;

public class Pwnet003Violation
{
    private readonly IPage page = null!;

    [Fact]
    public async Task Clicks_but_never_asserts() // PWNET003
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
    }

    [Fact]
    public void Placeholder_generated_later() // PWNET003: empty test always passes
    {
        // TODO
    }
}
