using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace ToolShare.E2E;

/// <summary>
/// One fresh browser context (cookies, storage) per test on top of the shared app/browser from
/// <see cref="E2EAppFixture"/>, so tests never see each other's sign-in state. Every test is
/// traced; <see cref="RunAsync"/> keeps the trace (+ a screenshot) only when the test fails.
/// </summary>
[Collection(E2EConsts.CollectionDefinitionName)]
public abstract class E2ETestBase : IAsyncLifetime
{
    private bool _tracing;

    protected E2ETestBase(E2EAppFixture app)
    {
        App = app;
    }

    protected E2EAppFixture App { get; }

    protected IBrowserContext Context { get; private set; } = null!;

    protected IPage Page { get; private set; } = null!;

    /// <summary>
    /// Where failure artifacts go: <c>TOOLSHARE_E2E_ARTIFACTS</c> (set by scripts/speckit-e2e.ps1 to
    /// its run folder), otherwise <c>playwright-artifacts</c> next to the test binaries.
    /// </summary>
    private static string ArtifactsDir =>
        Environment.GetEnvironmentVariable("TOOLSHARE_E2E_ARTIFACTS")
        ?? Path.Combine(AppContext.BaseDirectory, "playwright-artifacts");

    public async Task InitializeAsync()
    {
        Context = await App.Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = App.BaseUrl });
        await Context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = true });
        _tracing = true;
        Page = await Context.NewPageAsync();
        Page.SetDefaultTimeout(30_000);
    }

    public async Task DisposeAsync()
    {
        if (_tracing)
        {
            await Context.Tracing.StopAsync();
        }

        await Context.CloseAsync();
    }

    /// <summary>
    /// Runs the test body; on failure saves <c>&lt;test&gt;.trace.zip</c> (open with
    /// <c>playwright.ps1 show-trace</c>) and <c>&lt;test&gt;.png</c>, appends the app's recent output,
    /// and rethrows. Passing tests leave nothing behind.
    /// </summary>
    protected async Task RunAsync(Func<Task> scenario, [CallerMemberName] string testName = "")
    {
        try
        {
            await scenario();
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(ArtifactsDir);
            var basePath = Path.Combine(ArtifactsDir, $"{GetType().Name}.{testName}");
            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = basePath + ".png", FullPage = true });
            await Context.Tracing.StopAsync(new TracingStopOptions { Path = basePath + ".trace.zip" });
            _tracing = false;

            throw new Exception(
                $"{ex.Message}\nURL: {Page.Url}\nTrace: {basePath}.trace.zip\nScreenshot: {basePath}.png\n--- app output (tail) ---\n{App.AppOutputTail(30)}",
                ex);
        }
    }

    /// <summary>Signs in through the ABP Account login page as the seeded administrator.</summary>
    protected async Task SignInAsAdminAsync()
    {
        await Page.GotoAsync("/Account/Login");
        await Page.Locator("input[name='LoginInput.UserNameOrEmailAddress']").FillAsync(E2EAppFixture.AdminUserName);
        var password = Page.Locator("input[name='LoginInput.Password']");
        await password.FillAsync(E2EAppFixture.AdminPassword);
        await password.PressAsync("Enter");
        await Page.WaitForURLAsync(url => !url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Blazor Server prerenders buttons before the interactive circuit is attached, and a click
    /// that lands before that is silently lost — so click again until the expected effect shows.
    /// </summary>
    protected static async Task ClickUntilVisibleAsync(ILocator button, ILocator expected)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await button.ClickAsync();
            try
            {
                await expected.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 1_000 });
                return;
            }
            // Playwright reports an expired wait as System.TimeoutException, which
            // is not a PlaywrightException — catching only the latter made this
            // loop give up after the first lost click instead of retrying.
            catch (Exception exception) when (exception is TimeoutException or PlaywrightException)
            {
                // Circuit not interactive yet — retry.
            }
        }

        await Expect(expected).ToBeVisibleAsync();
    }
}
