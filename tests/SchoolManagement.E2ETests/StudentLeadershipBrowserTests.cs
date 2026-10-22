using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SchoolManagement.E2ETests;

[Collection("Phase1Restore")]
public class StudentLeadershipBrowserTests
{
    private static async Task<IPage> Login(IBrowser browser, Phase1Fixture host, string role, string engine)
    {
        var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = engine == "webkit", ViewportSize = new() { Width = 390, Height = 844 } });
        var page = await context.NewPageAsync(); await host.BeforeLogin(); await page.GotoAsync(host.BaseUrl + "/login");
        await page.GetByLabel(new Regex("^Staff ID or email address")).FillAsync(Phase1Fixture.Email(role));
        await page.GetByLabel(new Regex("^Password")).FillAsync(host.Password);
        await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard");
        await page.GotoAsync(host.BaseUrl + "/student-leadership"); return page;
    }
    private static async Task Confirm(IPage page)
    {
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
    }
    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task TeacherMonitorAndAdminBulkPrefectAppointmentsKeepHistory(string engine)
    {
        var host = new Phase1Fixture();
        try
        {
            await host.InitializeAsync(); await host.AddLeadershipStudent(); using var playwright = await Playwright.CreateAsync();
            await using var browser = engine == "chrome" ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true }) : await playwright.Webkit.LaunchAsync(new() { Headless = true });
            var teacher = await Login(browser, host, "Teacher", engine);
            await teacher.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Class") }).SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await teacher.GetByRole(AriaRole.Radio, new() { NameRegex = new Regex("QA001") }).CheckAsync();
            await teacher.GetByRole(AriaRole.Button, new() { Name = "Review appointment", Exact = true }).ClickAsync(); await Confirm(teacher);
            await Assertions.Expect(teacher.GetByRole(AriaRole.Table)).ToContainTextAsync("Class Monitor");
            await teacher.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Leadership role") }).SelectOptionAsync("Assistant Class Monitor");
            await teacher.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Class") }).SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await teacher.GetByRole(AriaRole.Radio, new() { NameRegex = new Regex("QA002") }).CheckAsync();
            await teacher.GetByRole(AriaRole.Button, new() { Name = "Review appointment", Exact = true }).ClickAsync(); await Confirm(teacher);
            await Assertions.Expect(teacher.GetByRole(AriaRole.Table)).ToContainTextAsync("Assistant Class Monitor");
            await teacher.GetByRole(AriaRole.Table).GetByRole(AriaRole.Button, new() { Name = "View history", Exact = true }).First.ClickAsync();
            await Assertions.Expect(teacher.GetByRole(AriaRole.Dialog)).ToContainTextAsync("Appointed");
            await teacher.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
            var admin = await Login(browser, host, "Admin", engine);
            await Assertions.Expect(admin.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Leadership role") })).ToHaveValueAsync("Prefect");
            await admin.GetByRole(AriaRole.Checkbox, new() { NameRegex = new Regex("QA001") }).First.CheckAsync();
            await admin.GetByRole(AriaRole.Checkbox, new() { NameRegex = new Regex("QA002") }).First.CheckAsync();
            await admin.GetByRole(AriaRole.Button, new() { Name = "Review appointment", Exact = true }).ClickAsync(); await Confirm(admin);
            await admin.GetByRole(AriaRole.Button, new() { Name = "Select all matching prefects", Exact = true }).ClickAsync();
            await admin.GetByRole(AriaRole.Combobox, new() { Name = "Action", Exact = true }).SelectOptionAsync("Left school");
            await admin.GetByLabel(new Regex("^Reason")).FillAsync("Completed final school year");
            await admin.GetByRole(AriaRole.Button, new() { Name = "Review changes", Exact = true }).ClickAsync(); await Confirm(admin);
            await admin.GetByRole(AriaRole.Combobox, new() { Name = "View", Exact = true }).SelectOptionAsync("true");
            await Assertions.Expect(admin.GetByRole(AriaRole.Table)).ToContainTextAsync("Left school");
            await Assertions.Expect(admin.GetByRole(AriaRole.Table)).ToContainTextAsync("Class Monitor");
            Assert.False(await admin.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"));
            await admin.ScreenshotAsync(new() { Path = Path.Combine(host.Artifacts, $"student-leadership-{engine}.png"), FullPage = true });
            var principal = await Login(browser, host, "Principal", engine);
            await Assertions.Expect(principal.GetByRole(AriaRole.Table)).ToContainTextAsync("Class Monitor");
            await Assertions.Expect(principal.GetByRole(AriaRole.Heading, new() { Name = "New appointment", Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(principal.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        }
        finally { await host.DisposeAsync(); }
    }
}
