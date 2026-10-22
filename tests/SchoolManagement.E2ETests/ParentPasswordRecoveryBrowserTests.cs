using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Playwright;

namespace SchoolManagement.E2ETests;

[Collection("Phase1Restore")]
public class ParentPasswordRecoveryBrowserTests
{
    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task ParentRecoveryMobileFlowHandlesInvalidCodeMismatchAndSuccessfulReset(string engine)
    {
        var own = new Phase1Fixture();
        try
        {
            await own.InitializeAsync(); using var playwright = await Playwright.CreateAsync();
            await using var browser = engine == "chrome" ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true })
                : await playwright.Webkit.LaunchAsync(new() { Headless = true });
            await using var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = engine == "webkit",
                ViewportSize = new() { Width = 390, Height = 844 }, IsMobile = true, HasTouch = true });
            var page = await context.NewPageAsync();
            var errors = new List<string>(); page.PageError += (_, text) => errors.Add(text);
            var resets = 0;
            // UI contract test uses synthetic responses; real reset security is tested against SQL separately.
            await page.RouteAsync("**/api/auth/parent-forgot-password/**", async route =>
            {
                var payload = JsonDocument.Parse(route.Request.PostData!);
                var status = 200; object body;
                if (route.Request.Url.EndsWith("request-otp")) body = new { challengeId = Guid.NewGuid(),
                    message = "If an active parent account matches this email, a verification code has been sent.", resendAfterSeconds = 60 };
                else if (route.Request.Url.EndsWith("verify-otp"))
                {
                    if (payload.RootElement.GetProperty("code").GetString() != "222222")
                    { status = 400; body = new { message = "The code is invalid or expired." }; }
                    else body = new { resetGrant = "synthetic-ui-grant", message = "Email verified. Choose a new password." };
                }
                else { resets++; body = new { message = "Password reset successfully. Sign in with your new password." }; }
                await route.FulfillAsync(new() { Status = status, ContentType = "application/json", Body = JsonSerializer.Serialize(body) });
            });
            await page.GotoAsync(own.BaseUrl + "/parent/login");
            await page.GetByRole(AriaRole.Link, new() { Name = "Forgot password?", Exact = true }).ClickAsync();
            await page.WaitForURLAsync("**/parent/forgot-password");
            await page.GetByLabel(new Regex("^Registered email")).FillAsync("parent@example.test");
            await page.GetByRole(AriaRole.Button, new() { Name = "Send verification code", Exact = true }).ClickAsync();
            await page.GetByLabel(new Regex("^Verification code")).FillAsync("000000");
            await page.GetByRole(AriaRole.Button, new() { Name = "Verify code", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("invalid or expired");
            await page.GetByLabel(new Regex("^Verification code")).FillAsync("222222");
            await page.GetByRole(AriaRole.Button, new() { Name = "Verify code", Exact = true }).ClickAsync();
            await page.GetByLabel(new Regex("^New password")).FillAsync("New!Pass7392");
            await page.GetByLabel(new Regex("^Confirm new password")).FillAsync("Different!7392");
            await page.GetByRole(AriaRole.Button, new() { Name = "Reset password", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("do not match"); Assert.Equal(0, resets);
            await page.GetByLabel(new Regex("^Confirm new password")).FillAsync("New!Pass7392");
            await page.GetByRole(AriaRole.Button, new() { Name = "Reset password", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Password reset successfully");
            Assert.Equal(1, resets); Assert.Empty(errors);
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"));
            Assert.Null(await page.EvaluateAsync<string?>("localStorage.getItem('parentAccessToken')"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(own.Artifacts, $"parent-recovery-{engine}.png"), FullPage = true });
            await page.GetByRole(AriaRole.Link, new() { Name = "Back to Parent Login", Exact = true }).Last.ClickAsync();
            await page.WaitForURLAsync("**/parent/login");
        }
        finally { await own.DisposeAsync(); }
    }
}
