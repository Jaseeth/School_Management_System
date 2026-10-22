using System.Text.RegularExpressions;
using Microsoft.Playwright;
namespace SchoolManagement.E2ETests;
[Collection("Phase1Restore")]
public class StaffTeachingBrowserTests
{
    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task StaffTeachingRoleQuestionMultipleSubjectsAndDraftResumeWork(string engine)
    {
        var host = new Phase1Fixture();
        try {
            await host.InitializeAsync(); await host.AddStaffTeachingSubject(); using var playwright = await Playwright.CreateAsync();
            await using var browser = engine == "chrome" ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true }) : await playwright.Webkit.LaunchAsync(new() { Headless = true });
            var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = engine == "webkit", ViewportSize = new() { Width = 390, Height = 844 } });
            var page = await context.NewPageAsync(); await host.BeforeLogin(); await page.GotoAsync(host.BaseUrl + "/login");
            await page.GetByLabel(new Regex("^Staff ID or email address")).FillAsync(Phase1Fixture.Email("Admin")); await page.GetByLabel(new Regex("^Password")).FillAsync(host.Password);
            await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard"); await page.GotoAsync(host.BaseUrl + "/staff");
            await page.GetByRole(AriaRole.Button, new() { Name = "Add Staff", Exact = true }).ClickAsync();
            var form = page.GetByRole(AriaRole.Form, new() { Name = "Add staff", Exact = true });
            var role = form.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Role") });
            var teach = form.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Can this staff member teach") });
            foreach (var name in new[] { "Admin", "Section Head", "Teacher" }) {
                await role.SelectOptionAsync(new SelectOptionValue { Label = name }); await Assertions.Expect(teach).ToBeVisibleAsync(); await teach.SelectOptionAsync("no");
                await Assertions.Expect(form.GetByRole(AriaRole.Checkbox)).ToHaveCountAsync(0);
            }
            foreach (var name in new[] { "Principal", "Deputy Principal" }) { await role.SelectOptionAsync(new SelectOptionValue { Label = name }); await Assertions.Expect(teach).ToHaveCountAsync(0); }
            await role.SelectOptionAsync(new SelectOptionValue { Label = "Teacher" }); await teach.SelectOptionAsync("yes");
            await form.GetByLabel(new Regex("^Staff number")).FillAsync("QA-CAP"); await form.GetByLabel(new Regex("^Full name")).FillAsync("QA Multiple Subject Teacher"); await form.GetByLabel(new Regex("^Email")).FillAsync("qa-cap@example.test");
            await form.GetByLabel("Search subjects", new() { Exact = true }).FillAsync("English"); await form.GetByRole(AriaRole.Checkbox, new() { Name = "QA English", Exact = true }).CheckAsync();
            await form.GetByLabel("Search subjects", new() { Exact = true }).FillAsync("Math"); await form.GetByRole(AriaRole.Checkbox, new() { Name = "QA Mathematics", Exact = true }).CheckAsync();
            await Assertions.Expect(form.GetByText("2 selected", new() { Exact = true })).ToBeVisibleAsync();
            await form.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Save as draft and leave", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Region, new() { Name = "Saved drafts", Exact = true }).GetByRole(AriaRole.Button, new() { Name = "Edit draft", Exact = true }).ClickAsync();
            await Assertions.Expect(teach).ToHaveValueAsync("yes"); await Assertions.Expect(form.GetByRole(AriaRole.Checkbox, new() { Name = "QA English", Exact = true })).ToBeCheckedAsync(); await Assertions.Expect(form.GetByRole(AriaRole.Checkbox, new() { Name = "QA Mathematics", Exact = true })).ToBeCheckedAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Artifacts, $"staff-teaching-{engine}.png"), FullPage = true });
            await form.GetByLabel(new Regex("^Temporary password")).FillAsync(host.Password); await form.GetByLabel(new Regex("^Confirm temporary password")).FillAsync(host.Password);
            await form.GetByRole(AriaRole.Button, new() { Name = "Create Staff", Exact = true }).ClickAsync();
            await Assertions.Expect(form).ToHaveCountAsync(0);
            var card = page.Locator("article").Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = "QA Multiple Subject Teacher", Exact = true }) });
            await Assertions.Expect(card).ToContainTextAsync("QA English"); await Assertions.Expect(card).ToContainTextAsync("QA Mathematics");
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"));
        }
        finally { await host.DisposeAsync(); }
    }
}
