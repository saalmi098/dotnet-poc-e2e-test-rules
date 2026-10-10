// Documentation only: excluded from compilation (see csproj).
using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests.Examples;

public class Pwnet010Violation
{
    private readonly IPage page = null!;

    [Fact]
    public async Task Assertion_can_never_fail()
    {
        try
        {
            await Expect(page.GetByTestId("done")).ToBeVisibleAsync();
        }
        catch (Exception ex) // PWNET010
        {
            Console.WriteLine(ex.Message);
        }
    }
}
