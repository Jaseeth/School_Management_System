using System.Text.RegularExpressions;
using Microsoft.Playwright;
namespace SchoolManagement.E2ETests;

[Collection("Phase1Restore")]
public class MarksReviewerBrowserTests
{
    private static async Task<IPage> Login(IBrowser browser, Phase1Fixture host, string email, string engine)
    {
        var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = engine == "webkit", ViewportSize = new() { Width = 390, Height = 844 } });
        var page = await context.NewPageAsync(); await host.BeforeLogin(); await page.GotoAsync(host.BaseUrl + "/login");
        await page.GetByLabel(new Regex("^Staff ID or email address")).FillAsync(email);
        await page.GetByLabel(new Regex("^Password")).FillAsync(host.Password);
        await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard");
        await page.GotoAsync(host.BaseUrl + "/marks-review"); return page;
    }
    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task HeadAssignsTeacherReviewerWhoApprovesThenHeadRemovesAccess(string engine)
    {
        var host = new Phase1Fixture();
        try
        {
            await host.InitializeAsync(); await host.AddMarksReviewerScenario(); using var playwright = await Playwright.CreateAsync();
            await using var browser = engine == "chrome" ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true }) : await playwright.Webkit.LaunchAsync(new() { Headless = true });
            var head = await Login(browser, host, Phase1Fixture.Email("Section Head"), engine);
            await head.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Reviewer teacher") }).SelectOptionAsync(new SelectOptionValue { Label = "QA-REVIEWER — QA Reviewer" });
            await head.GetByLabel(new Regex("^Assignment / removal reason")).FillAsync("Review assistance");
            await head.GetByRole(AriaRole.Button, new() { Name = "Assign reviewer", Exact = true }).ClickAsync();
            await head.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true }).ClickAsync();
            await Assertions.Expect(head.GetByRole(AriaRole.Region, new() { Name = "Reviewer assignments" }).GetByRole(AriaRole.Status)).ToContainTextAsync("Marks reviewer assigned");
            var reviewer = await Login(browser, host, "qa-reviewer@example.test", engine);
            await Assertions.Expect(reviewer.GetByRole(AriaRole.Button, new() { Name = "View Marks", Exact = true })).ToHaveCountAsync(1);
            await Assertions.Expect(reviewer.GetByRole(AriaRole.Heading, new() { Name = "Section marks reviewer", Exact = true })).ToHaveCountAsync(0);
            await reviewer.GetByRole(AriaRole.Button, new() { Name = "View Marks", Exact = true }).ClickAsync();
            await Assertions.Expect(reviewer.GetByText("QA001", new() { Exact = true })).ToBeVisibleAsync();
            await reviewer.GetByRole(AriaRole.Button, new() { Name = "Approve Marks", Exact = true }).ClickAsync();
            await reviewer.GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true }).ClickAsync();
            await Assertions.Expect(reviewer.GetByRole(AriaRole.Button, new() { Name = "View Marks", Exact = true })).ToHaveCountAsync(0);
            await head.ReloadAsync();
            await Assertions.Expect(head.GetByRole(AriaRole.Button, new() { Name = "View Marks", Exact = true })).ToHaveCountAsync(0);
            await head.GetByLabel(new Regex("^Assignment / removal reason")).FillAsync("Rotation complete");
            await head.GetByRole(AriaRole.Button, new() { Name = "Remove reviewer", Exact = true }).ClickAsync();
            await head.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true }).ClickAsync();
            await Assertions.Expect(head.GetByRole(AriaRole.Region, new() { Name = "Reviewer assignments" }).GetByRole(AriaRole.Status)).ToContainTextAsync("Reviewer access removed");
            await head.GetByText("Reviewer assignment history", new() { Exact = true }).ClickAsync();
            await Assertions.Expect(head.GetByText("Ended:", new() { Exact = false })).ToContainTextAsync("Rotation complete");
            Assert.False(await head.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"));
            await head.ScreenshotAsync(new() { Path = Path.Combine(host.Artifacts, $"marks-reviewer-{engine}.png"), FullPage = true });
        }
        finally { await host.DisposeAsync(); }
    }
}


