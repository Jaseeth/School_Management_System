using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SchoolManagement.E2ETests;

[Collection("Phase1Restore")]
public class ExtraLeaveAllowanceBrowserTests
{
    private static async Task<IPage> StaffPage(IBrowser browser, Phase1Fixture host, string role, bool webkit)
    {
        var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = webkit,
            ViewportSize = new() { Width = 390, Height = 844 }, IsMobile = true, HasTouch = true });
        var page = await context.NewPageAsync(); await host.BeforeLogin();
        await page.GotoAsync(host.BaseUrl + "/login");
        await page.GetByLabel(new Regex("^Staff ID or email address")).FillAsync(Phase1Fixture.Email(role));
        await page.GetByLabel(new Regex("^Password")).FillAsync(host.Password);
        await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard");
        return page;
    }

    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task ExtraLeaveStaffRequestPrincipalApprovalAndDirectGrantUpdateBalance(string engine)
    {
        var host = new Phase1Fixture();
        try
        {
            await host.InitializeAsync(); using var playwright = await Playwright.CreateAsync();
            await using var browser = engine == "chrome" ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true })
                : await playwright.Webkit.LaunchAsync(new() { Headless = true });
            var teacher = await StaffPage(browser, host, "Teacher", engine == "webkit");
            await teacher.GotoAsync(host.BaseUrl + "/staff-leave?tab=extraAllowance");
            await teacher.GetByLabel(new Regex("^Additional leave days")).FillAsync("5");
            var reason = "QA maternity entitlement " + engine;
            await teacher.GetByLabel(new Regex("^Reason for extra days")).FillAsync(reason);
            await teacher.GetByRole(AriaRole.Button, new() { Name = "Request Principal approval", Exact = true }).ClickAsync();
            await Assertions.Expect(teacher.GetByRole(AriaRole.Status).Filter(new() { HasText = "requested" })).ToBeVisibleAsync();
            var admin = await StaffPage(browser, host, "Admin", engine == "webkit");
            await admin.GotoAsync(host.BaseUrl + "/notifications");
            await Assertions.Expect(admin.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("^Extra leave allowance awaiting approval") })).ToBeVisibleAsync();
            var principal = await StaffPage(browser, host, "Principal", engine == "webkit");
            await principal.GotoAsync(host.BaseUrl + "/staff-leave-allowance");
            await Assertions.Expect(principal.GetByRole(AriaRole.Heading, new() { Name = "Extra leave days", Exact = true })).ToBeVisibleAsync();
            var row = principal.GetByRole(AriaRole.Article).Filter(new() { HasText = reason });
            await row.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true }).ClickAsync();
            await row.GetByLabel(new Regex("^Approved extra days")).FillAsync("6");
            await row.GetByLabel(new Regex("^Approval reason")).FillAsync("Six extra days verified under the school policy");
            await row.GetByRole(AriaRole.Button, new() { Name = "Confirm approval", Exact = true }).ClickAsync();
            await Assertions.Expect(principal.GetByRole(AriaRole.Status).Filter(new() { HasText = "approved and added" })).ToBeVisibleAsync();
            await principal.GetByLabel(new Regex("^Staff member")).SelectOptionAsync(new SelectOptionValue { Label = "QA-Teacher — QA Teacher" });
            await principal.GetByLabel(new Regex("^Additional leave days")).FillAsync("2");
            await principal.GetByLabel(new Regex("^Reason for extra days")).FillAsync("Additional government entitlement verified");
            await principal.GetByRole(AriaRole.Button, new() { Name = "Grant extra days directly", Exact = true }).ClickAsync();
            await Assertions.Expect(principal.GetByRole(AriaRole.Status).Filter(new() { HasText = "granted" })).ToBeVisibleAsync();
            await teacher.GotoAsync(host.BaseUrl + "/staff-leave?tab=balances");
            await Assertions.Expect(teacher.GetByText("Total yearly allowance", new() { Exact = true }).Locator("..").GetByText("48", new() { Exact = true })).ToBeVisibleAsync();
            await teacher.GotoAsync(host.BaseUrl + "/notifications");
            await Assertions.Expect(teacher.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("^Extra leave allowance approved") })).ToBeVisibleAsync();
            await Assertions.Expect(teacher.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("^Extra leave allowance granted") })).ToBeVisibleAsync();
            Assert.False(await principal.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"));
            await principal.ScreenshotAsync(new() { Path = Path.Combine(host.Artifacts, $"extra-leave-{engine}.png"), FullPage = true });
        }
        finally { await host.DisposeAsync(); }
    }
}
