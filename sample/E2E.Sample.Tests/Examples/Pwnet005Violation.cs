// Documentation only: excluded from compilation (see csproj).
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests.Examples;

public class Pwnet005Violation
{
    [Fact]
    public async Task Forgets_to_await(IPage page)
    {
        page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync(); // PWNET005
        await Expect(page.GetByTestId("done")).ToBeVisibleAsync();
    }
}
