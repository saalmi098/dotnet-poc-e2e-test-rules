using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace E2E.Sample.Tests;

/// <summary>Owns Playwright/browser lifetime so test classes never touch the raw API.</summary>
public sealed class BrowserSession : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;

    private BrowserSession(IPlaywright playwright, IBrowser browser, IPage page)
    {
        _playwright = playwright;
        _browser = browser;
        Page = page;
    }

    public IPage Page { get; }

    public static async Task<BrowserSession> StartAsync()
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync();
        return new BrowserSession(playwright, browser, await browser.NewPageAsync());
    }

    public async ValueTask DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}

/// <summary>Page object: the only place that knows locators and Playwright calls for the login screen.</summary>
public sealed class LoginPage
{
    private readonly IPage _page;

    private LoginPage(IPage page) => _page = page;

    public ILocator Dashboard => _page.GetByTestId("dashboard");

    public static async Task<LoginPage> OpenAsync(IPage page, string baseUrl)
    {
        await page.GotoAsync(baseUrl);
        return new LoginPage(page);
    }

    public Task SignInAsync()
    {
        return _page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
    }
}
