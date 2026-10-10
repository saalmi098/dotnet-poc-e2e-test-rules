using System;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests;

/// <summary>
/// Recommended Playwright.NET patterns (page objects, web-first assertions, URLs from configuration only).
/// Compiles with all PWNET rules active. Skipped by default: needs Playwright browsers, a running app and E2E_BASE_URL.
/// </summary>
public class CompliantExample
{
    [Fact(Skip = "Needs Playwright browsers (playwright.ps1 install) and a running app.")]
    public async Task Sign_in_shows_dashboard()
    {
        // Environment-specific values come from configuration only: no hardcoded fallback, fail fast instead.
        var baseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL")
            ?? throw new InvalidOperationException("Set the E2E_BASE_URL environment variable to the base URL of the app under test.");

        await using var session = await BrowserSession.StartAsync();
        var login = await LoginPage.OpenAsync(session.Page, baseUrl);

        // Interactions go through the page object (see PageObjects.cs).
        await login.SignInAsync();

        // Web-first assertion retries until the condition holds.
        await Expect(login.Dashboard).ToBeVisibleAsync();
    }
}
