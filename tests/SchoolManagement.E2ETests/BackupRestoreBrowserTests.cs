using Microsoft.Playwright;

namespace SchoolManagement.E2ETests;

[CollectionDefinition("Phase1Restore", DisableParallelization = true)]
public class Phase1RestoreCollection { }

[Collection("Phase1Restore")]
public class BackupRestoreBrowserTests
{
    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task BackupDownloadCancelAndConfirmedRestoreWorkInAnIsolatedSchool(string engine)
    {
        // Restore must never rewind the shared fixture used by other workflows.
        var own = new Phase1Fixture();
        try {
            await own.InitializeAsync();
            using var playwright=await Playwright.CreateAsync();
            await using var browser=engine=="chrome"
                ? await playwright.Chromium.LaunchAsync(new(){Channel="chrome",Headless=true})
                : await playwright.Webkit.LaunchAsync(new(){Headless=true});
            await using var context=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=engine=="webkit",AcceptDownloads=true,ViewportSize=new(){Width=1440,Height=1000}});
            var page=await context.NewPageAsync(); page.SetDefaultTimeout(30000);
            async Task Login() {
                await own.BeforeLogin(); await page.GotoAsync(own.BaseUrl+"/login");
                await page.GetByLabel("Staff ID or email address",new(){Exact=true}).FillAsync(Phase1Fixture.Email("Admin"));
                await page.GetByLabel("Password",new(){Exact=true}).FillAsync(own.Password);
                await page.Locator("button[type=submit]").ClickAsync(); await page.WaitForURLAsync("**/dashboard");
            }
            await Login(); await page.GotoAsync(own.BaseUrl+"/school-settings?tab=backups");
            await page.GetByRole(AriaRole.Button,new(){Name="Create backup now",Exact=true}).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new(){HasText="Backup created and verified"})).ToBeVisibleAsync(new(){Timeout=60000});
            var download=await page.RunAndWaitForDownloadAsync(()=>page.GetByRole(AriaRole.Button,new(){Name="Download",Exact=true}).ClickAsync());
            Assert.EndsWith(".bak",download.SuggestedFilename); Assert.Null(await download.FailureAsync());
            await page.GetByRole(AriaRole.Button,new(){Name="Restore…",Exact=true}).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button,new(){Name="Cancel",Exact=true}).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
            await page.GotoAsync(own.BaseUrl+"/school-settings");
            await page.GetByLabel("School name",new(){Exact=true}).FillAsync("Changed after backup");
            await page.GetByRole(AriaRole.Button,new(){Name="Save school settings",Exact=true}).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("saved");
            await page.GotoAsync(own.BaseUrl+"/school-settings?tab=backups");
            await page.GetByRole(AriaRole.Button,new(){Name="Restore…",Exact=true}).ClickAsync();
            await page.GetByLabel("Reason",new(){Exact=true}).FillAsync("Isolated browser restore verification");
            await page.GetByLabel("Your current Admin password",new(){Exact=true}).FillAsync(own.Password);
            await page.GetByLabel("Type RESTORE to confirm",new(){Exact=true}).FillAsync("RESTORE");
            var response=await page.RunAndWaitForResponseAsync(()=>page.GetByRole(AriaRole.Button,new(){Name="Restore all records",Exact=true}).ClickAsync(),r=>r.Url.EndsWith("/restore")&&r.Request.Method=="POST",new(){Timeout=90000});
            Assert.Equal(200,response.Status);
            await Login(); await page.GotoAsync(own.BaseUrl+"/school-settings");
            await Assertions.Expect(page.GetByLabel("School name",new(){Exact=true})).ToHaveValueAsync("QA School");
            await page.GotoAsync(own.BaseUrl+"/school-settings?tab=backups");
            await Assertions.Expect(page.Locator("body")).ToContainTextAsync("Completed");
            await Assertions.Expect(page.Locator("body")).ToContainTextAsync("Before restore");
            await page.ScreenshotAsync(new(){Path=Path.Combine(own.Artifacts,$"restore-completed-{engine}.png"),FullPage=true});
        } finally { await own.DisposeAsync(); }
    }
}
