using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SchoolManagement.E2ETests;

[Collection("Phase1Restore")]
public class LeaveWorkspaceBrowserTests
{
    private static async Task<IPage> WorkspaceLogin(IBrowser browser, Phase1Fixture host, string role, bool webkit)
    {
        var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = webkit,
            ViewportSize = new() { Width = 390, Height = 844 }, IsMobile = true, HasTouch = true });
        var page = await context.NewPageAsync(); await host.BeforeLogin(); await page.GotoAsync(host.BaseUrl + "/login");
        await page.GetByLabel(new Regex("^Staff ID or email address")).FillAsync(Phase1Fixture.Email(role));
        await page.GetByLabel(new Regex("^Password")).FillAsync(host.Password);
        await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard");
        return page;
    }

    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task LeaveWorkspacesClearlySeparateAdminPersonalAndStaffActionsAndPreserveDeepLinks(string engine)
    {
        var host = new Phase1Fixture();
        try
        {
            await host.InitializeAsync(); using var playwright = await Playwright.CreateAsync();
            await using var browser = engine == "chrome" ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true })
                : await playwright.Webkit.LaunchAsync(new() { Headless = true });
            var admin = await WorkspaceLogin(browser, host, "Admin", engine == "webkit");
            await admin.GotoAsync(host.BaseUrl + "/staff-leave");
            var areas = admin.GetByRole(AriaRole.Navigation, new() { Name = "Leave workspaces", Exact = true });
            var categories = admin.GetByRole(AriaRole.Navigation, new() { Name = "Leave categories", Exact = true });
            await Assertions.Expect(areas.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Staff management") })).ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(categories.GetByRole(AriaRole.Button, new() { Name = "My requests", Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(categories.GetByRole(AriaRole.Button, new() { Name = "Staff leave history", Exact = true })).ToBeVisibleAsync();
            await admin.ScreenshotAsync(new() { Path = Path.Combine(host.Artifacts, $"leave-management-mobile-{engine}.png"), FullPage = true });
            await areas.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^My leave") }).ClickAsync();
            await Assertions.Expect(categories.GetByRole(AriaRole.Button, new() { Name = "My requests", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
            await Assertions.Expect(categories.GetByRole(AriaRole.Button, new() { Name = "Arrange cover", Exact = true })).ToHaveCountAsync(0);
            await categories.GetByRole(AriaRole.Button, new() { Name = "My balance", Exact = true }).ClickAsync();
            await Assertions.Expect(admin.GetByRole(AriaRole.Heading, new() { Name = "Leave balances", Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(admin.GetByRole(AriaRole.Combobox, new() { Name = "Staff", Exact = true })).ToHaveCountAsync(0);
            await categories.GetByRole(AriaRole.Button, new() { Name = "Withdraw / cancel", Exact = true }).ClickAsync();
            await Assertions.Expect(admin.GetByRole(AriaRole.Combobox, new() { Name = "Your leave request", Exact = true })).ToBeVisibleAsync();
            await areas.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Staff management") }).ClickAsync();
            await categories.GetByRole(AriaRole.Button, new() { Name = "Cancellation requests", Exact = true }).ClickAsync();
            await Assertions.Expect(admin.GetByRole(AriaRole.Heading, new() { Name = "Staff cancellation requests", Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(admin.GetByRole(AriaRole.Combobox, new() { Name = "Your leave request", Exact = true })).ToHaveCountAsync(0);
            await admin.ReloadAsync();
            await Assertions.Expect(admin.GetByRole(AriaRole.Heading, new() { Name = "Staff cancellation requests", Exact = true })).ToBeVisibleAsync();
            await categories.GetByRole(AriaRole.Button, new() { Name = "Staff balances", Exact = true }).ClickAsync();
            await Assertions.Expect(admin.GetByRole(AriaRole.Combobox, new() { Name = "Staff", Exact = true })).ToBeVisibleAsync();
            await admin.GotoAsync(host.BaseUrl + "/staff-leave?tab=extraAllowance");
            await Assertions.Expect(admin.GetByRole(AriaRole.Heading, new() { Name = "Extra leave days", Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(admin.GetByLabel(new Regex("^Staff member"))).ToBeEnabledAsync();
            await areas.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^My leave") }).ClickAsync();
            await categories.GetByRole(AriaRole.Button, new() { Name = "Extra leave days", Exact = true }).ClickAsync();
            await Assertions.Expect(admin.GetByRole(AriaRole.Heading, new() { Name = "My extra leave days", Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(admin.GetByLabel(new Regex("^Staff member"))).ToBeDisabledAsync();
            Assert.False(await admin.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"));
            await admin.SetViewportSizeAsync(1440, 1000);
            await areas.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Staff management") }).ClickAsync();
            await admin.ScreenshotAsync(new() { Path = Path.Combine(host.Artifacts, $"leave-management-desktop-{engine}.png"), FullPage = true });
            await areas.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^My leave") }).ClickAsync();
            await admin.ScreenshotAsync(new() { Path = Path.Combine(host.Artifacts, $"leave-personal-desktop-{engine}.png"), FullPage = true });
            var teacher = await WorkspaceLogin(browser, host, "Teacher", engine == "webkit");
            await teacher.GotoAsync(host.BaseUrl + "/staff-leave");
            await Assertions.Expect(teacher.GetByRole(AriaRole.Navigation, new() { Name = "Leave workspaces", Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(teacher.GetByRole(AriaRole.Heading, new() { Name = "My leave requests", Exact = true })).ToBeVisibleAsync();
            var head = await WorkspaceLogin(browser, host, "Section Head", engine == "webkit");
            await head.GotoAsync(host.BaseUrl + "/staff-leave?tab=coverage");
            await Assertions.Expect(head.GetByRole(AriaRole.Navigation, new() { Name = "Leave workspaces", Exact = true })
                .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Section management") })).ToHaveAttributeAsync("aria-pressed", "true");
            await head.GotoAsync(host.BaseUrl + "/staff-leave?tab=cancellations");
            await Assertions.Expect(head.GetByRole(AriaRole.Heading, new() { Name = "Staff cancellation requests", Exact = true })).ToBeVisibleAsync();
        }
        finally { await host.DisposeAsync(); }
    }
}
