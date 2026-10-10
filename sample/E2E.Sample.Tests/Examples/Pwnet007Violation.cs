// Documentation only: excluded from compilation (see csproj).
using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests.Examples;

public class Pwnet007Violation
{
    private readonly IPage page = null!;

    [Fact]
    public async void Runner_cannot_await_this() // PWNET007
    {
        await Expect(page.GetByTestId("done")).ToBeVisibleAsync();
    }
}
