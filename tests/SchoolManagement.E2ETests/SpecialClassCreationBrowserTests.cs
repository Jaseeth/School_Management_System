using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SchoolManagement.E2ETests;

[Collection("Phase1Restore")]
public class SpecialClassCreationBrowserTests
{
    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task AdminAndPrincipalScheduleSpecialClassesThroughTheForm(string engine)
    {
        var host = new Phase1Fixture();
        try
        {
            await host.InitializeAsync(); using var playwright = await Playwright.CreateAsync();
            await using var browser = engine == "chrome" ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true })
                : await playwright.Webkit.LaunchAsync(new() { Headless = true });
            var day = DateTime.Today.AddDays(7);
            while (day.DayOfWeek != DayOfWeek.Saturday) day = day.AddDays(1);
            foreach (var role in new[] { "Admin", "Principal" })
            {
                var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = engine == "webkit", ViewportSize = new() { Width = 390, Height = 844 } });
                var page = await context.NewPageAsync(); await host.BeforeLogin();
                await page.GotoAsync(host.BaseUrl + "/login");
                await page.GetByLabel(new Regex("^Staff ID or email address")).FillAsync(Phase1Fixture.Email(role));
                await page.GetByLabel(new Regex("^Password")).FillAsync(host.Password);
                await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard");
                await page.GotoAsync(host.BaseUrl + "/special-classes");
                await page.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Academic year") }).SelectOptionAsync(new SelectOptionValue { Index = 1 });
                await page.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Grade") }).SelectOptionAsync(new SelectOptionValue { Index = 1 });
                await page.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Class") }).SelectOptionAsync(new SelectOptionValue { Index = 1 });
                await page.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Subject") }).SelectOptionAsync(new SelectOptionValue { Index = 1 });
                await page.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Teacher") }).SelectOptionAsync(new SelectOptionValue { Index = 1 });
                await page.GetByLabel(new Regex("^Date ")).FillAsync(day.ToString("yyyy-MM-dd"));
                await page.GetByLabel(new Regex("^Start time")).FillAsync("09:00");
                await page.GetByLabel(new Regex("^End time")).FillAsync("10:00");
                await page.GetByLabel(new Regex("^Location")).FillAsync("QA Hall");
                await page.GetByLabel(new Regex("^Reason")).FillAsync(role + " revision class");
                await page.GetByRole(AriaRole.Button, new() { Name = "Schedule class", Exact = true }).ClickAsync();
                await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Special class scheduled");
                await Assertions.Expect(page.GetByRole(AriaRole.Article).Filter(new() { HasText = role + " revision class" })).ToContainTextAsync("QA Hall");
                Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"));
                await page.ScreenshotAsync(new() { Path = Path.Combine(host.Artifacts, $"special-class-{role}-{engine}.png"), FullPage = true });
                day = day.AddDays(7);
                await context.CloseAsync();
            }
        }
        finally { await host.DisposeAsync(); }
    }
}
