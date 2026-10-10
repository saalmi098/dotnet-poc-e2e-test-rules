// Documentation only: excluded from compilation (see csproj).
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests.Examples;

public class Pwnet004Violation
{
    private readonly IPage page = null!;

    private const string AdminPassword = "hunter2"; // PWNET004 (identifier + literal)

    [Fact]
    public async Task Hardcodes_environment_data()
    {
        await page.GotoAsync("https://staging.example.com/login"); // PWNET004
        var url = "http://localhost:5000/x"; // PWNET004

        await page.GetByLabel("Name").FillAsync(AdminPassword);
        await Expect(page.GetByTestId("dashboard")).ToBeVisibleAsync();
    }
}
