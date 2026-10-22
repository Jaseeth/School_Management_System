using System.Text.RegularExpressions;
using Microsoft.Playwright;
namespace SchoolManagement.E2ETests;
[Collection("Phase1Restore")]
public class BulkTeachingBrowserTests
{
    [Fact]
    public async Task StaffTeachingEditorMakesLegacyAdminEligibleWithoutChangingRoles()
    {
        var host = new Phase1Fixture();
        try {
            await host.InitializeAsync(); await host.AddStaffTeachingSubject(); using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
            await host.BeforeLogin(); await page.GotoAsync(host.BaseUrl + "/login"); await page.GetByLabel(new Regex("^Staff ID or email address")).FillAsync(Phase1Fixture.Email("Admin")); await page.GetByLabel(new Regex("^Password")).FillAsync(host.Password);
            await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard"); await page.GotoAsync(host.BaseUrl + "/staff");
            var card = page.Locator("article").Filter(new() { Has = page.GetByText("QA-Admin", new() { Exact = true }) });
            await card.GetByRole(AriaRole.Button, new() { Name = "Teaching subjects", Exact = true }).ClickAsync();
            var dialog = page.GetByRole(AriaRole.Dialog);
            await Choice(dialog, "Can this staff member teach").SelectOptionAsync("yes");
            await dialog.GetByRole(AriaRole.Checkbox, new() { Name = "QA English", Exact = true }).CheckAsync();
            var response = await page.RunAndWaitForResponseAsync(() => dialog.GetByRole(AriaRole.Button, new() { Name = "Save teaching subjects", Exact = true }).ClickAsync(), r => r.Request.Method == "PUT" && r.Url.EndsWith("/teaching")); Assert.True(response.Ok);
            await Assertions.Expect(dialog).ToHaveCountAsync(0); await Assertions.Expect(card).ToContainTextAsync("QA English");
            await page.GotoAsync(host.BaseUrl + "/teacher-assignments"); var form = page.GetByRole(AriaRole.Region, new() { Name = "Assign subject teachers", Exact = true });
            await Choice(form, "Academic year").SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Choice(form, "Teaching staff").SelectOptionAsync((await Choice(form, "Teaching staff").Locator("option").Filter(new() { HasText = "QA-Admin —" }).GetAttributeAsync("value"))!);
            var subject = Choice(form.GetByRole(AriaRole.Group), "Subject"); await Assertions.Expect(subject.Locator("option")).ToHaveCountAsync(2); await Assertions.Expect(subject).ToContainTextAsync("QA English");
        }
        finally { await host.DisposeAsync(); }
    }
    private static ILocator Choice(ILocator root, string name) => root.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^" + name) });
    private static async Task Row(ILocator row, string subject, string section, string grade, string schoolClass) {
        await Choice(row, "Subject").SelectOptionAsync(new SelectOptionValue { Label = subject });
        await Choice(row, "Section").SelectOptionAsync(new SelectOptionValue { Label = section });
        await Choice(row, "Grade").SelectOptionAsync(new SelectOptionValue { Label = grade });
        await Choice(row, "Class").SelectOptionAsync(new SelectOptionValue { Label = schoolClass });
    }
    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task EligibleStaffAndSubjectsBulkAssignAcrossSectionsAndGrades(string engine)
    {
        var host = new Phase1Fixture();
        try {
            await host.InitializeAsync(); await host.AddBulkTeachingScenario(); using var playwright = await Playwright.CreateAsync();
            await using var browser = engine == "chrome" ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true }) : await playwright.Webkit.LaunchAsync(new() { Headless = true });
            var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = engine == "webkit", ViewportSize = new() { Width = 390, Height = 844 } }); var page = await context.NewPageAsync();
            await host.BeforeLogin(); await page.GotoAsync(host.BaseUrl + "/login"); await page.GetByLabel(new Regex("^Staff ID or email address")).FillAsync(Phase1Fixture.Email("Admin")); await page.GetByLabel(new Regex("^Password")).FillAsync(host.Password);
            await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard"); await page.GotoAsync(host.BaseUrl + "/teacher-assignments");
            var form = page.GetByRole(AriaRole.Region, new() { Name = "Assign subject teachers", Exact = true });
            await Choice(form, "Academic year").SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Assertions.Expect(Choice(form, "Teaching staff").Locator("option")).ToHaveCountAsync(4);
            await Choice(form, "Teaching staff").SelectOptionAsync((await Choice(form, "Teaching staff").Locator("option").Filter(new() { HasText = "QA-Admin —" }).GetAttributeAsync("value"))!);
            await Row(form.GetByRole(AriaRole.Group, new() { Name = "Assignment 1", Exact = true }), "QA English", "QA Section", "11", "A");
            await form.GetByRole(AriaRole.Button, new() { Name = "Add another assignment", Exact = true }).ClickAsync();
            await Row(form.GetByRole(AriaRole.Group, new() { Name = "Assignment 2", Exact = true }), "QA English", "QA Biology", "10", "B");
            await form.GetByRole(AriaRole.Button, new() { Name = "Add another assignment", Exact = true }).ClickAsync();
            await Row(form.GetByRole(AriaRole.Group, new() { Name = "Assignment 3", Exact = true }), "QA Mathematics", "QA Biology", "10", "B");
            await form.ScreenshotAsync(new() { Path = Path.Combine(host.Artifacts, $"bulk-teaching-{engine}.png") });
            await form.GetByRole(AriaRole.Button, new() { Name = "Review assignments (3)", Exact = true }).ClickAsync();
            var dialog = page.GetByRole(AriaRole.Dialog); await Assertions.Expect(dialog).ToContainTextAsync("QA Biology");
            var response = await page.RunAndWaitForResponseAsync(() => dialog.GetByRole(AriaRole.Button, new() { Name = "Save assignments", Exact = true }).ClickAsync(), r => r.Request.Method == "POST" && r.Url.EndsWith("/teacher-assignments/bulk")); Assert.True(response.Ok);
            await Assertions.Expect(form.GetByRole(AriaRole.Status)).ToContainTextAsync("Saved 3 assignment");
            var records = page.GetByRole(AriaRole.Region, new() { Name = "Existing teacher assignments", Exact = true });
            await Assertions.Expect(records.Locator("article")).ToHaveCountAsync(3); await Assertions.Expect(records).ToContainTextAsync("QA Mathematics");
            // A staff change must discard the previous staff member's unfinished rows.
            await Choice(form, "Teaching staff").SelectOptionAsync((await Choice(form, "Teaching staff").Locator("option").Filter(new() { HasText = "QA-Teacher —" }).GetAttributeAsync("value"))!);
            await Assertions.Expect(form.GetByRole(AriaRole.Group)).ToHaveCountAsync(1);
            Assert.DoesNotContain("QA English", await Choice(form.GetByRole(AriaRole.Group), "Subject").InnerTextAsync());
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"));
        }
        finally { await host.DisposeAsync(); }
    }
}
