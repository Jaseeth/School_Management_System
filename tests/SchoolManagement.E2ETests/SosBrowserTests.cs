using System.Text.RegularExpressions;
using Microsoft.Playwright;
namespace SchoolManagement.E2ETests;

[Collection("Phase1Restore")]
public class SosBrowserTests
{
    private static async Task<IPage> Login(IBrowser browser, Phase1Fixture host, string role, string engine)
    {
        var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = engine == "webkit", ViewportSize = new() { Width = 390, Height = 844 } });
        if (engine == "chrome") await context.GrantPermissionsAsync(["notifications"]);
        await context.AddInitScriptAsync("""
            window.sosToneCount = 0;
            const Audio = window.AudioContext || window.webkitAudioContext;
            if (Audio) { const original = Audio.prototype.createOscillator; Audio.prototype.createOscillator = function(...args) { window.sosToneCount++; return original.apply(this, args); }; }
            """);
        var page = await context.NewPageAsync(); await host.BeforeLogin();
        await page.GotoAsync(host.BaseUrl + (role == "Student" ? "/student/login" : role == "Parent" ? "/parent/login" : "/login"));
        await page.Locator("input:not([type=password]):not([type=checkbox]):not([type=hidden])").First.FillAsync(role == "Student" ? "QA001" : Phase1Fixture.Email(role));
        await page.Locator("input[type=password]").FillAsync(host.Password); await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard"); return page;
    }
    private static async Task Publish(IPage admin, string title, string audience)
    {
        await admin.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Alert type") }).SelectOptionAsync("sos");
        await admin.GetByLabel(new Regex("^Title")).FillAsync(title); await admin.GetByLabel(new Regex("^Message")).FillAsync("Please attend the office immediately. QA test only.");
        await admin.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Audience") }).SelectOptionAsync(audience);
        if (audience == "Role") await admin.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Role") }).SelectOptionAsync("Parent");
        if (audience == "SpecificStaff") await admin.GetByRole(AriaRole.Checkbox, new() { NameRegex = new Regex("QA-Teacher") }).CheckAsync();
        await admin.GetByRole(AriaRole.Button, new() { Name = "Review SOS", Exact = true }).ClickAsync();
        await admin.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Send SOS now", Exact = true }).ClickAsync();
        await Assertions.Expect(admin.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        await Assertions.Expect(admin.GetByText(new Regex("^SOS sent"))).ToBeVisibleAsync();
    }
    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task SosAlarmAcknowledgementRecipientPortalsAndCancellationWork(string engine)
    {
        var host = new Phase1Fixture();
        try
        {
            await host.InitializeAsync(); using var playwright = await Playwright.CreateAsync();
            await using var browser = engine == "chrome" ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true }) : await playwright.Webkit.LaunchAsync(new() { Headless = true });
            var admin = await Login(browser, host, "Admin", engine); await admin.GotoAsync(host.BaseUrl + "/announcements");
            var teacher = await Login(browser, host, "Teacher", engine);
            await teacher.GetByRole(AriaRole.Button, new() { Name = "Enable SOS sound", Exact = true }).ClickAsync();
            await Publish(admin, "Urgent staff briefing", "SpecificStaff");
            var sos = teacher.GetByRole(AriaRole.Dialog);
            await Assertions.Expect(sos).ToBeVisibleAsync(new() { Timeout = 20000 }); await Assertions.Expect(sos).ToContainTextAsync("Urgent staff briefing");
            await teacher.BringToFrontAsync();
            if (engine == "chrome") {
                await teacher.WaitForFunctionAsync("window.sosToneCount >= 3");
                await sos.GetByRole(AriaRole.Button, new() { Name = "Mute alarm", Exact = true }).ClickAsync();
                var tones = await teacher.EvaluateAsync<int>("window.sosToneCount"); await teacher.WaitForTimeoutAsync(1800); Assert.Equal(tones, await teacher.EvaluateAsync<int>("window.sosToneCount"));
                await sos.GetByRole(AriaRole.Button, new() { Name = "Resume alarm", Exact = true }).ClickAsync(); await teacher.WaitForFunctionAsync($"window.sosToneCount > {tones}");
            }
            await teacher.ScreenshotAsync(new() { Path = Path.Combine(host.Artifacts, $"sos-alert-{engine}.png"), FullPage = true });
            await sos.GetByRole(AriaRole.Button, new() { Name = "I have seen this — acknowledge", Exact = true }).ClickAsync(); await Assertions.Expect(sos).ToHaveCountAsync(0);
            await admin.GetByRole(AriaRole.Button, new() { Name = "View acknowledgements", Exact = true }).ClickAsync(); await Assertions.Expect(admin.GetByRole(AriaRole.Dialog)).ToContainTextAsync("1 of 1 acknowledged");
            await admin.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
            var student = await Login(browser, host, "Student", engine); await Publish(admin, "Urgent student briefing", "AllStudents");
            await Assertions.Expect(student.GetByRole(AriaRole.Dialog)).ToContainTextAsync("Urgent student briefing", new() { Timeout = 20000 });
            await Assertions.Expect(teacher.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
            await student.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "I have seen this — acknowledge", Exact = true }).ClickAsync();
            var parent = await Login(browser, host, "Parent", engine);
            await admin.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Audience") }).SelectOptionAsync("Role"); await admin.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex("^Role") }).SelectOptionAsync("Parent");
            await Publish(admin, "Urgent parent briefing", "Role");
            await Assertions.Expect(parent.GetByRole(AriaRole.Dialog)).ToContainTextAsync("Urgent parent briefing", new() { Timeout = 20000 });
            await parent.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "I have seen this — acknowledge", Exact = true }).ClickAsync();
            await Publish(admin, "Cancelled SOS test", "SpecificStaff"); await Assertions.Expect(teacher.GetByRole(AriaRole.Dialog)).ToContainTextAsync("Cancelled SOS test", new() { Timeout = 20000 });
            admin.Dialog += (_, d) => d.AcceptAsync();
            await admin.Locator("article").Filter(new() { Has = admin.GetByRole(AriaRole.Heading, new() { Name = "Cancelled SOS test", Exact = true }) }).GetByRole(AriaRole.Button, new() { Name = "Cancel SOS", Exact = true }).ClickAsync();
            await Assertions.Expect(teacher.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0, new() { Timeout = 20000 });
            Assert.False(await parent.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"));
        }
        finally { await host.DisposeAsync(); }
    }
}



