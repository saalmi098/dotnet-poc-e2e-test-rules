using System;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests;

/// <summary>
/// Recommended Playwright.NET patterns (page objects, web-first assertions, config-driven URLs).
/// Compiles with all PWNET rules active. Skipped by default: needs Playwright browsers and a running app.
/// </summary>
public class CompliantExample
{
    [Fact(Skip = "Needs Playwright browsers (playwright.ps1 install) and a running app.")]
    public async Task Sign_in_shows_dashboard()
    {
        // Environment-specific values come from configuration, not literals.
        var baseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5000";

        await using var session = await BrowserSession.StartAsync();
        var login = await LoginPage.OpenAsync(session.Page, baseUrl);

        // Interactions go through the page object (see PageObjects.cs).
        await login.SignInAsync();

        // Web-first assertion retries until the condition holds.
        await Expect(login.Dashboard).ToBeVisibleAsync();
    }
}
