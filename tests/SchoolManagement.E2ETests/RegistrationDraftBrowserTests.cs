using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SchoolManagement.E2ETests;

[Collection("Phase1Restore")]
public class RegistrationDraftBrowserTests
{
    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task StaffAndStudentDraftsCanSaveResumeDiscardAndDelete(string engine)
    {
        var host = new Phase1Fixture();
        try
        {
            await host.InitializeAsync(); using var playwright = await Playwright.CreateAsync();
            await using var browser = engine == "chrome" ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true })
                : await playwright.Webkit.LaunchAsync(new() { Headless = true });
            var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = engine == "webkit", ViewportSize = new() { Width = 390, Height = 844 } });
            var page = await context.NewPageAsync(); await host.BeforeLogin(); await page.GotoAsync(host.BaseUrl + "/login");
            await page.GetByLabel(new Regex("^Staff ID or email address")).FillAsync(Phase1Fixture.Email("Admin"));
            await page.GetByLabel(new Regex("^Password")).FillAsync(host.Password);
            await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard");
            await page.GotoAsync(host.BaseUrl + "/staff");
            await page.GetByRole(AriaRole.Button, new() { Name = "Add Staff", Exact = true }).ClickAsync();
            var form = page.GetByRole(AriaRole.Form, new() { Name = "Add staff", Exact = true });
            await form.GetByLabel(new Regex("^Staff number")).FillAsync("DRAFT-STAFF");
            await form.GetByLabel(new Regex("^Full name")).FillAsync("Draft Staff Example");
            await form.GetByLabel(new Regex("^Temporary password")).FillAsync(host.Password);
            await form.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
            var leave = page.GetByRole(AriaRole.Dialog, new() { Name = "Leave this form?", Exact = true });
            await leave.GetByRole(AriaRole.Button, new() { Name = "Keep editing", Exact = true }).ClickAsync();
            await Assertions.Expect(form.GetByLabel(new Regex("^Full name"))).ToHaveValueAsync("Draft Staff Example");
            await form.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
            await leave.GetByRole(AriaRole.Button, new() { Name = "Save as draft and leave", Exact = true }).ClickAsync();
            var drafts = page.GetByRole(AriaRole.Region, new() { Name = "Saved drafts", Exact = true });
            await Assertions.Expect(drafts).ToContainTextAsync("Draft Staff Example");
            await drafts.GetByRole(AriaRole.Button, new() { Name = "Edit draft", Exact = true }).ClickAsync();
            await Assertions.Expect(form.GetByLabel(new Regex("^Staff number"))).ToHaveValueAsync("DRAFT-STAFF");
            await Assertions.Expect(form.GetByLabel(new Regex("^Temporary password"))).ToHaveValueAsync("");
            await form.GetByLabel(new Regex("^Email" )).FillAsync("draft-staff@example.test");
            await form.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Role") }).SelectOptionAsync(new SelectOptionValue { Label = "Teacher" });
            await form.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Can this staff member teach") }).SelectOptionAsync("no");
            await form.GetByLabel(new Regex("^Temporary password")).FillAsync(host.Password);
            await form.GetByLabel(new Regex("^Confirm temporary password")).FillAsync(host.Password);
            var creation = await page.RunAndWaitForResponseAsync(
                () => form.GetByRole(AriaRole.Button, new() { Name = "Create Staff", Exact = true }).ClickAsync(),
                response => response.Request.Method == "POST" && response.Url.EndsWith("/api/Staff", StringComparison.OrdinalIgnoreCase));
            Assert.True(creation.Ok, "Draft staff creation must succeed before checking the confirmation.");
            await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = "created" })).ToBeVisibleAsync();
            await Assertions.Expect(drafts).ToContainTextAsync("No saved drafts");

            await page.GotoAsync(host.BaseUrl + "/students/add");
            await page.GetByLabel(new Regex("^Index Number")).FillAsync("DRAFT-STUDENT");
            await page.GetByLabel(new Regex("^Full Name")).FillAsync("Draft Student Example");
            await page.GetByLabel(new Regex("^Email")).FillAsync("draft-student@example.test");
            await page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Academic Placement", Exact = true })).ToBeVisibleAsync();
            for (var index = 0; index < 4; index++) await page.Locator("select").Nth(index).SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await page.GetByRole(AriaRole.Button, new() { Name = "Back to Students", Exact = true }).ClickAsync();
            await leave.GetByRole(AriaRole.Button, new() { Name = "Save as draft and leave", Exact = true }).ClickAsync();
            await page.WaitForURLAsync("**/students");
            await drafts.GetByRole(AriaRole.Button, new() { Name = "Edit draft", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Academic Placement", Exact = true })).ToBeVisibleAsync();
            for (var index = 0; index < 4; index++) await Assertions.Expect(page.Locator("select").Nth(index)).ToHaveValueAsync(new Regex(".+"));
            await page.GetByRole(AriaRole.Button, new() { Name = "Previous", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByLabel(new Regex("^Index Number"))).ToHaveValueAsync("DRAFT-STUDENT");
            await page.GetByLabel(new Regex("^Full Name")).FillAsync("Unsaved change");
            Assert.True(await page.EvaluateAsync<bool>("(() => { const event = new Event('beforeunload', {cancelable:true}); window.dispatchEvent(event); return event.defaultPrevented; })()"));
            await page.GoBackAsync();
            await leave.GetByRole(AriaRole.Button, new() { Name = "Keep editing", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Back to Students", Exact = true }).ClickAsync();
            await leave.GetByRole(AriaRole.Button, new() { Name = "Discard and leave", Exact = true }).ClickAsync();
            await page.WaitForURLAsync("**/students");
            await Assertions.Expect(drafts).ToContainTextAsync("Draft Student Example");
            await drafts.GetByRole(AriaRole.Button, new() { Name = "Delete draft", Exact = true }).ClickAsync();
            var confirm = page.GetByRole(AriaRole.Dialog, new() { Name = "Delete draft?", Exact = true });
            await confirm.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
            await Assertions.Expect(drafts).ToContainTextAsync("Draft Student Example");
            await drafts.GetByRole(AriaRole.Button, new() { Name = "Delete draft", Exact = true }).ClickAsync();
            await confirm.GetByRole(AriaRole.Button, new() { Name = "Confirm delete", Exact = true }).ClickAsync();
            await Assertions.Expect(drafts).ToContainTextAsync("No saved drafts");
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"));
        }
        finally { await host.DisposeAsync(); }
    }
}
