using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace E2E.Sample.Tests;

/// <summary>
/// Recommended Playwright.NET patterns. Compiles with all PWNET rules active.
/// Skipped by default because it needs installed browsers and a running app.
/// </summary>
public class CompliantExample
{
    [Fact(Skip = "Needs Playwright browsers (playwright.ps1 install) and a running app.")]
    public async Task Sign_in_shows_dashboard()
    {
        // Environment-specific values come from configuration, not literals.
        var baseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5000";

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();

        await page.GotoAsync(baseUrl);

        // Semantic locators + auto-waiting instead of CSS/XPath and Task.Delay.
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        // Web-first assertion retries until the condition holds.
        await Expect(page.GetByTestId("dashboard")).ToBeVisibleAsync();
    }
}
