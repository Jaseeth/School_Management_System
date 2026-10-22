using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.E2ETests;

// An actual API and production frontend, with an exclusively owned disposable SQL database.
public sealed class Phase1Fixture : IAsyncLifetime
{
    public string Root { get; } = FindRoot();
    public string Password { get; } = "Qa!" + Guid.NewGuid().ToString("N") + "7a";
    public string DatabaseName { get; } = "SchoolManagement_E2E_" + Guid.NewGuid().ToString("N");
    public string BaseUrl { get; private set; } = "";
    public int StudentId { get; private set; }
    public string Artifacts => Path.Combine(Root, ".local-tools", "qa", "browser");
    private Process? api, web;
    private ApplicationDbContext? db;
    private ServiceProvider? services;
    private readonly Queue<DateTime> logins = new();
    public async Task BeforeLogin()
    {
        // Exercise the real production limiter; never disable it just to make tests pass.
        while (logins.Count > 0 && DateTime.UtcNow - logins.Peek() >= TimeSpan.FromSeconds(62)) logins.Dequeue();
        if (logins.Count >= 8) {
            var wait = TimeSpan.FromSeconds(62) - (DateTime.UtcNow - logins.Peek());
            if (wait > TimeSpan.Zero) await Task.Delay(wait);
            while (logins.Count > 0 && DateTime.UtcNow - logins.Peek() >= TimeSpan.FromSeconds(62)) logins.Dequeue();
        }
        logins.Enqueue(DateTime.UtcNow);
    }
    public static readonly string[] Roles = ["Admin", "Principal", "Deputy Principal", "Section Head", "Teacher", "Student", "Parent", "Librarian"];
    public static string Email(string role) => role switch { "Admin" => "admin@school.com", "Teacher" => "teacher1@school.com", _ => "qa-" + role.ToLowerInvariant().Replace(" ", "-") + "@example.test" };
    public Task<bool> HasHoliday(string name) => db!.StaffLeaveHolidays.AsNoTracking().AnyAsync(x => x.Name == name && x.IsActive);
    public async Task AddStaffTeachingSubject()
    {
        db!.Subjects.Add(new Subject { Name = "QA English" }); await db.SaveChangesAsync();
    }
    public async Task AddBulkTeachingScenario()
    {
        var english = new Subject { Name = "QA English" };
        var otherClass = new SchoolClass { Name = "B", Grade = new Grade { Name = "10", Section = new Section { Name = "QA Biology" } } };
        db!.AddRange(english, otherClass); await db.SaveChangesAsync();
        var math = await db.Subjects.Where(s => s.Name == "QA Mathematics").Select(s => s.Id).SingleAsync();
        foreach (var role in new[] { "Admin", "Section Head" }) {
            var email = Email(role); var staff = await db.Staff.SingleAsync(s => db.Users.Any(u => u.Id == s.ApplicationUserId && u.Email == email));
            staff.CanTeach = true; staff.TeachingSubjects.Add(new() { SubjectId = english.Id }); staff.TeachingSubjects.Add(new() { SubjectId = math });
        }
        await db.SaveChangesAsync();
    }
    public async Task AddMarksReviewerScenario()
    {
        var users = services!.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "qa-reviewer@example.test", Email = "qa-reviewer@example.test", FullName = "QA Reviewer", EmailConfirmed = true, IsActive = true };
        Check(await users.CreateAsync(user, Password)); Check(await users.AddToRoleAsync(user, "Teacher"));
        var teacher = new Staff { StaffNumber = "QA-REVIEWER", FullName = "QA Reviewer", ApplicationUserId = user.Id };
        db!.Staff.Add(teacher); await db.SaveChangesAsync();
        var source = await db.TeacherAssignments.AsNoTracking().FirstAsync();
        db.TeacherAssignments.Add(new() { StaffId = teacher.Id, AcademicYearId = source.AcademicYearId, SchoolClassId = source.SchoolClassId, SubjectId = source.SubjectId });
        var exam = new Exam { Name = "QA Review Exam", MaximumMarks = 100, AcademicTermId = await db.AcademicTerms.Select(t => t.Id).FirstAsync() };
        db.Exams.Add(exam); await db.SaveChangesAsync();
        db.StudentMarks.Add(new() { StudentId = StudentId, ExamId = exam.Id, TeacherAssignmentId = source.Id, MarksObtained = 75, IsSubmitted = true });
        db.MarksSubmissions.Add(new() { ExamId = exam.Id, TeacherAssignmentId = source.Id, SubmittedByStaffId = source.StaffId, Status = SchoolManagement.Domain.Enums.MarksSubmissionStatus.Submitted });
        await db.SaveChangesAsync();
    }
    public async Task AddLeadershipStudent()
    {
        var enrollment = await db!.StudentAcademicEnrollments.AsNoTracking().FirstAsync();
        var student = new Student { IndexNumber = "QA002", FullName = "QA Second Student", SchoolClassId = enrollment.SchoolClassId };
        db.Students.Add(student); await db.SaveChangesAsync();
        db.StudentAcademicEnrollments.Add(new() { StudentId = student.Id, AcademicYearId = enrollment.AcademicYearId,
            SchoolClassId = enrollment.SchoolClassId, CreatedByStaffId = enrollment.CreatedByStaffId, EnrollmentDate = DateOnly.FromDateTime(DateTime.Today) });
        await db.SaveChangesAsync();
    }
    private static string FindRoot() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SchoolManagementSystem.slnx"))) dir = dir.Parent; return dir?.FullName ?? throw new InvalidOperationException("Repository not found"); }
    private static int Port() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Artifacts);
        var sqlServer = Environment.GetEnvironmentVariable("SCHOOL_TEST_SQL_SERVER") ?? @"(localdb)\MSSQLLocalDB";
        db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server={sqlServer};Database={DatabaseName};Integrated Security=True;TrustServerCertificate=True").Options);
        await db.Database.EnsureCreatedAsync();
        var registration = new ServiceCollection().AddLogging().AddSingleton(db);
        registration.AddDataProtection().UseEphemeralDataProtectionProvider();
        registration.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services = registration.BuildServiceProvider();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var staff = new Dictionary<string, Staff>();
        var identities = new Dictionary<string, ApplicationUser>();
        foreach (var role in Roles) {
            Check(await roles.CreateAsync(new IdentityRole(role)));
            var user = new ApplicationUser { UserName = Email(role), Email = Email(role), FullName = "QA " + role, EmailConfirmed = true, IsActive = true };
            Check(await users.CreateAsync(user, Password)); Check(await users.AddToRoleAsync(user, role)); identities[role] = user;
            if (role is not ("Student" or "Parent")) { var member = new Staff { StaffNumber = "QA-" + role.Replace(" ", ""), FullName = "QA " + role, ApplicationUserId = user.Id }; staff[role] = member; db.Staff.Add(member); }
        }
        foreach (var engine in new[] { "chrome", "webkit" }) {
            var forced = new ApplicationUser { UserName=$"qa-forced-{engine}@example.test", Email=$"qa-forced-{engine}@example.test", FullName="QA forced password", IsActive=true, EmailConfirmed=true, MustChangePassword=true };
            Check(await users.CreateAsync(forced,Password)); Check(await users.AddToRoleAsync(forced,"Teacher"));
            db.Staff.Add(new Staff { StaffNumber="QA-FORCED-"+engine, FullName=forced.FullName, ApplicationUserId=forced.Id });
        }
        var yearNumber = DateTime.UtcNow.Year;
        var year = new AcademicYear { Name = $"{yearNumber}/{yearNumber+1}", StartDate = new(yearNumber,1,1), EndDate = new(yearNumber,12,31), IsActive = true };
        var section = new Section { Name = "QA Section" }; var grade = new Grade { Name = "11", Section = section }; var schoolClass = new SchoolClass { Name = "A", Grade = grade };
        var subject = new Subject { Name = "QA Mathematics" };
        db.AddRange(year, schoolClass, subject); await db.SaveChangesAsync();
        staff["Teacher"].CanTeach = true; staff["Teacher"].TeachingSubjects.Add(new StaffTeachingSubject { SubjectId = subject.Id });
        var term = new AcademicTerm { Name = "QA Term", AcademicYearId = year.Id, StartDate = year.StartDate, EndDate = year.EndDate };
        var student = new Student { IndexNumber = "QA001", FullName = "QA Student", ApplicationUserId = identities["Student"].Id, SchoolClassId = schoolClass.Id, DateOfBirth = new(yearNumber-16,1,1) };
        var parent = new ParentGuardian { ParentNumber = "QAP001", FullName = "QA Parent", Email = Email("Parent"), ApplicationUserId = identities["Parent"].Id };
        db.AddRange(term, student, parent);
        db.SchoolProfiles.Add(new SchoolProfile { Name = "QA School", CountryCode = "LK", TimeZoneId = "Asia/Colombo" });
        await db.SaveChangesAsync(); StudentId = student.Id;
        db.StudentParentGuardians.Add(new() { StudentId = student.Id, ParentGuardianId = parent.Id, Relationship = "Guardian" });
        db.StudentAcademicEnrollments.Add(new() { StudentId = student.Id, AcademicYearId = year.Id, SchoolClassId = schoolClass.Id, EnrollmentDate = DateOnly.FromDateTime(year.StartDate), CreatedByStaffId = staff["Admin"].Id });
        db.StudentSubjectEnrollments.Add(new() { StudentId = student.Id, SubjectId = subject.Id, AcademicYearId = year.Id, EnrolledByStaffId = staff["Admin"].Id });
        db.TeacherAssignments.Add(new() { StaffId = staff["Teacher"].Id, AcademicYearId = year.Id, SchoolClassId = schoolClass.Id, SubjectId = subject.Id });
        db.ClassTeacherAssignments.Add(new() { StaffId = staff["Teacher"].Id, AcademicYearId = year.Id, SchoolClassId = schoolClass.Id, AssignedByStaffId = staff["Admin"].Id });
        db.SectionHeadAssignments.Add(new() { StaffId = staff["Section Head"].Id, AcademicYearId = year.Id, SectionId = section.Id });
        db.StaffLeaveCalendars.Add(new() { Year = yearNumber, CountryCode = "LK", WeekendMask = 65, SchoolStart = new(yearNumber,1,1), SchoolEnd = new(yearNumber,12,31), UpdatedByStaffId = staff["Admin"].Id });
        db.StaffLeaveAllowances.Add(new() { Year = yearNumber, LeaveType = 0, Days = 40, UpdatedByStaffId = staff["Admin"].Id });
        await db.SaveChangesAsync();
        var apiPort = Port(); var webPort = Port(); BaseUrl = $"https://localhost:{webPort}";
        var common = new Dictionary<string,string> {
            ["ASPNETCORE_ENVIRONMENT"]="Development", ["DOTNET_ENVIRONMENT"]="Development",
            ["ConnectionStrings__DefaultConnection"]=db.Database.GetConnectionString()!,
            ["Jwt__Key"]="qa-only-signing-key-" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            ["DevelopmentUsers__AdminPassword"]=Password,["DevelopmentUsers__TeacherPassword"]=Password,
            ["Firebase__CredentialsPath"]="",["Firebase__Web__ApiKey"]="",["PublicHolidays__CalendarificApiKey"]="",
            ["Backups__Directory"]=Path.Combine(Artifacts, DatabaseName), ["Mailpit__BaseUrl"]="http://127.0.0.1:1"
        };
        api = Start("dotnet", [typeof(SchoolManagement.API.Controllers.AuthController).Assembly.Location, "--urls", $"https://localhost:{apiPort}"], Path.Combine(Root,"src","SchoolManagement.API"), common, "api");
        await Ready($"https://localhost:{apiPort}/health");
        var webEnvironment = new Dictionary<string,string> {
            ["SCHOOL_API_PROXY_TARGET"]=$"https://localhost:{apiPort}",
            ["SCHOOL_HTTPS_CERT"]=Path.Combine(Root,".local-tools","https","localhost.pem"),
            ["SCHOOL_HTTPS_KEY"]=Path.Combine(Root,".local-tools","https","localhost.key")
        };
        web = Start("node", [Path.Combine(Root,"school-management-web","node_modules","vite","bin","vite.js"), "preview", "--host", "localhost", "--port", webPort.ToString(), "--strictPort"], Path.Combine(Root,"school-management-web"), webEnvironment, "web");
        await Ready(BaseUrl);
    }
    private static void Check(IdentityResult result) { if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description))); }
    private Process Start(string executable, string[] arguments, string cwd, Dictionary<string,string> environment, string log)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory=cwd, UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
        foreach (var a in arguments) start.ArgumentList.Add(a);
        foreach (var pair in environment) start.Environment[pair.Key]=pair.Value;
        var process = new Process { StartInfo=start };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) { lock(this) File.AppendAllText(Path.Combine(Artifacts,$"{log}-{DatabaseName}.log"), e.Data+Environment.NewLine); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) { lock(this) File.AppendAllText(Path.Combine(Artifacts,$"{log}-{DatabaseName}.log"), e.Data+Environment.NewLine); } };
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); return process;
    }
    private static async Task Ready(string url)
    {
        using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback=HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
        using var client = new HttpClient(handler) { Timeout=TimeSpan.FromSeconds(2) };
        for (var i=0;i<60;i++) { try { if ((await client.GetAsync(url)).IsSuccessStatusCode) return; } catch (HttpRequestException) {} catch (TaskCanceledException) {} await Task.Delay(1000); }
        throw new InvalidOperationException("QA server did not start. Inspect ignored QA logs.");
    }
    public async Task DisposeAsync()
    {
        foreach (var process in new[] {web,api}) if (process != null) { if (!process.HasExited) process.Kill(entireProcessTree:true); await process.WaitForExitAsync(); process.Dispose(); }
        if (db != null) {
            if (!db.Database.GetDbConnection().Database.StartsWith("SchoolManagement_E2E_", StringComparison.Ordinal)) throw new InvalidOperationException("Refusing to remove a non-test database");
            await db.Database.EnsureDeletedAsync(); await db.DisposeAsync();
        }
        if (services != null) await services.DisposeAsync();
    }
}
