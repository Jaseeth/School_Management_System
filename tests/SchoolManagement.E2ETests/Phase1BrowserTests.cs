using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SchoolManagement.E2ETests;

[CollectionDefinition("Phase1", DisableParallelization = true)]
public class Phase1Collection : ICollectionFixture<Phase1Fixture> { }

[Collection("Phase1")]
public class Phase1BrowserTests(Phase1Fixture host)
{
    public static IEnumerable<object[]> Matrix => from engine in new[] { "chrome", "webkit" } from mobile in new[] { false, true } select new object[] { engine, mobile };
    private async Task<IBrowser> Browser(IPlaywright playwright, string engine) => engine == "chrome"
        ? await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true })
        : await playwright.Webkit.LaunchAsync(new() { Headless = true });
    private BrowserNewContextOptions Options(IPlaywright playwright, string engine, bool mobile) => new() {
        ViewportSize = new() { Width = mobile ? 390 : 1440, Height = mobile ? 844 : 1000 },
        // Windows WebKit does not use the Windows trusted development-certificate store.
        // Chrome still verifies certificate trust; WebKit checks HTTPS with this local-only bypass.
        IsMobile = mobile, HasTouch = mobile, IgnoreHTTPSErrors = engine == "webkit",
        DeviceScaleFactor = mobile ? 2 : 1, Locale = "en-GB", TimezoneId = "Asia/Colombo"
    };
    private static async Task Login(IPage page, string role, Phase1Fixture host)
    {
        await host.BeforeLogin();
        await page.GotoAsync(host.BaseUrl + (role == "Student" ? "/student/login" : role == "Parent" ? "/parent/login" : "/login"));
        await page.Locator("input:not([type=password]):not([type=checkbox]):not([type=hidden])").First.FillAsync(role == "Student" ? "QA001" : Phase1Fixture.Email(role));
        await page.Locator("input[type=password]").FillAsync(host.Password);
        await page.Locator("button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/dashboard$"));
    }
    [Theory, MemberData(nameof(Matrix))]
    public async Task AllRoleNavigationAndMobileLayout(string engine, bool mobile)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await Browser(playwright, engine);
        var failures = new List<string>(); var checks = new List<object>();
        foreach (var role in Phase1Fixture.Roles) {
            await using var context = await browser.NewContextAsync(Options(playwright,engine,mobile));
            var page = await context.NewPageAsync(); page.SetDefaultTimeout(12000);
            var errors = new List<string>(); page.PageError += (_,e) => errors.Add(e);
            var responses = new List<string>();
            page.Response += (_,r) => { if (r.Url.Contains("/api/") && r.Status >= 400) responses.Add($"{r.Status} {new Uri(r.Url).AbsolutePath}"); };
            try {
                await Login(page, role, host); await page.WaitForTimeoutAsync(800);
                if (mobile && role is not ("Student" or "Parent")) {
                    await page.GetByRole(AriaRole.Button,new(){Name="Open menu",Exact=true}).ClickAsync();
                    await Assertions.Expect(page.GetByRole(AriaRole.Button,new(){Name="Close menu",Exact=true})).ToBeVisibleAsync();
                    await page.GetByRole(AriaRole.Button,new(){Name="Close menu",Exact=true}).ClickAsync();
                }
                var paths = new HashSet<string>();
                var hrefs = await page.Locator("a[href]").EvaluateAllAsync<string[]>("elements => elements.map(a => a.getAttribute('href'))");
                foreach (var href in hrefs.Where(h => h.StartsWith('/') && !h.Contains("logout"))) paths.Add(href);
                if (role is not ("Student" or "Parent")) paths.UnionWith(StaffPaths(role));
                if (role == "Admin") paths.Add("/school-settings?tab=backups");
                if (role == "Admin") { paths.Add($"/students/{host.StudentId}/reset-password"); paths.Add("/backups"); }
                if (role is "Admin" or "Principal" or "Deputy Principal" or "Section Head" or "Teacher") paths.Add($"/students/{host.StudentId}");
                if (role == "Student") paths.UnionWith(new[]{"/student/dashboard","/student/profile","/student/subjects","/student/attendance","/student/results","/student/timetable","/student/announcements","/student/notifications","/student/special-classes","/student/daily-schedule","/student/enrollment-history","/student/guardians","/student/change-password","/student/school-calendar"});
                if (role == "Parent") paths.UnionWith(new[]{"/parent/dashboard","/parent/profile","/parent/announcements","/parent/notifications","/parent/change-password","/parent/school-calendar"});
                if (role == "Parent") foreach (var child in new[] {"academic-profile","attendance","results","schedule"}) paths.Add($"/parent/children/{host.StudentId}/{child}");
                foreach (var path in paths.Order()) {
                    errors.Clear(); responses.Clear();
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    try {
                        await page.GotoAsync(host.BaseUrl + path); await page.WaitForLoadStateAsync(LoadState.NetworkIdle); watch.Stop();
                        var body = await page.Locator("body").InnerTextAsync();
                        var overflow = await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth + 2");
                        var issue = body.Contains("Page not found") || body.Contains("Access restricted") || errors.Count > 0 || responses.Count > 0 || overflow || body.Length < 30;
                        if (issue) failures.Add($"{role} {path}: overflow={overflow}; JS={string.Join(';',errors)}; API={string.Join(';',responses)}; text={body[..Math.Min(body.Length,130)]}");
                        checks.Add(new { role,path, engine,mobile, milliseconds=watch.ElapsedMilliseconds, overflow, javascriptErrors=errors.ToArray(), apiErrors=responses.ToArray(), passed=!issue });
                        if (issue || path.EndsWith("dashboard")) await page.ScreenshotAsync(new() { Path=Path.Combine(host.Artifacts,$"{engine}-{mobile}-{role.Replace(' ','-')}-{checks.Count}.png"), FullPage=true });
                        // Exercise safe tab/navigation controls; mutations are tested separately with explicit data.
                        foreach (var tab in await page.GetByRole(AriaRole.Tab).AllAsync()) if (await tab.IsVisibleAsync() && await tab.IsEnabledAsync()) { await tab.ClickAsync(); await page.WaitForTimeoutAsync(100); }
                        var names = await page.Locator("nav[aria-label] button").AllTextContentsAsync();
                        foreach (var name in names.Select(n=>n.Trim()).Distinct()) {
                            var tab = page.Locator("nav[aria-label]").GetByRole(AriaRole.Button,new(){Name=name,Exact=true});
                            if (await tab.CountAsync()==1 && await tab.IsVisibleAsync() && await tab.IsEnabledAsync()) {
                                await tab.ClickAsync(); await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                                if (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2")) failures.Add($"{role} {path} tab '{name}' has horizontal overflow");
                                if (errors.Count>0 || responses.Count>0) failures.Add($"{role} {path} tab '{name}': JS={string.Join(';',errors)}; API={string.Join(';',responses)}");
                                var forms=await page.Locator("form").CountAsync();
                                checks.Add(new { role,path,engine,mobile,tab=name,forms,passed=errors.Count==0&&responses.Count==0 });
                            }
                        }
                    } catch (Exception e) { failures.Add($"{role} {path}: {e.Message}"); }
                }
                if (role is "Teacher" or "Section Head" or "Librarian") {
                    await page.GotoAsync(host.BaseUrl+"/school-settings"); await Assertions.Expect(page.GetByRole(AriaRole.Heading,new(){Name="Access restricted"})).ToBeVisibleAsync();
                }
            } catch(Exception e) { failures.Add($"{role} login/navigation: {e.Message}"); }
        }
        await File.WriteAllTextAsync(Path.Combine(host.Artifacts,$"navigation-{engine}-{mobile}.json"),JsonSerializer.Serialize(new{checks,failures},new JsonSerializerOptions{WriteIndented=true}));
        Assert.True(failures.Count==0,string.Join(Environment.NewLine,failures));
    }
    private static string[] StaffPaths(string role)
    {
        var common = new[] {"/dashboard","/staff/my-profile","/staff-leave","/school-calendar","/notifications"};
        if (role == "Teacher") return [..common,"/announcements","/special-classes","/teacher/timetable","/teacher/today","/teacher/attendance","/teacher/marks"];
        if (role == "Section Head") return [..common,"/students","/attendance","/staff-attendance","/announcements","/special-classes","/reports","/section-head/attendance-approvals","/section-head/marks-review","/results/publishing"];
        if (role == "Librarian") return common;
        return [..common,"/students","/students/add","/staff","/parents","/academic-setup","/promotion","/teacher-assignments","/timetable","/attendance","/staff-attendance","/announcements","/special-classes","/reports","/audit-logs","/results/publishing","/exams","/school-settings",..(role=="Admin"?new[]{"/section-head-assignments","/staff-leave-allowance"}:Array.Empty<string>())];
    }
    [Theory, MemberData(nameof(Matrix))]
    public async Task PublicLinksLoginValidationCursorAndHttps(string engine, bool mobile)
    {
        using var playwright=await Playwright.CreateAsync(); await using var browser=await Browser(playwright,engine);
        await using var context=await browser.NewContextAsync(Options(playwright,engine,mobile)); var page=await context.NewPageAsync();
        foreach(var path in new[]{"/","/login","/student/login","/parent/login","/student/forgot-password","/staff/forgot-password","/student/register"}) {
            await page.GotoAsync(host.BaseUrl+path); await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.True(await page.EvaluateAsync<bool>("window.isSecureContext && location.protocol === 'https:'"));
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 2"),$"Mobile overflow: {path}");
            foreach(var control in await page.Locator("button:visible:not(:disabled),a[href]:visible").AllAsync()) Assert.Equal("pointer",await control.EvaluateAsync<string>("e => getComputedStyle(e).cursor"));
            foreach(var link in await page.Locator("a[href]").AllAsync()) {
                var href=await link.GetAttributeAsync("href"); Assert.False(string.IsNullOrWhiteSpace(href)||href=="#",$"Empty link on {path}");
            }
            if(path.EndsWith("/login")) {
                await page.Locator("button[type=submit]").ClickAsync();
                Assert.EndsWith(path,page.Url);
                Assert.True(await page.EvaluateAsync<bool>("!!document.querySelector(':invalid')") || (await page.Locator("body").InnerTextAsync()).Contains("Please"));
                var toggle=page.GetByRole(AriaRole.Button,new(){Name="Show password",Exact=true});
                if(await toggle.CountAsync()>0) { await toggle.ClickAsync(); Assert.Equal("text",await page.Locator("input[autocomplete=current-password]").GetAttributeAsync("type")); }
            }
        }
        await page.GotoAsync(host.BaseUrl+"/route-that-does-not-exist"); await Assertions.Expect(page.GetByRole(AriaRole.Heading,new(){Name="Page not found"})).ToBeVisibleAsync();
        await File.WriteAllTextAsync(Path.Combine(host.Artifacts,$"public-{engine}-{mobile}.txt"),$"PASS: HTTPS (certificate trust verified by Chrome; local certificate bypass in Windows WebKit), public routes, overflow, pointer cursors, local links, empty-login validation and password toggles. Engine={engine}; mobile={mobile}");
    }
    [Theory, MemberData(nameof(Matrix))]
    public async Task SchoolSettingsAndAnnouncementFormsPersistAndTargetRecipients(string engine, bool mobile)
    {
        using var playwright=await Playwright.CreateAsync(); await using var browser=await Browser(playwright,engine);
        await using var context=await browser.NewContextAsync(Options(playwright,engine,mobile)); var page=await context.NewPageAsync();
        await Login(page,"Admin",host);
        await page.GotoAsync(host.BaseUrl+"/school-settings");
        var name=page.GetByLabel("School name",new(){Exact=true}); await name.FillAsync("QA School "+engine);
        await page.GetByRole(AriaRole.Button,new(){Name="Save school settings",Exact=true}).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("saved");
        await page.ReloadAsync(); await Assertions.Expect(name).ToHaveValueAsync("QA School "+engine);
        await page.GetByRole(AriaRole.Button,new(){Name="Backup and Restore",Exact=true}).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading,new(){Name="Backup and Restore",Exact=true})).ToBeVisibleAsync();
        await page.GotoAsync(host.BaseUrl+"/announcements");
        var title="QA announcement "+engine+" "+Guid.NewGuid().ToString("N")[..6];
        await page.GetByLabel("Title",new(){Exact=true}).FillAsync(title);
        await page.GetByLabel("Message",new(){Exact=true}).FillAsync("Private test database announcement.");
        await page.GetByLabel("Audience").SelectOptionAsync("AllStudents");
        await page.GetByRole(AriaRole.Button,new(){Name="Publish announcement",Exact=true}).ClickAsync();
        await Assertions.Expect(page.GetByText("Announcement published.",new(){Exact=true})).ToBeVisibleAsync();
        await using var studentContext=await browser.NewContextAsync(Options(playwright,engine,false)); var student=await studentContext.NewPageAsync();
        await Login(student,"Student",host); await student.GotoAsync(host.BaseUrl+"/student/announcements"); await Assertions.Expect(student.GetByText(title,new(){Exact=true})).ToBeVisibleAsync();
        await using var parentContext=await browser.NewContextAsync(Options(playwright,engine,false)); var parent=await parentContext.NewPageAsync();
        await Login(parent,"Parent",host); await parent.GotoAsync(host.BaseUrl+"/parent/announcements"); await Assertions.Expect(parent.GetByText(title,new(){Exact=true})).ToHaveCountAsync(0);
    }

    [Theory, MemberData(nameof(Matrix))]
    public async Task AcademicCalendarAndLeaveFormsSaveThroughTheBrowser(string engine, bool mobile)
    {
        using var playwright=await Playwright.CreateAsync(); await using var browser=await Browser(playwright,engine);
        await using var adminContext=await browser.NewContextAsync(Options(playwright,engine,mobile)); var admin=await adminContext.NewPageAsync();
        await Login(admin,"Admin",host); await admin.GotoAsync(host.BaseUrl+"/academic-setup");
        await admin.GetByRole(AriaRole.Button,new(){Name="Sections",Exact=true}).ClickAsync();
        var section=$"QA extra {engine} {mobile}"; await admin.GetByLabel("Name",new(){Exact=true}).FillAsync(section);
        await admin.GetByRole(AriaRole.Button,new(){Name="Create section",Exact=true}).ClickAsync();
        await Assertions.Expect(admin.GetByRole(AriaRole.Status).Filter(new(){HasText="created successfully"})).ToBeVisibleAsync();
        await admin.ReloadAsync(); await admin.GetByRole(AriaRole.Button,new(){Name="Sections",Exact=true}).ClickAsync();
        await Assertions.Expect(admin.GetByRole(AriaRole.Heading,new(){Name=section,Exact=true})).ToBeVisibleAsync();
        await admin.GotoAsync(host.BaseUrl+"/school-calendar");
        await admin.GetByRole(AriaRole.Button,new(){Name="Add holidays",Exact=true}).ClickAsync();
        var holiday=$"QA holiday {engine} {mobile}";
        var day=$"{DateTime.UtcNow.Year}-12-{(engine=="chrome"?14:15)+(mobile?7:0)}";
        await admin.GetByLabel("From",new(){Exact=true}).FillAsync(day); await admin.GetByLabel("Through",new(){Exact=true}).FillAsync(day);
        await admin.GetByLabel("Holiday / break name",new(){Exact=true}).FillAsync(holiday);
        await admin.GetByRole(AriaRole.Button,new(){Name="Save holiday",Exact=true}).ClickAsync();
        await Assertions.Expect(admin.Locator("body")).ToContainTextAsync("saved");
        await using var teacherContext=await browser.NewContextAsync(Options(playwright,engine,mobile)); var teacher=await teacherContext.NewPageAsync();
        await Login(teacher,"Teacher",host); await teacher.GotoAsync(host.BaseUrl+"/staff-leave");
        await teacher.GetByRole(AriaRole.Button,new(){Name="Request leave",Exact=true}).ClickAsync();
        var leaveDay=$"{DateTime.UtcNow.Year}-12-{(engine=="chrome"?16:17)+(mobile?7:0)}";
        var reason=$"QA leave workflow {engine} {mobile}";
        await teacher.GetByLabel("From date",new(){Exact=true}).FillAsync(leaveDay); await teacher.GetByLabel("To date",new(){Exact=true}).FillAsync(leaveDay);
        await teacher.GetByLabel("Reason",new(){Exact=true}).FillAsync(reason);
        await teacher.GetByRole(AriaRole.Button,new(){Name="Submit leave request",Exact=true}).ClickAsync();
        await Assertions.Expect(teacher.Locator("body")).ToContainTextAsync("Leave request submitted");
        await admin.GotoAsync(host.BaseUrl+"/staff-leave"); await admin.GetByRole(AriaRole.Button,new(){NameRegex=new Regex("Pending approvals")}).ClickAsync();
        var request=admin.Locator("article").Filter(new(){HasText=reason});
        await request.GetByRole(AriaRole.Button,new(){Name="Approve",Exact=true}).ClickAsync();
        await admin.GetByRole(AriaRole.Button,new(){Name="Confirm approval",Exact=true}).ClickAsync();
        await Assertions.Expect(admin.Locator("body")).ToContainTextAsync("successfully");
        await teacher.GotoAsync(host.BaseUrl+"/staff-leave"); await Assertions.Expect(teacher.GetByRole(AriaRole.Row).Filter(new(){HasText=reason})).ToContainTextAsync("Approved");
        await teacher.GotoAsync(host.BaseUrl+"/notifications"); await Assertions.Expect(teacher.Locator("body")).ToContainTextAsync("Staff leave approved");
    }

    [Theory, InlineData("chrome"), InlineData("webkit")]
    public async Task TemporaryPasswordMustBeChangedBeforeEnteringPortal(string engine)
    {
        using var playwright=await Playwright.CreateAsync(); await using var browser=await Browser(playwright,engine);
        await using var context=await browser.NewContextAsync(Options(playwright,engine,true)); var page=await context.NewPageAsync();
        await host.BeforeLogin(); await page.GotoAsync(host.BaseUrl+"/login");
        await page.GetByLabel("Staff ID or email address",new(){Exact=true}).FillAsync($"qa-forced-{engine}@example.test");
        await page.GetByLabel("Password",new(){Exact=true}).FillAsync(host.Password); await page.Locator("button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(new Regex("/change-password$"));
        await page.GetByLabel("Current temporary password",new(){Exact=true}).FillAsync(host.Password);
        await page.GetByLabel("New password",new(){Exact=true}).FillAsync(host.Password+"Z");
        await page.GetByLabel("Confirm new password",new(){Exact=true}).FillAsync(host.Password+"Z");
        await page.GetByRole(AriaRole.Button,new(){Name="Change password",Exact=true}).ClickAsync();
        await page.WaitForURLAsync(new Regex("/dashboard$"));
        await Assertions.Expect(page.Locator("body")).ToContainTextAsync("Welcome");
    }

    [Theory, MemberData(nameof(Matrix))]
    public async Task DashboardCardsAndHeaderBellActuallyNavigate(string engine, bool mobile)
    {
        using var playwright=await Playwright.CreateAsync(); await using var browser=await Browser(playwright,engine);
        await using var context=await browser.NewContextAsync(Options(playwright,engine,mobile)); var page=await context.NewPageAsync();
        await Login(page,"Student",host);
        var cards=new Dictionary<string,string> {
            ["My Profile"]="profile",["My Subjects"]="subjects",["Attendance"]="attendance",["Results"]="results",
            ["Timetable"]="timetable",["Announcements"]="announcements",["Notifications"]="notifications",
            ["Special Classes"]="special-classes",["Daily Schedule"]="daily-schedule",["Enrollment History"]="enrollment-history",
            ["My Guardians"]="guardians",["Change Password"]="change-password",["School Calendar"]="school-calendar"
        };
        foreach(var card in cards) {
            await page.GotoAsync(host.BaseUrl+"/student/dashboard");
            var name=new Regex("^"+Regex.Escape(card.Key)+"(?:\\s|$)");
            var control=page.GetByRole(AriaRole.Button,new(){NameRegex=name}).Or(page.GetByRole(AriaRole.Link,new(){NameRegex=name}));
            await control.ClickAsync(); await page.WaitForURLAsync(new Regex("/student/"+card.Value+"$"));
        }
        await using var staffContext=await browser.NewContextAsync(Options(playwright,engine,mobile)); var staff=await staffContext.NewPageAsync();
        await Login(staff,"Admin",host); await staff.GetByRole(AriaRole.Button,new(){Name="Notifications",Exact=true}).ClickAsync();
        await staff.WaitForURLAsync(new Regex("/notifications$"));
        if(mobile) await staff.GetByRole(AriaRole.Button,new(){Name="Open menu",Exact=true}).ClickAsync();
        await staff.GetByRole(AriaRole.Link,new(){Name="School Settings",Exact=true}).ClickAsync();
        await staff.WaitForURLAsync(new Regex("/school-settings$"));
    }
}
