// Documentation only: excluded from compilation (see csproj).
using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests.Examples;

public class Pwnet009Violation
{
    private readonly IPage page = null!;

    [Fact]
    public async Task Bypasses_actionability()
    {
        var save = page.GetByRole(AriaRole.Button, new() { Name = "Save" });

        await save.ClickAsync(new() { Force = true });         // PWNET009
        await save.DispatchEventAsync("click");                 // PWNET009
        await save.EvaluateAsync("el => el.click()");           // PWNET009

        await Expect(page.GetByTestId("done")).ToBeVisibleAsync();
    }
}
