using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SchoolManagement.E2ETests;

[Collection("Phase1Restore")]
public class GovernmentFileImportBrowserTests
{
    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task GovernmentCsvTemplateDownloadPreviewAndImportPersist(string engine)
    {
        var own=new Phase1Fixture();
        try {
            await own.InitializeAsync(); using var playwright=await Playwright.CreateAsync();
            await using var browser=engine=="chrome" ? await playwright.Chromium.LaunchAsync(new(){Channel="chrome",Headless=true}) : await playwright.Webkit.LaunchAsync(new(){Headless=true});
            await using var context=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=engine=="webkit",AcceptDownloads=true,ViewportSize=new(){Width=390,Height=844},IsMobile=true,HasTouch=true});
            var page=await context.NewPageAsync(); await own.BeforeLogin(); await page.GotoAsync(own.BaseUrl+"/login");
            await page.GetByLabel("Staff ID or email address",new(){Exact=true}).FillAsync(Phase1Fixture.Email("Admin"));
            await page.GetByLabel("Password",new(){Exact=true}).FillAsync(own.Password); await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard");
            await page.GotoAsync(own.BaseUrl+"/school-calendar"); await page.GetByRole(AriaRole.Button,new(){Name="Add holidays",Exact=true}).ClickAsync();
            await page.GetByRole(AriaRole.Button,new(){NameRegex=new Regex("^Upload government calendar")}).ClickAsync();
            var download=await page.RunAndWaitForDownloadAsync(()=>page.GetByRole(AriaRole.Button,new(){Name="Download CSV template",Exact=true}).ClickAsync());
            Assert.EndsWith(".csv",download.SuggestedFilename); Assert.Null(await download.FailureAsync());
            var name="QA uploaded calendar "+engine;
            await page.GetByLabel("Government calendar file",new(){Exact=true}).SetInputFilesAsync(new FilePayload {Name="school-calendar.csv",MimeType="text/csv",Buffer=Encoding.UTF8.GetBytes($"From,Through,Name\n{DateTime.UtcNow.Year}-12-28,,{name}\n")});
            await page.GetByRole(AriaRole.Button,new(){Name="Preview file",Exact=true}).ClickAsync();
            await Assertions.Expect(page.GetByText(name,new(){Exact=true})).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Checkbox,new(){Name="Select file row 2",Exact=true})).ToBeCheckedAsync();
            await page.GetByRole(AriaRole.Button,new(){Name="Import 1 selected row(s)",Exact=true}).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new(){HasTextRegex=new Regex("import",RegexOptions.IgnoreCase)})).ToBeVisibleAsync();
            Assert.True(await own.HasHoliday(name));
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"));
        } finally { await own.DisposeAsync(); }
    }
}
