using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SchoolManagement.E2ETests;

[Collection("Phase1Restore")]
public class AuditDetailsBrowserTests
{
    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task AuditDetailsOpenAsDialogAndCloseWithKeyboardAndButton(string engine)
    {
        var host = new Phase1Fixture();
        try
        {
            await host.InitializeAsync(); using var playwright = await Playwright.CreateAsync();
            await using var browser = engine == "chrome" ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true })
                : await playwright.Webkit.LaunchAsync(new() { Headless = true });
            var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = engine == "webkit", ViewportSize = new() { Width = 390, Height = 844 } });
            var page = await context.NewPageAsync(); await host.BeforeLogin();
            await page.GotoAsync(host.BaseUrl + "/login");
            await page.GetByLabel(new Regex("^Staff ID or email address")).FillAsync(Phase1Fixture.Email("Admin"));
            await page.GetByLabel(new Regex("^Password")).FillAsync(host.Password);
            await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard");
            // A full result page reproduces the original off-screen detail panel.
            await page.RouteAsync("**/api/audit-logs?*", route => route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(new {
                page = 1, totalPages = 1, totalCount = 25,
                auditLogs = Enumerable.Range(1, 25).Select(id => new { id, action = "Update", entityName = "MondayAssembly", entityId = "1",
                    description = "Updated Monday assembly time", createdAt = "2026-10-20T09:00:00Z", staffId = 1,
                    oldValues = "{\"startTime\":\"07:30:00\",\"endTime\":\"08:10:00\"}", newValues = "{\"StartTime\":\"07:30:00\",\"EndTime\":\"08:15:00\"}" })
            }) }));
            await page.GotoAsync(host.BaseUrl + "/audit-logs");
            var view = page.GetByRole(AriaRole.Button, new() { Name = "View details for MondayAssembly #1", Exact = true }).First;
            await view.ClickAsync();
            var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Change details", Exact = true });
            await Assertions.Expect(dialog).ToBeVisibleAsync();
            await Assertions.Expect(dialog.GetByText("08:15", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(dialog.GetByText("End time", new() { Exact = true })).ToHaveCountAsync(1);
            await Assertions.Expect(dialog.GetByText("Start time", new() { Exact = true })).ToHaveCountAsync(0);
            Assert.True(await dialog.EvaluateAsync<bool>("el => el.matches(':modal')"));
            Assert.False(await dialog.EvaluateAsync<bool>("el => el.scrollWidth > el.clientWidth + 2"));
            await page.Keyboard.PressAsync("Escape"); await Assertions.Expect(dialog).ToHaveCountAsync(0);
            await view.ClickAsync(); await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
            await Assertions.Expect(view).ToBeFocusedAsync();
        }
        finally { await host.DisposeAsync(); }
    }
}
