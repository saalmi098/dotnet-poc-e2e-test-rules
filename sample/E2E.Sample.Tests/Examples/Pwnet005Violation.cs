// Documentation only: excluded from compilation (see csproj).
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests.Examples;

public class Pwnet005Violation
{
    private readonly IPage page = null!;

    [Fact]
    public async Task Forgets_to_await()
    {
        page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync(); // PWNET005
        await Expect(page.GetByTestId("done")).ToBeVisibleAsync();
    }

    [Fact]
    public void Blocks_on_async_call()
    {
        page.GotoAsync("/").Wait();                                   // PWNET005
        var title = page.TitleAsync().Result;                         // PWNET005
        Expect(page.GetByTestId("done")).ToBeVisibleAsync().GetAwaiter().GetResult(); // PWNET005
    }
}
