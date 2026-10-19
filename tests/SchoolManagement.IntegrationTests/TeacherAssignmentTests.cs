using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SchoolManagement.API.Services;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.API.Controllers;
using SchoolManagement.Application.ClassTeachers.DTOs;
using SchoolManagement.Application.TeacherAssignments.DTOs;
using SchoolManagement.Application.Timetables.DTOs;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.IntegrationTests;

public class TeacherAssignmentTests
{
    [Fact]
    public void GovernmentCalendarReaderValidatesCsvAndExcelWithoutEvaluatingFormulas()
    {
        using var csv = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("From,Through,Name\r\n2026-02-01,2026-03-02,\"Ramadan vacation, approved\"\r\n2026-12-25,,Holiday\r\n2027-01-01,,Wrong year\r\n03/04/2026,,Ambiguous date\r\n2026-05-05,2026-05-01,Backwards\r\n"));
        var rows = GovernmentCalendarReader.Read(csv, ".csv", 2026);
        Assert.Equal(30, rows[0].Days); Assert.Equal("Ramadan vacation, approved", rows[0].Name);
        Assert.Equal(1, rows[1].Days); Assert.Null(rows[1].Error);
        Assert.All(rows.Skip(2), row => Assert.NotNull(row.Error));
        using var workbook = new ClosedXML.Excel.XLWorkbook(); var sheet = workbook.AddWorksheet("Calendar");
        sheet.Cell(1, 1).Value = "Date"; sheet.Cell(1, 2).Value = "Holiday";
        sheet.Cell(2, 1).Value = new DateTime(2026, 4, 14); sheet.Cell(2, 2).Value = "Approved school holiday";
        using var excel = new MemoryStream(); workbook.SaveAs(excel); excel.Position = 0;
        var excelRows = GovernmentCalendarReader.Read(excel, ".xlsx", 2026);
        Assert.Equal(new DateOnly(2026, 4, 14), excelRows[0].From); Assert.Null(excelRows[0].Error);
        sheet.Cell(2, 2).FormulaA1 = "1+1";
        using var formulaFile = new MemoryStream(); workbook.SaveAs(formulaFile); formulaFile.Position = 0;
        Assert.Throws<CalendarFileException>(() => GovernmentCalendarReader.Read(formulaFile, ".xlsx", 2026));
        using var pdf = new MemoryStream([1, 2, 3]);
        Assert.Throws<CalendarFileException>(() => GovernmentCalendarReader.Read(pdf, ".pdf", 2026));
    }

    [Fact]
    public async Task GovernmentCalendarUploadPreviewsBeforeImportAndAppliesClosuresToSharedCalendar()
    {
        await using var db = await TestDatabase.CreateAsync();
        StaffLeaveController Controller(string id) => new(db.Context, db.Users, new LeavePushFailure()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "Test")) } } };
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var admin = Controller(db.AdminUserId); var teacher = Controller(db.TeacherUserIds[0]);
        Assert.IsType<ForbidResult>(await teacher.CalendarTemplate(2026));
        var template = Assert.IsType<FileContentResult>(await admin.CalendarTemplate(2026));
        using var templateStream = new MemoryStream(template.FileContents);
        Assert.Throws<CalendarFileException>(() => GovernmentCalendarReader.Read(templateStream, ".xlsx", 2026)); // Empty template never imports fake holidays.
        Assert.IsType<OkObjectResult>(await admin.SaveCalendar(new(2026, "LK", null, [0, 6])));
        Assert.IsType<OkObjectResult>(await admin.SaveHoliday(new(new DateOnly(2026, 2, 4), "Existing national holiday")));
        using var csv = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("From,Through,Name\n2026-02-01,2026-03-02,Ramadan vacation\n2026-02-10,,Overlapping holiday\n2027-01-01,,Wrong year\n"));
        var file = new FormFile(csv, 0, csv.Length, "file", "calendar.csv");
        Assert.IsType<ForbidResult>(await teacher.PreviewGovernmentCalendar(2026, file, cache));
        var preview = Assert.IsType<OkObjectResult>(await admin.PreviewGovernmentCalendar(2026, file, cache));
        var json = JsonSerializer.SerializeToElement(preview.Value);
        var token = json.GetProperty("previewToken").GetString()!;
        Assert.Equal(3, json.GetProperty("rows").GetArrayLength());
        Assert.Equal(1, await db.Context.StaffLeaveHolidays.CountAsync()); // Preview writes nothing.
        Assert.IsType<ForbidResult>(await teacher.ImportGovernmentCalendar(new(token, [2]), cache));
        Assert.IsType<BadRequestObjectResult>(await admin.ImportGovernmentCalendar(new(token, [4]), cache));
        var imported = Assert.IsType<OkObjectResult>(await admin.ImportGovernmentCalendar(new(token, [2, 3]), cache));
        Assert.Equal(29, JsonSerializer.SerializeToElement(imported.Value).GetProperty("added").GetInt32());
        Assert.Equal(30, await db.Context.StaffLeaveHolidays.CountAsync());
        Assert.Equal("Existing national holiday", await db.Context.StaffLeaveHolidays.Where(h => h.Date == new DateOnly(2026, 2, 4)).Select(h => h.Name).SingleAsync());
        Assert.Equal(0, await new StaffLeaveBalanceService(db.Context).WorkingDays(new(2026, 2, 1), new(2026, 3, 2)));
        var calendar = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await teacher.SchoolCalendar(2026)).Value);
        Assert.False(calendar.GetProperty("days").EnumerateArray().Single(d => d.GetProperty("date").GetString() == "2026-02-16").GetProperty("schoolDay").GetBoolean());
        Assert.IsType<ConflictObjectResult>(await admin.ImportGovernmentCalendar(new(token, [2]), cache)); // Consumed preview cannot be reused.
        using var wrongHeader = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Something,Else\nOne,Two\n"));
        Assert.IsType<BadRequestObjectResult>(await admin.PreviewGovernmentCalendar(2026, new FormFile(wrongHeader, 0, wrongHeader.Length, "file", "wrong.csv"), cache));
    }

    [Fact]
    public async Task HolidayImportIsAdminOnlyDeduplicatesAndPreservesExistingSchoolDates()
    {
        await using var db = await TestDatabase.CreateAsync();
        StaffLeaveController Controller(string id) => new(db.Context, db.Users, new LeavePushFailure()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "Test")) } } };
        var admin = Controller(db.AdminUserId); var teacher = Controller(db.TeacherUserIds[0]);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var handler = new HolidayHttpHandler();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PublicHolidays:CalendarificApiKey"] = "test-key" }).Build();
        var provider = new PublicHolidayService(new HttpClient(handler), configuration, cache);
        Assert.IsType<ForbidResult>(await teacher.PreviewPublicHolidays(2026, provider));
        Assert.IsType<BadRequestObjectResult>(await admin.PreviewPublicHolidays(2026, provider));
        Assert.IsType<OkObjectResult>(await admin.SaveCalendar(new(2026, "LK", null, [0, 6])));
        Assert.IsType<OkObjectResult>(await admin.SaveHoliday(new(new DateOnly(2026, 1, 1), "School-specific name")));
        db.Context.StaffLeaveHolidays.Add(new StaffLeaveHoliday { Date = new(2026, 12, 25), Name = "Previously excluded", IsActive = false, UpdatedByStaffId = db.AdminStaffId }); await db.Context.SaveChangesAsync();
        var preview = Assert.IsType<OkObjectResult>(await admin.PreviewPublicHolidays(2026, provider));
        var previewJson = JsonSerializer.SerializeToElement(preview.Value);
        Assert.Equal(3, previewJson.GetProperty("holidays").GetArrayLength());
        Assert.Equal(1, handler.Calls);
        Assert.IsType<ForbidResult>(await teacher.ImportPublicHolidays(new(2026, "LK", [new(2026, 2, 4)]), provider));
        Assert.IsType<BadRequestObjectResult>(await admin.ImportPublicHolidays(new(2026, "LK", [new(2026, 5, 5)]), provider));
        var imported = Assert.IsType<OkObjectResult>(await admin.ImportPublicHolidays(new(2026, "LK", [new(2026, 1, 1), new(2026, 2, 4), new(2026, 12, 25)]), provider));
        Assert.Equal(1, JsonSerializer.SerializeToElement(imported.Value).GetProperty("added").GetInt32());
        Assert.Equal("School-specific name", await db.Context.StaffLeaveHolidays.Where(h => h.Date == new DateOnly(2026, 1, 1)).Select(h => h.Name).SingleAsync());
        Assert.False(await db.Context.StaffLeaveHolidays.Where(h => h.Date == new DateOnly(2026, 12, 25)).Select(h => h.IsActive).SingleAsync());
        Assert.Equal(1, handler.Calls); // Preview and import reuse the provider cache.
        var repeated = Assert.IsType<OkObjectResult>(await admin.ImportPublicHolidays(new(2026, "LK", [new(2026, 2, 4)]), provider));
        Assert.Equal(0, JsonSerializer.SerializeToElement(repeated.Value).GetProperty("added").GetInt32());
        Assert.IsType<ConflictObjectResult>(await admin.ImportPublicHolidays(new(2026, "SA", [new(2026, 2, 4)]), provider));
        var missing = new PublicHolidayService(new HttpClient(handler), new ConfigurationBuilder().Build(), cache);
        Assert.IsType<ObjectResult>(await admin.PreviewPublicHolidays(2026, missing));
        Assert.Equal(3, await db.Context.StaffLeaveHolidays.CountAsync());
        using var errorCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var unavailable = new PublicHolidayService(new HttpClient(new HolidayHttpHandler { Status = System.Net.HttpStatusCode.TooManyRequests }), configuration, errorCache);
        var error = await Assert.ThrowsAsync<PublicHolidayException>(() => unavailable.Load("LK", 2026));
        Assert.DoesNotContain("test-key", error.Message);
        Assert.Equal(3, await db.Context.StaffLeaveHolidays.CountAsync());
    }

    private sealed class HolidayHttpHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public System.Net.HttpStatusCode Status { get; set; } = System.Net.HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent("""
                {"meta":{"code":200},"response":{"holidays":[
                  {"name":"Example nationwide holiday","date":{"iso":"2026-01-01"},"type":["National holiday"],"locations":"All"},
                  {"name":"Example public holiday","date":{"iso":"2026-02-04"},"type":["National holiday"],"locations":"All"},
                  {"name":"Second observance on same public date","date":{"iso":"2026-02-04"},"type":["National holiday"],"locations":"All"},
                  {"name":"Example year-end holiday","date":{"iso":"2026-12-25"},"type":["National holiday"],"locations":"All"},
                  {"name":"Bank only","date":{"iso":"2026-05-05"},"type":["Bank holiday"],"locations":"All"},
                  {"name":"Observance only","date":{"iso":"2026-06-06"},"type":["Observance"],"locations":"All"},
                  {"name":"Regional only","date":{"iso":"2026-07-07"},"type":["Public holiday"],"locations":"Western"},
                  {"name":"Wrong year","date":{"iso":"2027-01-01"},"type":["National holiday"],"locations":"All"}
                ]}}
                """) });
        }
    }

    [Fact]
    public async Task LeaveAllowancesExcludeConfiguredWeekendsAndWeekdayHolidaysAcrossYears()
    {
        await using var db = await TestDatabase.CreateAsync();
        StaffLeaveController Controller(string id) => new(db.Context, db.Users, new LeavePushFailure()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "Test")) } } };
        var admin = Controller(db.AdminUserId); var teacher = Controller(db.TeacherUserIds[0]);
        Assert.IsType<ForbidResult>(await teacher.LeaveSettings(2026));
        Assert.IsType<ForbidResult>(await teacher.SaveCalendar(new(2026, "LK", null, [0, 6])));
        Assert.IsType<OkObjectResult>(await admin.SaveCalendar(new(2026, "LK", "Western", [0, 6])));
        Assert.IsType<BadRequestObjectResult>(await admin.SaveCalendar(new(2026, "LK", null, [0, 1, 2, 3, 4, 5, 6])));
        Assert.IsType<OkObjectResult>(await admin.SaveHoliday(new(new DateOnly(2026, 1, 7), "Weekday public holiday")));
        Assert.IsType<OkObjectResult>(await admin.SaveHoliday(new(new DateOnly(2026, 1, 10), "Weekend public holiday")));
        Assert.IsType<OkObjectResult>(await admin.SaveHoliday(new(new DateOnly(2026, 1, 7), "Updated holiday name")));
        Assert.Equal(2, await db.Context.StaffLeaveHolidays.CountAsync());
        Assert.IsType<OkObjectResult>(await admin.SaveAllowance(new(2026, null, 40)));
        Assert.IsType<OkObjectResult>(await admin.SaveAllowance(new(2026, db.TeacherIds[0], 8)));
        var service = new StaffLeaveBalanceService(db.Context);
        Assert.Equal(5, await service.WorkingDays(new(2026, 1, 5), new(2026, 1, 12)));
        StaffLeaveRequest Leave(StaffLeaveStatus status, DateTime from, DateTime to) => new() { StaffId = db.TeacherIds[0], LeaveType = StaffLeaveType.Sick, FromDate = from, ToDate = to, Status = status, Reason = "Count check" };
        var used = Leave(StaffLeaveStatus.Approved, new(2026, 1, 5), new(2026, 1, 12));
        db.Context.AddRange(used, Leave(StaffLeaveStatus.Pending, new(2026, 1, 13), new(2026, 1, 13)), Leave(StaffLeaveStatus.Rejected, new(2026, 1, 14), new(2026, 1, 14)), Leave(StaffLeaveStatus.Cancelled, new(2026, 1, 15), new(2026, 1, 15))); await db.Context.SaveChangesAsync();
        var balance = (await service.Total(db.TeacherIds[0], 2026));
        Assert.Equal(8, balance.Allowance); Assert.Equal(5, balance.Used); Assert.Equal(1, balance.Pending); Assert.Equal(3, balance.Remaining);
        Assert.Equal(40, (await service.Total(db.TeacherIds[1], 2026)).Allowance);
        Assert.IsType<ForbidResult>(await teacher.LeaveBalances(2026, db.TeacherIds[1]));
        var preview = Assert.IsType<OkObjectResult>(await teacher.LeavePreview(StaffLeaveType.Sick, new(2026, 1, 19), new(2026, 1, 23)));
        Assert.True(JsonSerializer.SerializeToElement(preview.Value).GetProperty("years")[0].GetProperty("exceedsBalance").GetBoolean());
        // Early return restores counted days without altering the allowance.
        used.ToDate = new(2026, 1, 6); await db.Context.SaveChangesAsync();
        Assert.Equal(6, (await service.Total(db.TeacherIds[0], 2026)).Remaining);
        used.Status = StaffLeaveStatus.Cancelled; await db.Context.SaveChangesAsync();
        Assert.Equal(8, (await service.Total(db.TeacherIds[0], 2026)).Remaining);
        Assert.IsType<OkObjectResult>(await admin.SaveCalendar(new(2027, "SA", null, [5, 6])));
        Assert.IsType<OkObjectResult>(await admin.SaveHoliday(new(new DateOnly(2027, 1, 3), "Sunday school holiday")));
        Assert.Equal(2, await service.WorkingDays(new(2026, 12, 31), new(2027, 1, 4)));
        var holidayId = await db.Context.StaffLeaveHolidays.Where(h => h.Date == new DateOnly(2027, 1, 3)).Select(h => h.Id).SingleAsync();
        Assert.IsType<ForbidResult>(await teacher.RemoveHoliday(holidayId));
        Assert.IsType<OkObjectResult>(await admin.RemoveHoliday(holidayId));
        Assert.Equal(3, await service.WorkingDays(new(2026, 12, 31), new(2027, 1, 4)));
        Assert.False((await db.Context.StaffLeaveHolidays.FindAsync(holidayId))!.IsActive);
        // Sick and Casual leave share the same total, while reporting stays separate.
        var casual = Leave(StaffLeaveStatus.Approved, new(2026, 1, 14), new(2026, 1, 15)); casual.LeaveType = StaffLeaveType.Casual;
        used.Status = StaffLeaveStatus.Approved; used.ToDate = new(2026, 1, 12);
        db.Context.StaffLeaveRequests.Add(casual); await db.Context.SaveChangesAsync();
        var shared = await service.Total(db.TeacherIds[0], 2026);
        Assert.Equal(7, shared.Used); Assert.Equal(1, shared.Remaining);
        var breakdown = await service.Balances(db.TeacherIds[0], 2026);
        Assert.Equal(5, breakdown.Single(b => b.LeaveType == (int)StaffLeaveType.Sick).Used);
        Assert.Equal(2, breakdown.Single(b => b.LeaveType == (int)StaffLeaveType.Casual).Used);
        Assert.All(breakdown, b => Assert.Null(b.Allowance));
        Assert.IsType<OkObjectResult>(await admin.SaveCalendar(new(2026, "LK", null, [0, 6], new(2026, 1, 5), new(2026, 1, 30))));
        Assert.IsType<OkObjectResult>(await admin.SaveHoliday(new(new DateOnly(2026, 1, 20), "Term break", new DateOnly(2026, 1, 22))));
        var calendar = Assert.IsType<OkObjectResult>(await teacher.SchoolCalendar(2026));
        var calendarJson = JsonSerializer.SerializeToElement(calendar.Value);
        Assert.Equal(365, calendarJson.GetProperty("days").GetArrayLength());
        Assert.Equal(16, calendarJson.GetProperty("summary").GetProperty("schoolDays").GetInt32());
        Assert.Equal(16, await service.WorkingDays(new(2026, 1, 1), new(2026, 12, 31)));
        Assert.False(calendarJson.GetProperty("canManage").GetBoolean());
        Assert.IsType<BadRequestObjectResult>(await admin.SaveCalendar(new(2026, "LK", null, [0, 6], new(2026, 5, 1), new(2026, 4, 1))));
    }

    [Fact]
    public async Task StaffAttendanceShowsApprovedLeaveFlagsConflictsAndRetainsCorrections()
    {
        await using var db = await TestDatabase.CreateAsync();
        var day = AnnualPromotionService.SchoolToday; var date = DateOnly.FromDateTime(day);
        StaffAttendanceController Controller(string id) => new(db.Context, db.Users) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "Test")) } } };
        JsonElement Json(OkObjectResult result) => JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var admin = Controller(db.AdminUserId);
        Assert.IsType<ForbidResult>(await Controller(db.TeacherUserIds[0]).Day(date));
        var leave = new StaffLeaveRequest { StaffId = db.TeacherIds[0], FromDate = day, ToDate = day.AddDays(2), Reason = "Leave test", Status = StaffLeaveStatus.Pending };
        db.Context.StaffLeaveRequests.Add(leave); await db.Context.SaveChangesAsync();
        var request = new StaffAttendanceController.MarkRequest(db.TeacherIds[0], date, "Present", null, null);
        Assert.IsType<OkObjectResult>(await admin.Mark(request));
        leave = (await db.Context.StaffLeaveRequests.FindAsync(leave.Id))!; leave.Status = StaffLeaveStatus.Approved; await db.Context.SaveChangesAsync();
        var dayData = Json(Assert.IsType<OkObjectResult>(await admin.Day(date)));
        var row = dayData.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == db.TeacherIds[0]);
        Assert.True(row.GetProperty("reviewRequired").GetBoolean());
        var recordId = row.GetProperty("recordId").GetInt32();
        Assert.IsType<ConflictObjectResult>(await admin.Mark(request with { ExpectedRecordId = recordId }));
        Assert.IsType<BadRequestObjectResult>(await admin.Mark(request with { ExpectedRecordId = recordId, ConfirmLeaveConflict = true }));
        Assert.IsType<OkObjectResult>(await admin.Mark(request with { ExpectedRecordId = recordId, ConfirmLeaveConflict = true, Remarks = "Staff arrived; leave correction being reviewed" }));
        Assert.IsType<ConflictObjectResult>(await admin.Mark(request with { ExpectedRecordId = recordId, ConfirmLeaveConflict = true, Remarks = "Stale entry" }));
        dayData = Json(Assert.IsType<OkObjectResult>(await admin.Day(date)));
        row = dayData.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == db.TeacherIds[0]);
        Assert.True(row.GetProperty("conflict").GetBoolean()); Assert.False(row.GetProperty("reviewRequired").GetBoolean());
        Assert.Equal(2, Json(Assert.IsType<OkObjectResult>(await admin.History(db.TeacherIds[0], date))).GetProperty("rows").GetArrayLength());
        Assert.IsType<ForbidResult>(await Controller(db.TeacherUserIds[1]).Mark(request with { StaffId = db.TeacherIds[1] }));
        Assert.IsType<BadRequestObjectResult>(await admin.Mark(request with { Date = date.AddDays(1) }));
        // Unrecorded staff on approved leave appear automatically, not as absent or present.
        var secondLeave = new StaffLeaveRequest { StaffId = db.TeacherIds[1], FromDate = day, ToDate = day, Reason = "Approved leave", Status = StaffLeaveStatus.Approved };
        db.Context.StaffLeaveRequests.Add(secondLeave); await db.Context.SaveChangesAsync();
        dayData = Json(Assert.IsType<OkObjectResult>(await admin.Day(date)));
        Assert.Equal("On approved leave", dayData.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == db.TeacherIds[1]).GetProperty("status").GetString());
        leave = (await db.Context.StaffLeaveRequests.FindAsync(leave.Id))!;
        secondLeave.Status = StaffLeaveStatus.Cancelled; leave.ToDate = day.AddDays(-1); await db.Context.SaveChangesAsync();
        dayData = Json(Assert.IsType<OkObjectResult>(await admin.Day(date)));
        Assert.False(dayData.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == db.TeacherIds[0]).GetProperty("conflict").GetBoolean());
        Assert.Equal("Not marked", dayData.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == db.TeacherIds[1]).GetProperty("status").GetString());
        var headUser = await db.CreateUserAsync("attendance-head", "Section Head");
        var head = new Staff { StaffNumber = "ATT-HEAD", FullName = "Attendance head", ApplicationUserId = headUser.Id };
        db.Context.Staff.Add(head); await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId });
        db.Context.ClassTeacherAssignments.Add(new ClassTeacherAssignment { StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId, AcademicYearId = db.YearId, AssignedByStaffId = db.AdminStaffId }); await db.Context.SaveChangesAsync();
        var headData = Json(Assert.IsType<OkObjectResult>(await Controller(headUser.Id).Day(date)));
        Assert.False(headData.GetProperty("canMark").GetBoolean());
        Assert.Contains(headData.GetProperty("rows").EnumerateArray(), r => r.GetProperty("id").GetInt32() == db.TeacherIds[0]);
        Assert.DoesNotContain(headData.GetProperty("rows").EnumerateArray(), r => r.GetProperty("id").GetInt32() == db.TeacherIds[1]);
        Assert.IsType<ForbidResult>(await Controller(headUser.Id).History(db.TeacherIds[1], date));
        Assert.IsType<ForbidResult>(await Controller(headUser.Id).Mark(request));
    }

    [Fact]
    public async Task LeaveCancellationPreservesEarlierDaysAndRevokesOnlyAffectedCoverAfterApproval()
    {
        await using var db = await TestDatabase.CreateAsync();
        var day = AnnualPromotionService.SchoolToday.AddDays(10);
        StaffLeaveController Controller(string id) => new(db.Context, db.Users, new LeavePushFailure()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "Test")) } } };
        var owner = Controller(db.TeacherUserIds[0]); var admin = Controller(db.AdminUserId);
        var pending = new StaffLeaveRequest { StaffId = db.TeacherIds[0], FromDate = day, ToDate = day, Reason = "Pending" };
        db.Context.StaffLeaveRequests.Add(pending); await db.Context.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await Controller(db.TeacherUserIds[1]).Withdraw(pending.Id));
        Assert.IsType<OkObjectResult>(await owner.Withdraw(pending.Id));
        Assert.IsType<ConflictObjectResult>(await owner.Withdraw(pending.Id));
        Assert.Equal(StaffLeaveStatus.Cancelled, (await db.Context.StaffLeaveRequests.FindAsync(pending.Id))!.Status);
        var leave = new StaffLeaveRequest { StaffId = db.TeacherIds[0], FromDate = day, ToDate = day.AddDays(3), Reason = "Approved", Status = StaffLeaveStatus.Approved };
        db.Context.StaffLeaveRequests.Add(leave); await db.Context.SaveChangesAsync();
        LeaveTeacherCoverage Cover(DateTime date) => new() { StaffLeaveRequestId = leave.Id, AcademicYearId = db.YearId, SchoolClassId = db.ClassId, StaffId = db.TeacherIds[1], AssignedByStaffId = db.AdminStaffId, CoverDate = DateOnly.FromDateTime(date), SourceKind = "ClassTeacher", SourceId = 123, Duty = "Attendance" };
        var earlier = Cover(day); var later = Cover(day.AddDays(2)); db.Context.AddRange(earlier, later); await db.Context.SaveChangesAsync();
        var request = new StaffLeaveController.CancelLeaveRequest(day.AddDays(2), "Returning early");
        Assert.IsType<ForbidResult>(await Controller(db.TeacherUserIds[1]).RequestCancellation(leave.Id, request));
        Assert.IsType<OkObjectResult>(await owner.RequestCancellation(leave.Id, request));
        Assert.IsType<ConflictObjectResult>(await owner.RequestCancellation(leave.Id, request));
        var cancellationId = await db.Context.StaffLeaveCancellations.Select(c => c.Id).SingleAsync();
        Assert.Equal(StaffLeaveStatus.Approved, (await db.Context.StaffLeaveRequests.FindAsync(leave.Id))!.Status);
        Assert.False((await db.Context.LeaveTeacherCoverages.FindAsync(later.Id))!.IsRevoked);
        Assert.IsType<ForbidResult>(await owner.ReviewCancellation(cancellationId, new(true, null)));
        Assert.IsType<OkObjectResult>(await admin.ReviewCancellation(cancellationId, new(false, "Please discuss dates")));
        Assert.False((await db.Context.LeaveTeacherCoverages.FindAsync(later.Id))!.IsRevoked);
        Assert.IsType<OkObjectResult>(await owner.RequestCancellation(leave.Id, request));
        cancellationId = await db.Context.StaffLeaveCancellations.Where(c => c.Status == "Pending").Select(c => c.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await admin.ReviewCancellation(cancellationId, new(true, "Confirmed")));
        Assert.IsType<ConflictObjectResult>(await admin.ReviewCancellation(cancellationId, new(true, null)));
        leave = (await db.Context.StaffLeaveRequests.FindAsync(leave.Id))!;
        Assert.Equal(day.AddDays(1), leave.ToDate); Assert.Equal(StaffLeaveStatus.Approved, leave.Status);
        Assert.False((await db.Context.LeaveTeacherCoverages.FindAsync(earlier.Id))!.IsRevoked);
        Assert.True((await db.Context.LeaveTeacherCoverages.FindAsync(later.Id))!.IsRevoked);
        Assert.Equal(db.AdminStaffId, (await db.Context.LeaveTeacherCoverages.FindAsync(later.Id))!.RevokedByStaffId);
        Assert.True(await db.Context.Notifications.AnyAsync(n => n.RecipientStaffId == db.TeacherIds[1] && n.Title == "Leave cover cancelled"));
        var visible = Assert.IsType<OkObjectResult>(await owner.Cancellations());
        Assert.Equal(2, JsonSerializer.SerializeToElement(visible.Value).GetProperty("rows").GetArrayLength());
        var hidden = Assert.IsType<OkObjectResult>(await Controller(db.TeacherUserIds[1]).Cancellations());
        Assert.Equal(0, JsonSerializer.SerializeToElement(hidden.Value).GetProperty("rows").GetArrayLength());
        var oldRequest = new StaffLeaveCancellation { StaffLeaveRequestId = leave.Id, EffectiveFrom = day, OriginalToDate = day.AddDays(3), Reason = "Archived request", Status = "Rejected", RequestedAt = DateTime.UtcNow.AddMonths(-8) };
        db.Context.StaffLeaveCancellations.Add(oldRequest);
        for (var i = 0; i < 28; i++) db.Context.StaffLeaveCancellations.Add(new StaffLeaveCancellation { StaffLeaveRequestId = leave.Id, EffectiveFrom = day, OriginalToDate = day.AddDays(3), Reason = "Page check", Status = "Rejected", RequestedAt = DateTime.UtcNow.AddMinutes(-i - 1) });
        await db.Context.SaveChangesAsync();
        var recent = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await owner.Cancellations()).Value);
        Assert.Equal(30, recent.GetProperty("total").GetInt32());
        Assert.Equal(25, recent.GetProperty("rows").GetArrayLength());
        Assert.Equal(2, recent.GetProperty("totalPages").GetInt32());
        var secondPage = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await owner.Cancellations(page: 2)).Value);
        Assert.Equal(5, secondPage.GetProperty("rows").GetArrayLength());
        var firstIds = recent.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("Id").GetInt32()).ToArray();
        Assert.DoesNotContain(secondPage.GetProperty("rows").EnumerateArray(), r => firstIds.Contains(r.GetProperty("Id").GetInt32()));
        var archived = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await owner.Cancellations(from: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-9)), to: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-7)))).Value);
        Assert.Equal(1, archived.GetProperty("total").GetInt32());
        Assert.Equal(oldRequest.Id, archived.GetProperty("rows")[0].GetProperty("Id").GetInt32());
        Assert.Equal(31, await db.Context.StaffLeaveCancellations.CountAsync());
        Assert.IsType<BadRequestObjectResult>(await owner.Cancellations(from: DateOnly.FromDateTime(day), to: DateOnly.FromDateTime(day.AddDays(-1))));
        Assert.IsType<BadRequestObjectResult>(await owner.Cancellations(pageSize: 101));
        // A leader cannot approve cancellation of their own leave.
        var ownLeave = new StaffLeaveRequest { StaffId = db.AdminStaffId, FromDate = day, ToDate = day.AddDays(1), Reason = "Leader leave", Status = StaffLeaveStatus.Approved };
        db.Context.StaffLeaveRequests.Add(ownLeave); await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await admin.RequestCancellation(ownLeave.Id, new(day, "No longer needed")));
        var ownCancelId = await db.Context.StaffLeaveCancellations.Where(c => c.StaffLeaveRequestId == ownLeave.Id).Select(c => c.Id).SingleAsync();
        Assert.IsType<BadRequestObjectResult>(await admin.ReviewCancellation(ownCancelId, new(true, null)));
        var leaderUser = await db.CreateUserAsync("cancellation-reviewer", "Admin");
        db.Context.Staff.Add(new Staff { StaffNumber = "CANCEL-LEADER", FullName = "Second leader", ApplicationUserId = leaderUser.Id }); await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await Controller(leaderUser.Id).ReviewCancellation(ownCancelId, new(true, null)));
        Assert.Equal(StaffLeaveStatus.Cancelled, (await db.Context.StaffLeaveRequests.FindAsync(ownLeave.Id))!.Status);
    }

    [Fact]
    public async Task LeaveCoverChecksScopeConflictsNotificationsRevocationAndDateExpiry()
    {
        await using var db = await TestDatabase.CreateAsync();
        var monday = DateTime.Today.AddDays(1); while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);
        var date = DateOnly.FromDateTime(monday);
        var time = new AttendanceClock(new DateTimeOffset(DateTime.SpecifyKind(monday.AddHours(8), DateTimeKind.Unspecified), TimeSpan.Zero));
        var window = new AttendanceWindowService(db.Context, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Attendance:SchoolTimeZoneId"] = "UTC" }).Build(), time);
        var headUser = await db.CreateUserAsync("cover-head", "Section Head");
        var head = new Staff { StaffNumber = "COVER-HEAD", FullName = "Cover head", ApplicationUserId = headUser.Id };
        var term = new AcademicTerm { Name = "Cover term", AcademicYearId = db.YearId, StartDate = monday.AddDays(-1), EndDate = monday.AddMonths(2) };
        var otherClass = new SchoolClass { Name = "C", GradeId = (await db.Context.SchoolClasses.FindAsync(db.ClassId))!.GradeId };
        db.Context.AddRange(head, term, otherClass); await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId });
        var duty = new ClassTeacherAssignment { StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId, AcademicYearId = db.YearId, AssignedByStaffId = db.AdminStaffId };
        var original = new TimetableEntry { StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId, SubjectId = db.SubjectRequest().SubjectId, AcademicYearId = db.YearId, AcademicTermId = term.Id, Day = SchoolDay.Monday, StartTime = new(9, 0), EndTime = new(10, 0), CreatedByStaffId = db.AdminStaffId };
        var conflict = new TimetableEntry { StaffId = db.TeacherIds[1], SchoolClassId = otherClass.Id, SubjectId = db.SubjectRequest().SubjectId, AcademicYearId = db.YearId, AcademicTermId = term.Id, Day = SchoolDay.Monday, StartTime = new(9, 30), EndTime = new(10, 15), CreatedByStaffId = db.AdminStaffId };
        var leave = new StaffLeaveRequest { StaffId = db.TeacherIds[0], LeaveType = StaffLeaveType.Sick, FromDate = monday, ToDate = monday.AddDays(1), Reason = "Absent", Status = StaffLeaveStatus.Approved };
        db.Context.AddRange(duty, original, conflict, leave); await db.Context.SaveChangesAsync();
        LeaveCoverageController Controller(string id) => new(db.Context, db.Users, window, new LeavePushFailure(), Microsoft.Extensions.Logging.Abstractions.NullLogger<LeaveCoverageController>.Instance) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "Test")) } } };
        var manager = Controller(headUser.Id);
        Assert.IsType<ForbidResult>(await Controller(db.TeacherUserIds[1]).Options(date));
        var otherHeadUser = await db.CreateUserAsync("cover-other-head", "Section Head");
        var otherHead = new Staff { StaffNumber = "COVER-OTHER", FullName = "Other section head", ApplicationUserId = otherHeadUser.Id };
        var otherSection = new Section { Name = "Other coverage section" };
        db.Context.AddRange(otherHead, otherSection); await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = otherHead.Id, SectionId = otherSection.Id, AcademicYearId = db.YearId });
        await db.Context.SaveChangesAsync();
        var outside = Assert.IsType<OkObjectResult>(await Controller(otherHeadUser.Id).Options(date));
        Assert.Equal(0, JsonSerializer.SerializeToElement(outside.Value).GetProperty("leaves").GetArrayLength());
        var options = Assert.IsType<OkObjectResult>(await manager.Options(date));
        Assert.Equal(1, JsonSerializer.SerializeToElement(options.Value).GetProperty("leaves").GetArrayLength());
        var teaching = new LeaveCoverageController.AssignRequest(leave.Id, date, "Timetable", original.Id, db.TeacherIds[1]);
        Assert.IsType<ForbidResult>(await Controller(otherHeadUser.Id).Assign(teaching));
        Assert.IsType<ConflictObjectResult>(await manager.Assign(teaching));
        conflict = await db.Context.TimetableEntries.SingleAsync(t => t.Id == conflict.Id); conflict.IsActive = false; await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await manager.Assign(teaching));
        Assert.IsType<ConflictObjectResult>(await manager.Assign(teaching));
        Assert.Equal(1, await db.Context.Notifications.CountAsync(n => n.ReferenceType == "LeaveTeacherCoverage"));
        var replacementLeave = new StaffLeaveRequest { StaffId = db.TeacherIds[1], LeaveType = StaffLeaveType.Casual, FromDate = monday, ToDate = monday, Reason = "Unavailable", Status = StaffLeaveStatus.Approved };
        db.Context.StaffLeaveRequests.Add(replacementLeave); await db.Context.SaveChangesAsync();
        var attendance = new LeaveCoverageController.AssignRequest(leave.Id, date, "ClassTeacher", duty.Id, db.TeacherIds[1]);
        Assert.IsType<ConflictObjectResult>(await manager.Assign(attendance));
        replacementLeave = await db.Context.StaffLeaveRequests.SingleAsync(l => l.Id == replacementLeave.Id); replacementLeave.Status = StaffLeaveStatus.Rejected; await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await manager.Assign(attendance));
        Assert.True(await LeaveCoverageAccess.HasAttendance(db.Context, db.TeacherIds[1], db.YearId, db.ClassId, date));
        var attendanceController = new AttendanceController(db.Context, db.Users, window) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[1])], "Test")) } } };
        var classes = Assert.IsType<OkObjectResult>(await attendanceController.GetMyAttendanceClasses());
        Assert.Equal(1, JsonSerializer.SerializeToElement(classes.Value).GetProperty("classes").GetArrayLength());
        time.Now = time.Now.AddDays(1);
        classes = Assert.IsType<OkObjectResult>(await attendanceController.GetMyAttendanceClasses());
        Assert.Equal(0, JsonSerializer.SerializeToElement(classes.Value).GetProperty("classes").GetArrayLength());
        time.Now = time.Now.AddDays(-1);
        var coverageId = await db.Context.LeaveTeacherCoverages.Where(c => c.SourceKind == "ClassTeacher").Select(c => c.Id).SingleAsync();
        Assert.IsType<ForbidResult>(await Controller(db.TeacherUserIds[0]).Revoke(coverageId));
        Assert.IsType<ForbidResult>(await Controller(otherHeadUser.Id).Revoke(coverageId));
        Assert.IsType<OkObjectResult>(await manager.Revoke(coverageId));
        Assert.False(await LeaveCoverageAccess.HasAttendance(db.Context, db.TeacherIds[1], db.YearId, db.ClassId, date));
        Assert.IsType<ConflictObjectResult>(await manager.Revoke(coverageId));
        Assert.Equal(2, await db.Context.Notifications.CountAsync(n => n.ReferenceId == coverageId && n.ReferenceType == "LeaveTeacherCoverage"));
        Assert.IsType<OkObjectResult>(await manager.Assign(attendance with { Date = date.AddDays(1) }));
        Assert.False(await LeaveCoverageAccess.HasAttendance(db.Context, db.TeacherIds[1], db.YearId, db.ClassId, date));
        time.Now = time.Now.AddDays(2);
        classes = Assert.IsType<OkObjectResult>(await attendanceController.GetMyAttendanceClasses());
        Assert.Equal(0, JsonSerializer.SerializeToElement(classes.Value).GetProperty("classes").GetArrayLength());
        time.Now = time.Now.AddDays(-2);
        Assert.IsType<BadRequestObjectResult>(await manager.Assign(teaching with { Date = date.AddDays(-1) }));
        var special = new SpecialClassSession { StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId, AcademicYearId = db.YearId, AcademicTermId = term.Id, SubjectId = db.SubjectRequest().SubjectId, ClassDate = date, StartTime = new(9, 0), EndTime = new(10, 0), Status = SpecialClassStatus.Approved, CreatedByStaffId = db.AdminStaffId };
        db.Context.SpecialClassSessions.Add(special); await db.Context.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await manager.Assign(teaching with { SourceKind = "SpecialClass", SourceId = special.Id }));
        special = await db.Context.SpecialClassSessions.SingleAsync(s => s.Id == special.Id); special.StartTime = new(11, 0); special.EndTime = new(12, 0); await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await manager.Assign(teaching with { SourceKind = "SpecialClass", SourceId = special.Id }));
        var mine = Assert.IsType<OkObjectResult>(await Controller(db.TeacherUserIds[1]).My());
        Assert.Equal(3, JsonSerializer.SerializeToElement(mine.Value).GetProperty("assignments").GetArrayLength());
        Assert.False(await db.Context.Notifications.AnyAsync(n => n.ReferenceType == "LeaveTeacherCoverageSection"));
        var regularCoverId = await db.Context.LeaveTeacherCoverages.Where(c => c.SourceKind == "Timetable" && !c.IsRevoked).Select(c => c.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await manager.Revoke(regularCoverId));
        var adminUserId = (await db.Context.Staff.FindAsync(db.AdminStaffId))!.ApplicationUserId!;
        Assert.IsType<OkObjectResult>(await Controller(adminUserId).Assign(teaching));
        var assignedId = await db.Context.LeaveTeacherCoverages.Where(c => c.SourceKind == "Timetable" && !c.IsRevoked).Select(c => c.Id).SingleAsync();
        var alerts = await db.Context.Notifications.Where(n => n.ReferenceId == assignedId && (n.ReferenceType == "LeaveTeacherCoverage" || n.ReferenceType == "LeaveTeacherCoverageSection")).ToListAsync();
        Assert.Equal(2, alerts.Count);
        Assert.Contains(alerts, n => n.RecipientStaffId == db.TeacherIds[1]);
        Assert.Contains(alerts, n => n.RecipientStaffId == head.Id && n.ReferenceType == "LeaveTeacherCoverageSection");
        Assert.DoesNotContain(alerts, n => n.RecipientStaffId == otherHead.Id);
        var update = teaching with { ReplacesCoverageId = assignedId, StaffId = db.TeacherIds[0] };
        Assert.IsType<BadRequestObjectResult>(await Controller(adminUserId).Assign(update));
        Assert.False((await db.Context.LeaveTeacherCoverages.FindAsync(assignedId))!.IsRevoked);
        var newUser = await db.CreateUserAsync("new-cover-teacher", "Teacher");
        var newTeacher = new Staff { StaffNumber = "NEW-COVER", FullName = "New replacement", ApplicationUserId = newUser.Id };
        db.Context.Staff.Add(newTeacher); await db.Context.SaveChangesAsync();
        update = update with { StaffId = newTeacher.Id };
        Assert.IsType<ForbidResult>(await Controller(otherHeadUser.Id).Assign(update));
        Assert.IsType<OkObjectResult>(await Controller(adminUserId).Assign(update));
        Assert.IsType<ConflictObjectResult>(await Controller(adminUserId).Assign(update));
        var updated = await db.Context.LeaveTeacherCoverages.SingleAsync(c => c.ReplacesCoverageId == assignedId);
        var old = await db.Context.LeaveTeacherCoverages.FindAsync(assignedId);
        Assert.True(old!.IsRevoked); Assert.Equal(db.AdminStaffId, old.RevokedByStaffId);
        Assert.Contains(await db.Context.Notifications.Where(n => n.ReferenceId == updated.Id).ToListAsync(), n => n.RecipientStaffId == head.Id && n.ReferenceType == "LeaveTeacherCoverageSection");
        Assert.True(await db.Context.Notifications.AnyAsync(n => n.ReferenceId == assignedId && n.RecipientStaffId == db.TeacherIds[1] && n.Title == "Leave cover duty reassigned"));
        Assert.IsType<ForbidResult>(await Controller(newUser.Id).History());
        var hiddenHistory = Assert.IsType<OkObjectResult>(await Controller(otherHeadUser.Id).History());
        Assert.Equal(0, JsonSerializer.SerializeToElement(hiddenHistory.Value).GetProperty("total").GetInt32());
        var history = Assert.IsType<OkObjectResult>(await manager.History(search: "NEW-COVER", from: date, to: date));
        Assert.Equal(1, JsonSerializer.SerializeToElement(history.Value).GetProperty("total").GetInt32());
        Assert.IsType<OkObjectResult>(await Controller(adminUserId).Revoke(updated.Id));
        var removed = await db.Context.LeaveTeacherCoverages.FindAsync(updated.Id);
        Assert.Equal(db.AdminStaffId, removed!.RevokedByStaffId);
        Assert.True(await db.Context.Notifications.AnyAsync(n => n.ReferenceId == updated.Id && n.RecipientStaffId == head.Id && n.Title == "Section leave cover removed"));
        var removalHistory = Assert.IsType<OkObjectResult>(await manager.History(search: "NEW-COVER"));
        Assert.Equal("Removed", JsonSerializer.SerializeToElement(removalHistory.Value).GetProperty("rows")[0].GetProperty("status").GetString());
    }

    private sealed class LeavePushFailure : SchoolManagement.Application.Notifications.IPushNotificationService
    {
        public Task SendToStaffAsync(int staffId, string title, string message, string? referenceType = null, int? referenceId = null) => throw new InvalidOperationException("Simulated push outage");
        public Task SendToStudentAsync(int studentId, string title, string message, string? referenceType = null, int? referenceId = null) => Task.CompletedTask;
        public Task SendToParentAsync(int parentGuardianId, string title, string message, string? referenceType = null, int? referenceId = null) => Task.CompletedTask;
    }

    [Fact]
    public async Task AllStaffCanRequestLeaveAndReviewNotificationsReachOnlyTheCorrectStaff()
    {
        await using var db = await TestDatabase.CreateAsync();
        var roleManager = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Principal", "Deputy Principal" }) Assert.True((await roleManager.CreateAsync(new IdentityRole(role))).Succeeded);
        async Task<Staff> StaffAccount(string name, string role) {
            var account = await db.CreateUserAsync(name, role);
            var person = new Staff { StaffNumber = name, FullName = name, ApplicationUserId = account.Id };
            db.Context.Staff.Add(person); await db.Context.SaveChangesAsync(); return person;
        }
        var principal = await StaffAccount("leave-principal", "Principal");
        var deputy = await StaffAccount("leave-deputy", "Deputy Principal");
        var head = await StaffAccount("leave-head", "Section Head");
        var unrelated = await StaffAccount("leave-unrelated", "Section Head");
        var otherSection = new Section { Name = "Other leave section" };
        db.Context.Sections.Add(otherSection); await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.AddRange(new SectionHeadAssignment { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId }, new SectionHeadAssignment { StaffId = unrelated.Id, SectionId = otherSection.Id, AcademicYearId = db.YearId });
        db.Context.TeacherAssignments.Add(new TeacherAssignment { StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId, AcademicYearId = db.YearId, SubjectId = db.SubjectRequest().SubjectId });
        await db.Context.SaveChangesAsync();
        StaffLeaveController Controller(string userId) => new(db.Context, db.Users, new LeavePushFailure()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) } } };
        var day = DateTime.Today.AddDays(10);
        SchoolManagement.Application.StaffLeave.DTOs.CreateStaffLeaveRequest Request(DateTime date) => new() { LeaveType = StaffLeaveType.Casual, FromDate = date, ToDate = date.AddDays(1), Reason = "Personal leave" };
        var teacher = Controller(db.TeacherUserIds[0]);
        Assert.IsType<OkObjectResult>(await teacher.CreateLeaveRequest(Request(day)));
        var leaveId = await db.Context.StaffLeaveRequests.Where(l => l.StaffId == db.TeacherIds[0]).Select(l => l.Id).SingleAsync();
        Assert.Equal(3, await db.Context.Notifications.CountAsync(n => n.Type == NotificationType.LeaveRequested && n.ReferenceId == leaveId));
        Assert.IsType<BadRequestObjectResult>(await teacher.CreateLeaveRequest(Request(day)));
        var headPending = Assert.IsType<OkObjectResult>(await Controller(head.ApplicationUserId!).GetPendingLeaveRequests());
        Assert.Equal(1, JsonSerializer.SerializeToElement(headPending.Value).GetProperty("count").GetInt32());
        var otherPending = Assert.IsType<OkObjectResult>(await Controller(unrelated.ApplicationUserId!).GetPendingLeaveRequests());
        Assert.Equal(0, JsonSerializer.SerializeToElement(otherPending.Value).GetProperty("count").GetInt32());
        var denied = Assert.IsType<ObjectResult>(await Controller(head.ApplicationUserId!).ReviewLeaveRequest(leaveId, new() { Approve = true }));
        Assert.Equal(403, denied.StatusCode);
        Assert.IsType<OkObjectResult>(await Controller(db.AdminUserId).ReviewLeaveRequest(leaveId, new() { Approve = true, Remarks = new string('A', 1000) }));
        var approved = await db.Context.Notifications.Where(n => n.Type == NotificationType.LeaveApproved && n.ReferenceId == leaveId).ToListAsync();
        Assert.Equal(5, approved.Count);
        Assert.Equal(5, approved.Select(n => n.RecipientStaffId).Distinct().Count());
        Assert.Contains(approved, n => n.RecipientStaffId == db.TeacherIds[0]);
        Assert.Contains(approved, n => n.RecipientStaffId == head.Id);
        Assert.DoesNotContain(approved, n => n.RecipientStaffId == unrelated.Id);
        Assert.All(approved, n => Assert.True(n.Message.Length <= 1000));
        Assert.IsType<ConflictObjectResult>(await Controller(principal.ApplicationUserId!).ReviewLeaveRequest(leaveId, new() { Approve = false }));
        Assert.Equal(5, await db.Context.Notifications.CountAsync(n => n.Type == NotificationType.LeaveApproved && n.ReferenceId == leaveId));
        Assert.IsType<OkObjectResult>(await teacher.CreateLeaveRequest(Request(day.AddDays(5))));
        var rejectionId = await db.Context.StaffLeaveRequests.Where(l => l.StaffId == db.TeacherIds[0] && l.Status == StaffLeaveStatus.Pending).Select(l => l.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await Controller(principal.ApplicationUserId!).ReviewLeaveRequest(rejectionId, new() { Approve = false, Remarks = "Please choose another date" }));
        var rejected = Assert.Single(await db.Context.Notifications.Where(n => n.Type == NotificationType.LeaveRejected && n.ReferenceId == rejectionId).ToListAsync());
        Assert.Equal(db.TeacherIds[0], rejected.RecipientStaffId);
        Assert.IsType<ForbidResult>(await teacher.GetLeaveHistory());
        var history = Assert.IsType<OkObjectResult>(await Controller(db.AdminUserId).GetLeaveHistory());
        var historyJson = JsonSerializer.SerializeToElement(history.Value);
        Assert.Equal(2, historyJson.GetProperty("total").GetInt32());
        Assert.Equal(1, historyJson.GetProperty("summary").GetProperty("approved").GetInt32());
        Assert.Equal(1, historyJson.GetProperty("summary").GetProperty("rejected").GetInt32());
        // History remains available when a teacher's assignment is later deactivated.
        var historicalAssignment = await db.Context.TeacherAssignments.SingleAsync(a => a.StaffId == db.TeacherIds[0]);
        historicalAssignment.IsActive = false; await db.Context.SaveChangesAsync();
        var scopedHistory = Assert.IsType<OkObjectResult>(await Controller(head.ApplicationUserId!).GetLeaveHistory());
        Assert.Equal(2, JsonSerializer.SerializeToElement(scopedHistory.Value).GetProperty("total").GetInt32());
        var hiddenHistory = Assert.IsType<OkObjectResult>(await Controller(unrelated.ApplicationUserId!).GetLeaveHistory());
        Assert.Equal(0, JsonSerializer.SerializeToElement(hiddenHistory.Value).GetProperty("total").GetInt32());
        var filteredHistory = Assert.IsType<OkObjectResult>(await Controller(principal.ApplicationUserId!).GetLeaveHistory(status: StaffLeaveStatus.Approved, fromDate: day.AddDays(1), toDate: day.AddDays(1)));
        var filteredJson = JsonSerializer.SerializeToElement(filteredHistory.Value);
        Assert.Equal(1, filteredJson.GetProperty("total").GetInt32());
        Assert.Equal(2, filteredJson.GetProperty("requests")[0].GetProperty("calendarDays").GetInt32());
        Assert.Equal("Admin", filteredJson.GetProperty("requests")[0].GetProperty("reviewedBy").GetString());
        var historyPage = Assert.IsType<OkObjectResult>(await Controller(db.AdminUserId).GetLeaveHistory(search: "T0", page: 2, pageSize: 1));
        var pageJson = JsonSerializer.SerializeToElement(historyPage.Value);
        Assert.Equal(2, pageJson.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, pageJson.GetProperty("requests").GetArrayLength());
        Assert.IsType<BadRequestObjectResult>(await Controller(db.AdminUserId).GetLeaveHistory(fromDate: day.AddDays(2), toDate: day));
        foreach (var userId in new[] { db.AdminUserId, principal.ApplicationUserId!, deputy.ApplicationUserId!, head.ApplicationUserId! })
        {
            Assert.IsType<OkObjectResult>(await Controller(userId).CreateLeaveRequest(Request(day.AddDays(20))));
            var mine = Assert.IsType<OkObjectResult>(await Controller(userId).GetMyLeaveRequests());
            Assert.Equal(1, JsonSerializer.SerializeToElement(mine.Value).GetProperty("count").GetInt32());
        }
        var adminLeave = await db.Context.StaffLeaveRequests.Where(l => l.StaffId == db.AdminStaffId).Select(l => l.Id).SingleAsync();
        Assert.IsType<BadRequestObjectResult>(await Controller(db.AdminUserId).ReviewLeaveRequest(adminLeave, new() { Approve = true }));
        Assert.IsType<OkObjectResult>(await Controller(principal.ApplicationUserId!).ReviewLeaveRequest(adminLeave, new() { Approve = true }));
        Assert.True(await db.Context.Notifications.AnyAsync(n => n.ReferenceId == adminLeave && n.Type == NotificationType.LeaveApproved && n.RecipientStaffId == db.AdminStaffId));
        Assert.True(await db.Context.Notifications.AnyAsync(n => n.ReferenceId == adminLeave && n.Type == NotificationType.LeaveApproved && n.RecipientStaffId == unrelated.Id));
    }

    [Fact]
    public async Task PromotionMistakesCanBeUndoneWithoutLosingHistoryOrAffectingOtherStudents()
    {
        await using var db = await TestDatabase.CreateAsync();
        var alternate = new SchoolClass { Name = "B", GradeId = (await db.Context.SchoolClasses.FindAsync(db.ClassId))!.GradeId };
        var nextYear = new AcademicYear { Name = "Future repeat year", IsActive = true, StartDate = AnnualPromotionService.SchoolToday.AddYears(1).AddDays(2), EndDate = AnnualPromotionService.SchoolToday.AddYears(2) };
        var students = new[] { new Student { IndexNumber = "UNDO-01", FullName = "Wrong student", SchoolClassId = db.ClassId }, new Student { IndexNumber = "UNDO-02", FullName = "Other student", SchoolClassId = db.ClassId } };
        db.Context.AddRange(alternate, nextYear); db.Context.Students.AddRange(students); await db.Context.SaveChangesAsync();
        db.Context.StudentAcademicEnrollments.AddRange(students.Select(s => new StudentAcademicEnrollment { StudentId = s.Id, AcademicYearId = db.YearId, SchoolClassId = db.ClassId, CreatedByStaffId = db.AdminStaffId }));
        await db.Context.SaveChangesAsync();
        var controller = new AnnualPromotionController(db.Context, new AnnualPromotionService(db.Context)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, db.AdminUserId), new Claim(ClaimTypes.Role, "Admin")], "Test")) } } };
        Assert.IsType<BadRequestObjectResult>(await controller.SetExceptions(new([students[0].Id], db.YearId, db.ClassId, "No change", "CorrectPlacement")));
        Assert.Equal(0, await db.Context.StudentPromotionHistories.CountAsync());
        Assert.IsType<BadRequestObjectResult>(await controller.SetExceptions(new([students[0].Id], db.YearId, db.ClassId, "Wrong year", "RepeatNextYear")));
        Assert.IsType<OkObjectResult>(await controller.SetExceptions(new([students[0].Id], db.YearId, alternate.Id, "Mistaken selection", "CorrectPlacement")));
        var correction = await db.Context.AnnualPromotionChanges.SingleAsync();
        Assert.Equal(alternate.Id, (await db.Context.Students.FindAsync(students[0].Id))!.SchoolClassId);
        Assert.IsType<OkObjectResult>(await controller.Undo(correction.Id));
        Assert.Equal(db.ClassId, (await db.Context.Students.FindAsync(students[0].Id))!.SchoolClassId);
        Assert.Equal(db.ClassId, (await db.Context.Students.FindAsync(students[1].Id))!.SchoolClassId);
        Assert.Equal(2, await db.Context.StudentPromotionHistories.CountAsync());
        Assert.True(await db.Context.StudentPromotionHistories.AnyAsync(h => h.Action == "Undo"));
        Assert.Equal(0, await db.Context.AnnualPromotionExceptions.CountAsync());
        Assert.IsType<ConflictObjectResult>(await controller.Undo(correction.Id));
        Assert.IsType<OkObjectResult>(await controller.SetExceptions(new([students[1].Id], nextYear.Id, db.ClassId, "Future mistake", "RepeatNextYear")));
        var pendingId = await db.Context.AnnualPromotionChanges.Where(c => c.StudentId == students[1].Id).Select(c => c.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await controller.Undo(pendingId));
        Assert.Equal(0, await db.Context.AnnualPromotionExceptions.CountAsync());
        // The older screenshot's same-class Repeat can be removed without inventing a placement.
        db.Context.AnnualPromotionExceptions.Add(new AnnualPromotionException { StudentId = students[0].Id, AcademicYearId = nextYear.Id, SchoolClassId = db.ClassId, Reason = "Old pending", ProcessedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        var legacyId = await db.Context.AnnualPromotionExceptions.Select(e => e.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await controller.UndoLegacyException(legacyId));
        var legacyStudent = new Student { IndexNumber = "UNDO-LEGACY", FullName = "Older same-class repeat", SchoolClassId = db.ClassId };
        db.Context.Students.Add(legacyStudent); await db.Context.SaveChangesAsync();
        db.Context.StudentAcademicEnrollments.Add(new StudentAcademicEnrollment { StudentId = legacyStudent.Id, AcademicYearId = db.YearId, SchoolClassId = db.ClassId, CreatedByStaffId = db.AdminStaffId });
        var legacyException = new AnnualPromotionException { StudentId = legacyStudent.Id, AcademicYearId = db.YearId, SchoolClassId = db.ClassId, Reason = "test", ProcessedByStaffId = db.AdminStaffId };
        db.Context.AnnualPromotionExceptions.Add(legacyException);
        db.Context.StudentPromotionHistories.Add(new StudentPromotionHistory { StudentId = legacyStudent.Id, FromAcademicYearId = db.YearId, ToAcademicYearId = db.YearId, FromSchoolClassId = db.ClassId, ToSchoolClassId = db.ClassId, Action = "Repeat", Reason = "test", ProcessedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await controller.UndoLegacyException(legacyException.Id));
        Assert.Equal(db.ClassId, (await db.Context.Students.FindAsync(legacyStudent.Id))!.SchoolClassId);
        Assert.Equal(2, await db.Context.StudentPromotionHistories.CountAsync(h => h.StudentId == legacyStudent.Id));
        // A later change must protect the student against an older Undo request.
        Assert.IsType<OkObjectResult>(await controller.SetExceptions(new([students[0].Id], db.YearId, alternate.Id, "First correction", "CorrectPlacement")));
        var olderId = await db.Context.AnnualPromotionChanges.MaxAsync(c => c.Id);
        Assert.IsType<OkObjectResult>(await controller.SetExceptions(new([students[0].Id], db.YearId, db.ClassId, "Later correction", "CorrectPlacement")));
        Assert.IsType<ConflictObjectResult>(await controller.Undo(olderId));
        Assert.IsType<OkObjectResult>(await controller.SetExceptions(new([students[1].Id], db.YearId, alternate.Id, "Prepare unchanged student", "CorrectPlacement")));
        var count = await db.Context.StudentPromotionHistories.CountAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.SetExceptions(new([students[0].Id, students[1].Id], db.YearId, alternate.Id, "Reject unchanged batch", "CorrectPlacement")));
        db.Context.ChangeTracker.Clear();
        Assert.Equal(count, await db.Context.StudentPromotionHistories.CountAsync());
        Assert.Equal(db.ClassId, (await db.Context.Students.FindAsync(students[0].Id))!.SchoolClassId);
    }

    [Fact]
    public async Task AutomaticPromotionHonorsDatesRepeatsGraduationCorrectionsAndReruns()
    {
        await using var db = await TestDatabase.CreateAsync();
        var today = AnnualPromotionService.SchoolToday;
        var sourceYear = await db.Context.AcademicYears.FindAsync(db.YearId);
        sourceYear!.StartDate = today.AddYears(-1); sourceYear.EndDate = today.AddDays(-1);
        var nextYear = new AcademicYear { Name = "Next academic year", StartDate = today.AddDays(1), EndDate = today.AddYears(1), IsActive = true };
        var finalClass = new SchoolClass { Name = "A", Grade = new Grade { Name = "12", SectionId = db.SectionId } };
        var unmapped = new SchoolClass { Name = "B", GradeId = (await db.Context.SchoolClasses.FindAsync(db.ClassId))!.GradeId };
        db.Context.AddRange(nextYear, finalClass, unmapped); await db.Context.SaveChangesAsync();
        var students = new[] {
            new Student { IndexNumber = "AUTO-01", FullName = "Promote", SchoolClassId = db.ClassId },
            new Student { IndexNumber = "AUTO-02", FullName = "Repeat", SchoolClassId = db.ClassId },
            new Student { IndexNumber = "AUTO-03", FullName = "Graduate", SchoolClassId = finalClass.Id },
            new Student { IndexNumber = "AUTO-04", FullName = "Missing mapping", SchoolClassId = unmapped.Id }
        };
        db.Context.Students.AddRange(students); await db.Context.SaveChangesAsync();
        db.Context.StudentAcademicEnrollments.AddRange(students.Select(s => new StudentAcademicEnrollment {
            StudentId = s.Id, SchoolClassId = s.SchoolClassId, AcademicYearId = sourceYear.Id, CreatedByStaffId = db.AdminStaffId, EnrollmentDate = DateOnly.FromDateTime(sourceYear.StartDate)
        }));
        db.Context.StudentAttendances.Add(new StudentAttendance { StudentId = students[0].Id, SchoolClassId = db.ClassId, AcademicYearId = sourceYear.Id, AttendanceDate = today.AddDays(-2), Status = AttendanceStatus.Present, MarkedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        var service = new AnnualPromotionService(db.Context);
        var controller = new AnnualPromotionController(db.Context, service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, db.AdminUserId), new Claim(ClaimTypes.Role, "Admin")], "Test"))
        } } };
        Assert.IsType<BadRequestObjectResult>(await controller.SetMapping(new(db.ClassId, db.ClassId, false, true)));
        Assert.IsType<OkObjectResult>(await controller.SetMapping(new(db.ClassId, finalClass.Id, false, true)));
        Assert.IsType<OkObjectResult>(await controller.SetMapping(new(finalClass.Id, null, true, true)));
        Assert.IsType<OkObjectResult>(await controller.SetExceptions(new([students[1].Id], nextYear.Id, db.ClassId, "Repeat grade for another year")));
        await service.RunAsync(today);
        Assert.Equal(0, await db.Context.StudentPromotionHistories.CountAsync());
        nextYear = await db.Context.AcademicYears.SingleAsync(y => y.Id == nextYear.Id);
        nextYear.StartDate = today; await db.Context.SaveChangesAsync();
        var result = JsonSerializer.Serialize(await service.RunAsync(today));
        Assert.Contains("No promotion mapping", result);
        Assert.Equal(3, await db.Context.StudentPromotionHistories.CountAsync());
        Assert.Equal(finalClass.Id, (await db.Context.StudentAcademicEnrollments.SingleAsync(e => e.StudentId == students[0].Id && e.AcademicYearId == nextYear.Id)).SchoolClassId);
        Assert.Equal(db.ClassId, (await db.Context.StudentAcademicEnrollments.SingleAsync(e => e.StudentId == students[1].Id && e.AcademicYearId == nextYear.Id)).SchoolClassId);
        Assert.True((await db.Context.Students.FindAsync(students[2].Id))!.IsGraduated);
        Assert.Equal(4, await db.Context.StudentAcademicEnrollments.CountAsync(e => e.AcademicYearId == sourceYear.Id));
        Assert.Equal(1, await db.Context.StudentAttendances.CountAsync());
        await service.RunAsync(today);
        Assert.Equal(3, await db.Context.StudentPromotionHistories.CountAsync());
        // Two independent checks share a transaction-owned SQL lock and cannot duplicate promotion.
        await using (var otherContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(db.Context.Database.GetConnectionString()).Options))
            await Task.WhenAll(service.RunAsync(today), new AnnualPromotionService(otherContext).RunAsync(today));
        Assert.Equal(3, await db.Context.StudentPromotionHistories.CountAsync());
        Assert.IsType<OkObjectResult>(await controller.SetExceptions(new([students[0].Id], nextYear.Id, db.ClassId, "Move back after review")));
        Assert.Equal(db.ClassId, (await db.Context.StudentAcademicEnrollments.SingleAsync(e => e.StudentId == students[0].Id && e.AcademicYearId == nextYear.Id)).SchoolClassId);
        Assert.IsType<OkObjectResult>(await controller.SetExceptions(new([students[2].Id], nextYear.Id, finalClass.Id, "Repeat final grade")));
        Assert.False((await db.Context.Students.FindAsync(students[2].Id))!.IsGraduated);
        Assert.Equal(1, await db.Context.StudentAcademicEnrollments.CountAsync(e => e.StudentId == students[2].Id && e.IsCurrent));
        var count = await db.Context.StudentPromotionHistories.CountAsync();
        await service.RunAsync(today); Assert.Equal(count, await db.Context.StudentPromotionHistories.CountAsync());
        Assert.IsType<OkObjectResult>(await controller.Get());
        var exceptionId = await db.Context.AnnualPromotionExceptions.Where(e => e.StudentId == students[0].Id).Select(e => e.Id).SingleAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.RemoveException(exceptionId));
        var repeatChangeId = await db.Context.AnnualPromotionChanges.Where(c => c.StudentId == students[1].Id).Select(c => c.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await controller.Undo(repeatChangeId));
        Assert.Equal(finalClass.Id, (await db.Context.StudentAcademicEnrollments.SingleAsync(e => e.StudentId == students[1].Id && e.AcademicYearId == nextYear.Id)).SchoolClassId);
        await service.RunAsync(today);
        Assert.Equal(finalClass.Id, (await db.Context.Students.FindAsync(students[1].Id))!.SchoolClassId);
        var completionChangeId = await db.Context.AnnualPromotionChanges.Where(c => c.StudentId == students[2].Id).Select(c => c.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await controller.Undo(completionChangeId));
        Assert.True((await db.Context.Students.FindAsync(students[2].Id))!.IsGraduated);
    }

    private sealed class ReportEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ReportsTest";
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = "";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Fact]
    public async Task PeopleReportsIncludeAssignmentsAndParentsAndRejectSectionHeads()
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = db.TeacherIds[1], SectionId = db.SectionId, AcademicYearId = db.YearId });
        db.Context.TeacherAssignments.Add(new TeacherAssignment { StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId, AcademicYearId = db.YearId, SubjectId = db.SubjectRequest().SubjectId });
        db.Context.ParentGuardians.Add(new ParentGuardian { ParentNumber = "P-REPORT", FullName = "Report parent" });
        await db.Context.SaveChangesAsync();
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "Test")) };
        var controller = new ReportsController(db.Context, new ReportEnvironment()) { ControllerContext = new ControllerContext { HttpContext = http } };
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        foreach (var kind in new[] { "sectionHeads", "teachers", "parents" })
        {
            http.Request.Path = "/api/reports/people/" + kind;
            var result = Assert.IsType<OkObjectResult>(await controller.GetPeopleReport(kind, db.YearId));
            Assert.Contains("records", JsonSerializer.Serialize(result.Value));
            if (kind == "parents") Assert.Contains("P-REPORT", JsonSerializer.Serialize(result.Value));
            http.Request.Path += "/pdf";
            var pdf = Assert.IsType<FileContentResult>(await controller.GetPeopleReport(kind, db.YearId));
            Assert.True(pdf.FileContents.Length > 1000);
            if (Environment.GetEnvironmentVariable("SCHOOL_REPORT_QA_OUTPUT") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllBytesAsync(Path.Combine(output, kind + "-qa.pdf"), pdf.FileContents);
            }
        }
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Section Head")], "Test"));
        Assert.IsType<ForbidResult>(await controller.GetPeopleReport("parents", db.YearId));
    }

    [Fact]
    public async Task ReportsIncludeLateAttendanceAndExportStudentPdf()
    {
        await using var db = await TestDatabase.CreateAsync();
        var student = new Student { IndexNumber = "REPORT-01", FullName = "Report verification student", SchoolClassId = db.ClassId, IsActive = true };
        db.Context.Students.Add(student); await db.Context.SaveChangesAsync();
        db.Context.StudentAcademicEnrollments.Add(new StudentAcademicEnrollment { StudentId = student.Id, AcademicYearId = db.YearId,
            SchoolClassId = db.ClassId, CreatedByStaffId = db.AdminStaffId, EnrollmentDate = DateOnly.FromDateTime(DateTime.Today), IsActive = true, IsCurrent = true });
        db.Context.StudentAttendances.Add(new StudentAttendance { StudentId = student.Id, SchoolClassId = db.ClassId, AcademicYearId = db.YearId,
            AttendanceDate = DateTime.Today, Status = AttendanceStatus.Late, MarkedByStaffId = db.TeacherIds[0] });
        var assignment = new TeacherAssignment { StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId, AcademicYearId = db.YearId, SubjectId = db.SubjectRequest().SubjectId };
        var exam = new Exam { Name = "First Term", MaximumMarks = 100, ExamDate = DateTime.Today,
            AcademicTerm = new AcademicTerm { Name = "1st Term", AcademicYearId = db.YearId, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(3) } };
        db.Context.StudentMarks.Add(new StudentMark { StudentId = student.Id, TeacherAssignment = assignment, Exam = exam, MarksObtained = 85, IsPublished = true, IsSubmitted = true });
        db.Context.StudentMarks.Add(new StudentMark { StudentId = student.Id, TeacherAssignment = assignment,
            Exam = new Exam { Name = "Unpublished exam", MaximumMarks = 100, AcademicTerm = exam.AcademicTerm }, MarksObtained = 50, IsPublished = false });
        await db.Context.SaveChangesAsync();
        var controller = new ReportsController(db.Context, new ReportEnvironment());
        var response = Assert.IsType<OkObjectResult>(await controller.GetStudentAcademicProfile(student.Id));
        var report = Assert.IsType<SchoolManagement.Application.Reports.DTOs.StudentAcademicProfileReportDto>(response.Value);
        Assert.Equal(100m, report.Attendance.AttendancePercentage);
        Assert.Equal(0, report.Attendance.PresentDays);
        Assert.Equal(1, report.Attendance.LateDays);
        Assert.Single(report.PublishedResults);
        Assert.Equal(85m, report.PublishedResults[0].MarksObtained);
        var classResult = Assert.IsType<OkObjectResult>(await controller.GetClassResultReport(db.ClassId, db.YearId, exam.AcademicTermId, exam.Id));
        Assert.Contains("85", JsonSerializer.Serialize(classResult.Value));
        var classAttendance = Assert.IsType<OkObjectResult>(await controller.GetClassAttendanceReport(db.ClassId, db.YearId, null, null));
        Assert.Contains("100", JsonSerializer.Serialize(classAttendance.Value));
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var pdf = Assert.IsType<FileContentResult>(await controller.GetStudentAcademicProfilePdf(student.Id));
        var classPdf = Assert.IsType<FileContentResult>(await controller.GetClassStudentReportPdf(db.ClassId, db.YearId));
        var attendancePdf = Assert.IsType<FileContentResult>(await controller.GetClassAttendanceReportPdf(db.ClassId, db.YearId, null, null));
        var resultsPdf = Assert.IsType<FileContentResult>(await controller.GetClassResultReportPdf(db.ClassId, db.YearId, exam.AcademicTermId, exam.Id));
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.True(pdf.FileContents.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.FileContents, 0, 4));
        if (Environment.GetEnvironmentVariable("SCHOOL_REPORT_QA_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllBytesAsync(Path.Combine(output, "student-report-qa.pdf"), pdf.FileContents);
            await File.WriteAllBytesAsync(Path.Combine(output, "class-students-qa.pdf"), classPdf.FileContents);
            await File.WriteAllBytesAsync(Path.Combine(output, "class-attendance-qa.pdf"), attendancePdf.FileContents);
            await File.WriteAllBytesAsync(Path.Combine(output, "class-results-qa.pdf"), resultsPdf.FileContents);
        }
        var action = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor();
        var actionContext = new ActionContext(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[1]), new Claim(ClaimTypes.Role, "Section Head")], "Test")) },
            new Microsoft.AspNetCore.Routing.RouteData(), action);
        var executing = new Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext(actionContext, [],
            new Dictionary<string, object?> { ["studentId"] = student.Id }, controller);
        controller.ControllerContext = new ControllerContext(actionContext);
        var called = false;
        await controller.OnActionExecutionAsync(executing, () => { called = true; return Task.FromResult(new Microsoft.AspNetCore.Mvc.Filters.ActionExecutedContext(actionContext, [], controller)); });
        Assert.False(called);
        Assert.IsType<ForbidResult>(executing.Result);
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = db.TeacherIds[1], SectionId = db.SectionId, AcademicYearId = db.YearId, IsActive = true });
        await db.Context.SaveChangesAsync();
        executing.Result = null;
        await controller.OnActionExecutionAsync(executing, () => { called = true; return Task.FromResult(new Microsoft.AspNetCore.Mvc.Filters.ActionExecutedContext(actionContext, [], controller)); });
        Assert.True(called);
    }

    [Fact]
    public async Task StaffNotificationsArePersonalAndReadActionsCannotAffectOthers()
    {
        await using var db = await TestDatabase.CreateAsync();
        var own = new Notification { RecipientStaffId = db.TeacherIds[0], Title = "Special class approved", Message = "Approved", Type = NotificationType.SpecialClassApproved };
        var other = new Notification { RecipientStaffId = db.AdminStaffId, Title = "Admin alert", Message = "Private", Type = NotificationType.General };
        db.Context.Notifications.AddRange(own, other); await db.Context.SaveChangesAsync();
        var controller = new NotificationController(db.Context)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[0])], "Test")) } } };
        var list = Assert.IsType<OkObjectResult>(await controller.GetMyNotifications(null));
        Assert.Equal(1, JsonSerializer.SerializeToElement(list.Value).GetProperty("count").GetInt32());
        Assert.IsType<NotFoundObjectResult>(await controller.MarkNotificationRead(other.Id, new() { IsRead = true }));
        Assert.IsType<OkObjectResult>(await controller.MarkNotificationRead(own.Id, new() { IsRead = true }));
        var unread = Assert.IsType<OkObjectResult>(await controller.GetUnreadCount());
        Assert.Equal(0, JsonSerializer.SerializeToElement(unread.Value).GetProperty("unreadCount").GetInt32());
        Assert.IsType<OkObjectResult>(await controller.MarkNotificationRead(own.Id, new() { IsRead = false }));
        Assert.IsType<OkObjectResult>(await controller.MarkAllAsRead());
        Assert.True(own.IsRead);
        Assert.False(other.IsRead);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Principal")]
    [InlineData("Section Head")]
    [InlineData("Teacher")]
    public async Task SpecialClassNotifiesLeadershipSectionTeacherAndFamilies(string creatorRole)
    {
        await using var db = await TestDatabase.CreateAsync();
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Principal"))).Succeeded);
        var principalUser = await db.CreateUserAsync("special-principal", "Principal");
        var principal = new Staff { StaffNumber = "PRINC", FullName = "Principal", ApplicationUserId = principalUser.Id };
        var headUser = await db.CreateUserAsync("special-head-notify", "Section Head");
        var head = new Staff { StaffNumber = "HEAD-N", FullName = "Head", ApplicationUserId = headUser.Id };
        var child = new Student { IndexNumber = "SPECIAL-N", FullName = "Child", SchoolClassId = db.ClassId, IsActive = true };
        var parent = new ParentGuardian { ParentNumber = "SPECIAL-P", FullName = "Parent", IsActive = true };
        db.Context.AddRange(principal, head, child, parent);
        db.Context.TeacherAssignments.Add(new TeacherAssignment { StaffId = db.TeacherIds[0], SubjectId = db.SubjectRequest().SubjectId, SchoolClassId = db.ClassId, AcademicYearId = db.YearId });
        await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId });
        db.Context.StudentParentGuardians.Add(new StudentParentGuardian { StudentId = child.Id, ParentGuardianId = parent.Id, Relationship = "Parent" });
        await db.Context.SaveChangesAsync();
        var actorUserId = creatorRole switch { "Admin" => db.AdminUserId, "Principal" => principalUser.Id, "Section Head" => headUser.Id, _ => db.TeacherUserIds[0] };
        var actorStaffId = creatorRole switch { "Admin" => db.AdminStaffId, "Principal" => principal.Id, "Section Head" => head.Id, _ => db.TeacherIds[0] };
        SpecialClassController Controller(string userId) => new(db.Context, db.Users, new SpecialClassPush())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) } } };
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        while (date.DayOfWeek != DayOfWeek.Saturday) date = date.AddDays(1);
        if (creatorRole == "Teacher")
        {
            Assert.IsType<OkObjectResult>(await Controller(actorUserId).Request(new() { AcademicYearId = db.YearId, SchoolClassId = db.ClassId,
                SubjectId = db.SubjectRequest().SubjectId, ClassDate = date, StartTime = new(9, 0), EndTime = new(10, 0), Reason = "Revision" }));
        }
        else
        {
            Assert.IsType<OkObjectResult>(await Controller(actorUserId).Create(new() { AcademicYearId = db.YearId, SchoolClassId = db.ClassId,
                SubjectId = db.SubjectRequest().SubjectId, StaffId = db.TeacherIds[0], ClassDate = date, StartTime = new(9, 0), EndTime = new(10, 0), Reason = "Revision" }));
        }
        var session = await db.Context.SpecialClassSessions.SingleAsync();
        var alerts = await db.Context.Notifications.Where(x => x.ReferenceType == "SpecialClassSession" && x.ReferenceId == session.Id).ToListAsync();
        var expectedStaff = new[] { db.AdminStaffId, principal.Id, head.Id, db.TeacherIds[0] }.Where(id => id != actorStaffId).Order().ToArray();
        Assert.Equal(expectedStaff, alerts.Where(x => x.RecipientStaffId != null).Select(x => x.RecipientStaffId!.Value).Order().ToArray());
        Assert.DoesNotContain(alerts, x => x.RecipientStaffId == db.TeacherIds[1]);
        Assert.Equal(1, alerts.Count(x => x.RecipientStudentId == child.Id));
        Assert.Equal(1, alerts.Count(x => x.RecipientParentGuardianId == parent.Id));
        if (creatorRole == "Teacher")
        {
            Assert.IsType<OkObjectResult>(await Controller(headUser.Id).Review(session.Id, new() { Approve = true }));
            Assert.Equal(1, await db.Context.Notifications.CountAsync(x => x.Type == NotificationType.SpecialClassApproved && x.RecipientParentGuardianId == parent.Id));
            Assert.Equal(1, await db.Context.Notifications.CountAsync(x => x.Type == NotificationType.SpecialClassApproved && x.RecipientStudentId == child.Id));
        }
    }

    private sealed class SpecialClassPush : SchoolManagement.Application.Notifications.IPushNotificationService
    {
        public Task SendToStaffAsync(int staffId, string title, string message, string? referenceType = null, int? referenceId = null) => Task.CompletedTask;
        public Task SendToStudentAsync(int studentId, string title, string message, string? referenceType = null, int? referenceId = null) => Task.CompletedTask;
        public Task SendToParentAsync(int parentGuardianId, string title, string message, string? referenceType = null, int? referenceId = null) => Task.CompletedTask;
    }

    [Fact]
    public async Task SpecialClassRequestReviewConflictRescheduleAndCancel()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = new TeacherAssignment { StaffId = db.TeacherIds[0], SubjectId = db.SubjectRequest().SubjectId,
            SchoolClassId = db.ClassId, AcademicYearId = db.YearId, IsActive = true };
        db.Context.TeacherAssignments.Add(assignment);
        var headUser = await db.CreateUserAsync("special-head", "Section Head");
        var head = new Staff { StaffNumber = "SPECIAL-HEAD", FullName = "Head", ApplicationUserId = headUser.Id };
        db.Context.Staff.Add(head); await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId, IsActive = true });
        await db.Context.SaveChangesAsync();
        SpecialClassController Controller(string userId) => new(db.Context, db.Users, new SpecialClassPush())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) } } };
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        while (date.DayOfWeek != DayOfWeek.Saturday) date = date.AddDays(1);
        var request = new SchoolManagement.Application.SpecialClasses.DTOs.RequestSpecialClassRequest { AcademicYearId = db.YearId,
            SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId, ClassDate = date, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0), Reason = "Exam revision" };
        var teacher = Controller(db.TeacherUserIds[0]);
        Assert.IsType<OkObjectResult>(await teacher.Request(request));
        Assert.IsType<BadRequestObjectResult>(await teacher.Request(request));
        var session = await db.Context.SpecialClassSessions.SingleAsync();
        Assert.IsType<ForbidResult>(await Controller(db.TeacherUserIds[1]).Review(session.Id, new() { Approve = true }));
        Assert.IsType<OkObjectResult>(await Controller(headUser.Id).Review(session.Id, new() { Approve = true }));
        var hidden = Assert.IsType<OkObjectResult>(await Controller(db.TeacherUserIds[1]).Upcoming(null, null, null));
        Assert.Equal(0, JsonSerializer.SerializeToElement(hidden.Value).GetProperty("count").GetInt32());
        Assert.IsType<OkObjectResult>(await Controller(headUser.Id).Reschedule(session.Id, new() { ClassDate = date, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0), Reason = "Changed time" }));
        Assert.IsType<BadRequestObjectResult>(await teacher.Cancel(session.Id, new() { Reason = "Cancelled revision" }));
        Assert.IsType<OkObjectResult>(await Controller(headUser.Id).Cancel(session.Id, new() { Reason = "Cancelled revision" }));
        Assert.False(session.IsActive);
        Assert.True(await db.Context.Notifications.AnyAsync(x => x.ReferenceType == "SpecialClassSession"));
    }

    [Fact]
    public async Task ScopedAnnouncementCreatorsCannotReachUnassignedRecipients()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = new TeacherAssignment { StaffId = db.TeacherIds[0], SubjectId = db.SubjectRequest().SubjectId,
            SchoolClassId = db.ClassId, AcademicYearId = db.YearId, IsActive = true };
        var child = new Student { FullName = "Assigned student", IndexNumber = "SCOPED", SchoolClassId = db.ClassId, IsActive = true };
        var other = new Student { FullName = "Not enrolled in subject", IndexNumber = "OUTSIDE", SchoolClassId = db.ClassId, IsActive = true };
        db.Context.AddRange(assignment, child, other); await db.Context.SaveChangesAsync();
        foreach (var student in new[] { child, other })
            db.Context.StudentAcademicEnrollments.Add(new StudentAcademicEnrollment { StudentId = student.Id,
                AcademicYearId = db.YearId, SchoolClassId = db.ClassId, CreatedByStaffId = db.AdminStaffId, IsActive = true });
        db.Context.StudentSubjectEnrollments.Add(new StudentSubjectEnrollment { StudentId = child.Id,
            AcademicYearId = db.YearId, SubjectId = assignment.SubjectId, EnrolledByStaffId = db.AdminStaffId, IsActive = true });
        await db.Context.SaveChangesAsync();
        SchoolAnnouncementsController Controller(int index, string role) => new(db.Context, null!, new PublishingAudit())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[index]), new Claim(ClaimTypes.Role, role)], "Test")) } } };
        var teacher = Controller(0, "Teacher");
        var lookup = Assert.IsType<OkObjectResult>(await teacher.GetRecipientOptions());
        Assert.Single(JsonSerializer.SerializeToElement(lookup.Value).GetProperty("students").EnumerateArray());
        Assert.IsType<ForbidResult>(await teacher.CreateAnnouncement(new() { Title = "Outside", Message = "Test", AudienceType = "SpecificStudents", StudentIds = [other.Id] }));
        Assert.IsType<ForbidResult>(await teacher.CreateAnnouncement(new() { Title = "Everyone", Message = "Test", AudienceType = "AllStudents" }));
        Assert.IsType<OkObjectResult>(await teacher.CreateAnnouncement(new() { Title = "My subject", Message = "Test", AudienceType = "SubjectStudents", TeacherAssignmentId = assignment.Id }));
        var announcement = await db.Context.SchoolAnnouncements.SingleAsync();
        Assert.Equal(new[] { child.Id }, announcement.StudentIds);
        Assert.IsType<ForbidResult>(await Controller(1, "Teacher").DisableAnnouncement(announcement.Id));
        Assert.IsType<ForbidResult>(await Controller(1, "Teacher").GetAnnouncementById(announcement.Id));
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = db.TeacherIds[1], SectionId = db.SectionId, AcademicYearId = db.YearId, IsActive = true });
        await db.Context.SaveChangesAsync();
        var head = Controller(1, "Section Head");
        Assert.IsType<OkObjectResult>(await head.CreateAnnouncement(new() { Title = "My teachers", Message = "Test", AudienceType = "SectionTeachers", SectionId = db.SectionId }));
        Assert.IsType<ForbidResult>(await head.CreateAnnouncement(new() { Title = "Other section", Message = "Test", AudienceType = "SectionStudents", SectionId = int.MaxValue }));
        Assert.IsType<ForbidResult>(await head.CreateAnnouncement(new() { Title = "All staff", Message = "Test", AudienceType = "AllStaff" }));
    }

    [Fact]
    public async Task RecipientLookupReturnsNumbersAndParentFeedIsPrivate()
    {
        await using var db = await TestDatabase.CreateAsync();
        var parents = new[] {
            new ParentGuardian { ParentNumber = "P001", FullName = "First Parent", ApplicationUserId = db.TeacherUserIds[0] },
            new ParentGuardian { ParentNumber = "P002", FullName = "Other Parent", ApplicationUserId = db.TeacherUserIds[1] } };
        db.Context.ParentGuardians.AddRange(parents); await db.Context.SaveChangesAsync();
        var child = new Student { IndexNumber = "P-CHILD", FullName = "Linked child", SchoolClassId = db.ClassId, IsActive = true };
        db.Context.StudentParentGuardians.Add(new StudentParentGuardian { ParentGuardianId = parents[0].Id, Student = child, Relationship = "Parent", IsActive = true });
        db.Context.SchoolAnnouncements.Add(new SchoolAnnouncement { Title = "Students only", Message = "Not for parents",
            AudienceType = "AllStudents", PublishAt = DateTime.UtcNow.AddMinutes(-1), CreatedByStaffId = db.AdminStaffId });
        db.Context.SchoolAnnouncements.Add(new SchoolAnnouncement { Title = "Private parent message", Message = "Selected parent",
            AudienceType = "SpecificParents", ParentIds = [parents[0].Id], PublishAt = DateTime.UtcNow.AddMinutes(-1), CreatedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        var lookup = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.AdminUserId), new Claim(ClaimTypes.Role, "Admin")], "Test")) } } };
        var response = Assert.IsType<OkObjectResult>(await lookup.GetRecipientOptions());
        var options = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal("P001", options.GetProperty("parents")[0].GetProperty("code").GetString());
        Assert.NotEmpty(options.GetProperty("staff").EnumerateArray());
        for (var index = 0; index < 2; index++)
        {
            var controller = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[index]), new Claim(ClaimTypes.Role, "Parent")], "Test")) } } };
            var result = Assert.IsType<OkObjectResult>(await controller.GetMyParentAnnouncements());
            Assert.Equal(index == 0 ? 1 : 0, JsonSerializer.SerializeToElement(result.Value).GetProperty("announcements").GetArrayLength());
        }
        db.Context.SchoolAnnouncements.Add(new SchoolAnnouncement { Title = "All parents", Message = "Parent role message",
            AudienceType = "Role", RoleName = "Parent", PublishAt = DateTime.UtcNow.AddMinutes(-1), CreatedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        var otherParent = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[1]), new Claim(ClaimTypes.Role, "Parent")], "Test")) } } };
        var roleFeed = Assert.IsType<OkObjectResult>(await otherParent.GetMyParentAnnouncements());
        Assert.Equal(1, JsonSerializer.SerializeToElement(roleFeed.Value).GetProperty("announcements").GetArrayLength());
    }

    [Fact]
    public async Task TargetedStudentAnnouncementDoesNotReachOtherStudents()
    {
        await using var db = await TestDatabase.CreateAsync();
        var students = Enumerable.Range(0, 2).Select(index => new Student { IndexNumber = "ANN" + index,
            FullName = "Student " + index, SchoolClassId = db.ClassId, ApplicationUserId = db.TeacherUserIds[index], IsActive = true }).ToList();
        db.Context.Students.AddRange(students); await db.Context.SaveChangesAsync();
        db.Context.SchoolAnnouncements.Add(new SchoolAnnouncement { Title = "Private", Message = "Selected student",
            AudienceType = "SpecificStudents", StudentIds = [students[0].Id], PublishAt = DateTime.UtcNow.AddMinutes(-1), CreatedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        for (var index = 0; index < 2; index++)
        {
            var controller = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[index]), new Claim(ClaimTypes.Role, "Student")], "Test")) } } };
            var result = Assert.IsType<OkObjectResult>(await controller.GetMyStudentAnnouncements());
            Assert.Equal(index == 0 ? 1 : 0, JsonSerializer.SerializeToElement(result.Value).GetProperty("totalAnnouncements").GetInt32());
        }
    }

    [Theory]
    [InlineData("SpecificStaff")]
    [InlineData("SpecificSectionHeads")]
    public async Task TargetedAnnouncementsAreVisibleOnlyToSelectedStaff(string audience)
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Context.SchoolAnnouncements.Add(new SchoolAnnouncement { Title = "Private announcement", Message = "Selected only",
            AudienceType = audience, StaffIds = [db.TeacherIds[0]], PublishAt = DateTime.UtcNow.AddMinutes(-1), CreatedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        for (var index = 0; index < 2; index++)
        {
            var controller = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[index]), new Claim(ClaimTypes.Role, "Teacher")], "Test")) } } };
            var result = Assert.IsType<OkObjectResult>(await controller.GetMyStaffAnnouncements());
            var json = JsonSerializer.SerializeToElement(result.Value);
            Assert.Equal(index == 0 ? 1 : 0, json.GetProperty("totalAnnouncements").GetInt32());
        }
        var admin = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.AdminUserId), new Claim(ClaimTypes.Role, "Admin")], "Test")) } } };
        Assert.IsType<BadRequestObjectResult>(await admin.CreateAnnouncement(new SchoolManagement.Application.Announcements.DTOs.CreateSchoolAnnouncementRequest
            { Title = "Invalid", Message = "Test", AudienceType = audience, StaffIds = [int.MaxValue] }));
        Assert.IsType<BadRequestObjectResult>(await admin.CreateAnnouncement(new SchoolManagement.Application.Announcements.DTOs.CreateSchoolAnnouncementRequest
            { Title = "Empty", Message = "Test", AudienceType = audience }));
    }

    private sealed class PublishingAudit : SchoolManagement.Application.Auditing.IAuditLogService
    {
        public Task LogAsync(string action, string entityName, string? entityId = null, string? description = null,
            object? oldValues = null, object? newValues = null) => Task.CompletedTask;
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("class")]
    [InlineData("head")]
    [InlineData("unrelated")]
    public async Task PublishingRequiresApprovalAndScopedAccess(string actor)
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = new TeacherAssignment { StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId,
            AcademicYearId = db.YearId, SubjectId = db.SubjectRequest().SubjectId, IsActive = true };
        var exam = new Exam { Name = "Publishing test", MaximumMarks = 100,
            AcademicTerm = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId,
                StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(3) } };
        var submission = new MarksSubmission { Exam = exam, TeacherAssignment = assignment,
            SubmittedByStaffId = db.TeacherIds[0], Status = MarksSubmissionStatus.Submitted };
        db.Context.MarksSubmissions.Add(submission);
        await db.Context.SaveChangesAsync();
        var userId = actor == "subject" ? db.TeacherUserIds[0] : db.TeacherUserIds[1];
        var role = "Teacher";
        if (actor == "class")
            db.Context.ClassTeacherAssignments.Add(new ClassTeacherAssignment { StaffId = db.TeacherIds[1],
                SchoolClassId = db.ClassId, AcademicYearId = db.YearId, AssignedByStaffId = db.AdminStaffId, IsActive = true });
        if (actor == "head")
        {
            role = "Section Head";
            db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = db.TeacherIds[1],
                SectionId = db.SectionId, AcademicYearId = db.YearId, IsActive = true });
        }
        await db.Context.SaveChangesAsync();
        var controller = new MarksReviewController(db.Context, null!, new PublishingAudit())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "Test")) } } };
        if (actor == "unrelated")
        {
            submission.Status = MarksSubmissionStatus.Approved; await db.Context.SaveChangesAsync();
            var denied = Assert.IsType<ObjectResult>(await controller.Publish(submission.Id));
            Assert.Equal(403, denied.StatusCode);
            return;
        }
        Assert.IsType<BadRequestObjectResult>(await controller.Publish(submission.Id));
        submission.Status = MarksSubmissionStatus.Approved; await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await controller.GetApproved());
        Assert.IsType<OkObjectResult>(await controller.GetSubmission(submission.Id));
        Assert.IsType<OkObjectResult>(await controller.Publish(submission.Id));
        Assert.Equal(MarksSubmissionStatus.Published, submission.Status);
        Assert.IsType<BadRequestObjectResult>(await controller.Publish(submission.Id));
    }

    [Fact]
    public async Task MarksReviewLoadsWithoutFirebaseCredentials()
    {
        await using var db = await TestDatabase.CreateAsync();
        var user = await db.CreateUserAsync("review-head", "Section Head");
        var head = new Staff { StaffNumber = "REVIEW-HEAD", FullName = "Head", ApplicationUserId = user.Id };
        db.Context.Staff.Add(head);
        await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment
        { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId, IsActive = true });
        await db.Context.SaveChangesAsync();
        var push = new SchoolManagement.Infrastructure.Notifications.FirebasePushNotificationService(db.Context,
            Microsoft.Extensions.Options.Options.Create(new SchoolManagement.Application.Notifications.FirebaseSettings
            { CredentialsPath = "missing-development-firebase.json" }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SchoolManagement.Infrastructure.Notifications.FirebasePushNotificationService>.Instance);
        var controller = new MarksReviewController(db.Context, push, null!)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Role, "Section Head")], "Test")) } } };
        Assert.IsType<OkObjectResult>(await controller.GetPending());
        await push.SendToStaffAsync(head.Id, "Test", "No device tokens");
    }

    private sealed class AttendanceClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task SectionHeadAssignmentsEditRemoveReactivateAndRejectDuplicates()
    {
        await using var db = await TestDatabase.CreateAsync();
        var headUser = await db.CreateUserAsync("head-management", "Section Head");
        var head = new Staff { StaffNumber = "HEAD-M", FullName = "Head", ApplicationUserId = headUser.Id };
        var otherSection = new Section { Name = "Other" };
        db.Context.AddRange(head, otherSection); await db.Context.SaveChangesAsync();
        var controller = new SectionHeadAssignmentsController(db.Context)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.AdminUserId), new Claim(ClaimTypes.Role, "Admin")], "Test")) } } };
        var request = new SchoolManagement.Application.Academic.DTOs.CreateSectionHeadAssignmentRequest
            { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId };
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        var id = await db.Context.SectionHeadAssignments.Select(x => x.Id).SingleAsync();
        request.SectionId = otherSection.Id;
        Assert.IsType<OkObjectResult>(await controller.Update(id, request));
        Assert.Equal(otherSection.Id, await db.Context.SectionHeadAssignments.Where(x => x.Id == id).Select(x => x.SectionId).SingleAsync());
        Assert.IsType<OkObjectResult>(await controller.Disable(id));
        Assert.False((await db.Context.SectionHeadAssignments.FindAsync(id))!.IsActive);
        Assert.True((await db.Context.Users.FindAsync(headUser.Id))!.IsActive);
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        Assert.Equal(id, await db.Context.SectionHeadAssignments.Select(x => x.Id).SingleAsync());
        request.SectionId = db.SectionId;
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        Assert.IsType<ConflictObjectResult>(await controller.Update(id, request));
        request.StaffId = db.TeacherIds[0];
        Assert.IsType<BadRequestObjectResult>(await controller.Update(id, request));
        Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        Assert.Equal(2, await db.Context.SectionHeadAssignments.CountAsync());
        Assert.Equal(otherSection.Id, await db.Context.SectionHeadAssignments.Where(x => x.Id == id).Select(x => x.SectionId).SingleAsync());
        Assert.Equal(2, await db.Context.AuditLogs.CountAsync(x => x.EntityName == "SectionHeadAssignment"));
    }

    [Fact]
    public async Task SectionHeadTemporaryTeacherCanAttendOnlyAssignedClassUntilExpiry()
    {
        await using var db = await TestDatabase.CreateAsync();
        var headUser = await db.CreateUserAsync("head", "Section Head");
        var head = new Staff { StaffNumber = "HEAD", FullName = "Head", ApplicationUserId = headUser.Id };
        db.Context.Staff.Add(head); await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { AcademicYearId = db.YearId, SectionId = db.SectionId, StaffId = head.Id });
        await db.Context.SaveChangesAsync();
        var request = new AssignTemporaryClassTeacherRequest { AcademicYearId = db.YearId, SchoolClassId = db.ClassId,
            StaffId = db.TeacherIds[1], Reason = "Class teacher absent" };
        Assert.IsType<OkObjectResult>(await db.ClassController(headUser.Id).AssignTemporaryClassTeacher(request));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Attendance:SchoolTimeZoneId"] = "UTC" }).Build();
        var attendance = new AttendanceController(db.Context, db.Users, new AttendanceWindowService(db.Context, configuration))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[1])], "Test")) } } };
        var response = Assert.IsType<OkObjectResult>(await attendance.GetMyAttendanceClasses());
        Assert.Equal(1, JsonSerializer.SerializeToElement(response.Value).GetProperty("classes").GetArrayLength());
        var outside = new SchoolClass { Name = "Other", Grade = new Grade { Name = "12", Section = new Section { Name = "Other section" } } };
        db.Context.SchoolClasses.Add(outside); await db.Context.SaveChangesAsync();
        request.SchoolClassId = outside.Id;
        Assert.IsType<ForbidResult>(await db.ClassController(headUser.Id).AssignTemporaryClassTeacher(request));
        var temporary = await db.Context.TemporaryClassTeacherAssignments.SingleAsync();
        temporary.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); await db.Context.SaveChangesAsync();
        response = Assert.IsType<OkObjectResult>(await attendance.GetMyAttendanceClasses());
        Assert.Equal(0, JsonSerializer.SerializeToElement(response.Value).GetProperty("classes").GetArrayLength());
    }

    [Fact]
    public async Task AttendanceFirstEntryCorrectionsCutoffAndTeacherScopedPermission()
    {
        await using var db = await TestDatabase.CreateAsync();
        Assert.IsType<OkObjectResult>(await db.ClassController(db.AdminUserId).AssignClassTeacher(db.ClassRequest(0)));
        var monday = DateTime.Today;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);
        var clock = new AttendanceClock(new DateTimeOffset(DateTime.SpecifyKind(monday.AddHours(8).AddMinutes(30), DateTimeKind.Unspecified), TimeSpan.Zero));
        var windows = new AttendanceWindowService(db.Context, new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Attendance:SchoolTimeZoneId"] = "UTC" }).Build(), clock);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = monday, EndDate = monday.AddMonths(2) };
        var first = new Student { IndexNumber = "S1", FullName = "First", SchoolClassId = db.ClassId };
        var second = new Student { IndexNumber = "S2", FullName = "Second", SchoolClassId = db.ClassId };
        db.Context.AddRange(term, first, second);
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { AcademicYearId = db.YearId, SectionId = db.SectionId, StaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        var assignment = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(assignment));
        Assert.IsType<OkObjectResult>(await TimetableFor(db, db.AdminUserId).Create(new CreateTimetableEntryRequest {
            AcademicYearId = db.YearId, AcademicTermId = term.Id, SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId,
            StaffId = assignment.StaffId, Day = SchoolDay.Monday, StartTime = new TimeOnly(8, 15), EndTime = new TimeOnly(9, 0) }));
        ControllerContext Principal(string id) => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, id == db.AdminUserId ? "Section Head" : "Teacher")], "Test")) } };
        var attendance = new AttendanceController(db.Context, db.Users, windows) { ControllerContext = Principal(db.TeacherUserIds[0]) };
        var approval = new AttendanceApprovalController(db.Context, windows) { ControllerContext = Principal(db.TeacherUserIds[0]) };
        var head = new AttendanceApprovalController(db.Context, windows) { ControllerContext = Principal(db.AdminUserId) };
        var mark = new MarkAttendanceRequest { AcademicYearId = db.YearId,
            SchoolClassId = db.ClassId, AttendanceDate = monday, Students = [new() { StudentId = first.Id, Status = AttendanceStatus.Present }, new() { StudentId = second.Id, Status = AttendanceStatus.Late }] };
        Assert.IsType<OkObjectResult>(await attendance.MarkAttendance(mark));
        var window = await windows.GetAsync(db.YearId, db.ClassId, monday, db.TeacherIds[0]);
        Assert.True(window.HasSavedAttendance); Assert.True(window.IsWindowOpen); Assert.False(window.CanSaveDirectly);
        mark.Students[0].Status = AttendanceStatus.Absent;
        Assert.IsType<ConflictObjectResult>(await attendance.MarkAttendance(mark));
        var correction = new SubmitAttendanceChangeRequest { AcademicYearId = db.YearId, SchoolClassId = db.ClassId, AttendanceDate = monday,
            Students = [new() { StudentId = first.Id, Status = AttendanceStatus.Absent, Remarks = "Correction" }] };
        Assert.IsType<OkObjectResult>(await approval.SubmitRequest(correction));
        var correctionId = await db.Context.AttendanceChangeRequests.Select(x => x.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await head.Review(correctionId, new ReviewAttendanceChangeRequest { Approve = true }));
        Assert.Equal(AttendanceStatus.Absent, await db.Context.StudentAttendances.Where(x => x.StudentId == first.Id).Select(x => x.Status).SingleAsync());
        clock.Now = clock.Now.AddHours(2);
        Assert.IsType<ConflictObjectResult>(await approval.SubmitRequest(correction));
        var permission = new SubmitAttendanceChangeRequest { AcademicYearId = db.YearId, SchoolClassId = db.ClassId,
            AttendanceDate = monday, IsPermissionRequest = true, Reason = "Late correction" };
        Assert.IsType<OkObjectResult>(await approval.SubmitRequest(permission));
        var permissionId = await db.Context.AttendanceChangeRequests.Where(x => x.IsPermissionRequest).Select(x => x.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await head.Review(permissionId, new ReviewAttendanceChangeRequest { Approve = true, PermissionMinutes = 30 }));
        Assert.True((await windows.GetAsync(db.YearId, db.ClassId, monday, db.TeacherIds[0])).CanSaveDirectly);
        Assert.False((await windows.GetAsync(db.YearId, db.ClassId, monday, db.TeacherIds[1])).CanSaveDirectly);
        mark.Students[0].Status = AttendanceStatus.Excused;
        Assert.IsType<OkObjectResult>(await attendance.MarkAttendance(mark));
        clock.Now = clock.Now.AddMinutes(31);
        Assert.IsType<ConflictObjectResult>(await attendance.MarkAttendance(mark));
    }

    [Fact]
    public async Task AttendancePermissionReportsMissingHeadAndRemainsPendingWithoutChangingAttendance()
    {
        await using var db = await TestDatabase.CreateAsync();
        Assert.IsType<OkObjectResult>(await db.ClassController(db.AdminUserId).AssignClassTeacher(db.ClassRequest(0)));
        var first = new Student { IndexNumber = "S001", FullName = "First", SchoolClassId = db.ClassId };
        var second = new Student { IndexNumber = "S002", FullName = "Second", SchoolClassId = db.ClassId };
        db.Context.AddRange(first, second); await db.Context.SaveChangesAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Attendance:SchoolTimeZoneId"] = "UTC" }).Build();
        var windows = new AttendanceWindowService(db.Context, configuration);
        var day = windows.SchoolNow.Date;
        var window = await windows.GetAsync(db.YearId, db.ClassId, day);
        Assert.False(window.CanSaveDirectly);
        Assert.Contains("No active academic term", window.UnavailableReason);
        var controller = new AttendanceApprovalController(db.Context, windows)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[0])], "Test")) } }
        };
        var request = new SubmitAttendanceChangeRequest { AcademicYearId = db.YearId, SchoolClassId = db.ClassId,
            AttendanceDate = day, IsPermissionRequest = true, Reason = "Missed attendance cutoff", Students = [] };
        var conflict = Assert.IsType<ConflictObjectResult>(await controller.SubmitRequest(request));
        Assert.Contains("No active Section Head", JsonSerializer.Serialize(conflict.Value));
        Assert.Equal(0, await db.Context.AttendanceChangeRequests.CountAsync());
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { AcademicYearId = db.YearId,
            SectionId = db.SectionId, StaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await controller.SubmitRequest(request));
        Assert.Equal(0, await db.Context.StudentAttendances.CountAsync());
        var response = Assert.IsType<OkObjectResult>(await controller.MyRequests(db.YearId, db.ClassId, day));
        var pending = JsonSerializer.SerializeToElement(response.Value).GetProperty("requests")[0];
        Assert.Equal(1, pending.GetProperty("Status").GetInt32());
        Assert.True(pending.GetProperty("IsPermissionRequest").GetBoolean());
        Assert.Equal(0, pending.GetProperty("Students").GetArrayLength());
        Assert.IsType<ConflictObjectResult>(await controller.SubmitRequest(request));
        Assert.Equal(1, await db.Context.AttendanceChangeRequests.CountAsync());
    }

    [Fact]
    public async Task CommonIntervalsReserveEveryClassSupportEditingAndDoNotCountAsLessons()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(assignment));
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = year!.StartDate, EndDate = year.EndDate };
        db.Context.AcademicTerms.Add(term); await db.Context.SaveChangesAsync();
        TimetableIntervalsController Intervals(string userId) => new(db.Context, db.Users)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) } }
        };
        var intervals = Intervals(db.AdminUserId);
        var request = new TimetableIntervalsController.IntervalRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            Day = SchoolDay.Monday, StartTime = new TimeOnly(7, 30), EndTime = new TimeOnly(8, 15) };
        Assert.IsType<BadRequestObjectResult>(await intervals.Create(request));
        request.Day = SchoolDay.Tuesday; request.StartTime = new TimeOnly(10, 0); request.EndTime = new TimeOnly(10, 15);
        Assert.IsType<ForbidResult>(await Intervals(db.TeacherUserIds[0]).Create(request));
        Assert.IsType<OkObjectResult>(await intervals.Create(request));
        var id = await db.Context.TimetableIntervals.Select(x => x.Id).SingleAsync();
        Assert.IsType<BadRequestObjectResult>(await intervals.Create(request));
        var lesson = new CreateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId, StaffId = assignment.StaffId,
            Day = SchoolDay.Tuesday, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 45) };
        Assert.Contains("interval", JsonSerializer.Serialize(Assert.IsType<BadRequestObjectResult>(await TimetableFor(db, db.AdminUserId).Create(lesson)).Value));
        var otherClass = new SchoolClass { Name = "B", GradeId = (await db.Context.SchoolClasses.FindAsync(db.ClassId))!.GradeId };
        db.Context.SchoolClasses.Add(otherClass); await db.Context.SaveChangesAsync();
        lesson.SchoolClassId = otherClass.Id;
        Assert.Contains("interval", JsonSerializer.Serialize(Assert.IsType<BadRequestObjectResult>(await TimetableFor(db, db.AdminUserId).Create(lesson)).Value));
        lesson.SchoolClassId = db.ClassId;
        lesson.StartTime = new TimeOnly(10, 15); lesson.EndTime = new TimeOnly(11, 0);
        Assert.IsType<OkObjectResult>(await TimetableFor(db, db.AdminUserId).Create(lesson));
        request.StartTime = new TimeOnly(10, 30); request.EndTime = new TimeOnly(10, 45);
        Assert.IsType<BadRequestObjectResult>(await intervals.Update(id, request));
        request.StartTime = new TimeOnly(9, 45); request.EndTime = new TimeOnly(10, 15);
        Assert.IsType<OkObjectResult>(await intervals.Update(id, request));
        var teacherFeed = Assert.IsType<OkObjectResult>(await Intervals(db.TeacherUserIds[0]).Get());
        Assert.Equal(1, JsonSerializer.SerializeToElement(teacherFeed.Value).GetArrayLength());
        var unrelatedFeed = Assert.IsType<OkObjectResult>(await Intervals(db.TeacherUserIds[1]).Get());
        Assert.Equal(1, JsonSerializer.SerializeToElement(unrelatedFeed.Value).GetArrayLength());
        // Nine lessons plus an interval are allowed on Tuesday.
        for (var i = 0; i < 8; i++)
        {
            lesson.StartTime = new TimeOnly(12 + i, 0); lesson.EndTime = new TimeOnly(12 + i, 45);
            Assert.IsType<OkObjectResult>(await TimetableFor(db, db.AdminUserId).Create(lesson));
        }
        Assert.Equal(9, await db.Context.TimetableEntries.CountAsync());
        Assert.IsType<OkObjectResult>(await intervals.Remove(id));
        Assert.Equal(0, await db.Context.TimetableIntervals.CountAsync(x => x.IsActive));
        request.StartTime = new TimeOnly(9, 45); request.EndTime = new TimeOnly(10, 15);
        Assert.IsType<OkObjectResult>(await intervals.Create(request));
    }

    [Fact]
    public async Task TimetableIndependentClassesRespectDailyLimitsAndReleaseRemovedSlots()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(assignment));
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = year!.StartDate, EndDate = year.EndDate };
        var target = new SchoolClass { Name = "B", GradeId = (await db.Context.SchoolClasses.FindAsync(db.ClassId))!.GradeId };
        db.Context.AddRange(term, target); await db.Context.SaveChangesAsync();
        var controller = TimetableFor(db, db.AdminUserId);
        var request = new CreateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = target.Id, SubjectId = assignment.SubjectId, StaffId = assignment.StaffId };
        foreach (var day in new[] { SchoolDay.Monday, SchoolDay.Tuesday, SchoolDay.Wednesday, SchoolDay.Thursday, SchoolDay.Friday })
        {
            request.Day = day;
            var limit = day == SchoolDay.Friday ? 5 : day == SchoolDay.Monday ? 8 : 9;
            for (var i = 0; i < limit; i++)
            {
                request.StartTime = new TimeOnly(8 + i, 15); request.EndTime = new TimeOnly(9 + i, 0);
                Assert.IsType<OkObjectResult>(await controller.Create(request));
            }
            request.StartTime = new TimeOnly(18, 0); request.EndTime = new TimeOnly(18, 45);
            var rejected = Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
            Assert.Contains("maximum", JsonSerializer.Serialize(rejected.Value));
        }
        Assert.True(await db.Context.TeacherAssignments.AnyAsync(x => x.SchoolClassId == target.Id && x.StaffId == assignment.StaffId && x.IsActive));
        var monday = await db.Context.TimetableEntries.FirstAsync(x => x.Day == SchoolDay.Monday);
        var update = new UpdateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = target.Id, SubjectId = assignment.SubjectId, StaffId = assignment.StaffId,
            Day = SchoolDay.Friday, StartTime = new TimeOnly(19, 0), EndTime = new TimeOnly(19, 45), IsActive = true };
        Assert.IsType<BadRequestObjectResult>(await controller.Update(monday.Id, update));
        update.Day = SchoolDay.Monday;
        Assert.IsType<OkObjectResult>(await controller.Update(monday.Id, update));
        Assert.IsType<OkObjectResult>(await controller.Deactivate(monday.Id));
        request.Day = SchoolDay.Monday;
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        Assert.Equal(8, await db.Context.TimetableEntries.CountAsync(x => x.IsActive && x.Day == SchoolDay.Monday));
    }

    [Fact]
    public async Task TimetablePeriodsSupportAdjacentTimesUpdateAndDeactivation()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(assignment));
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = year!.StartDate, EndDate = year.EndDate };
        db.Context.AcademicTerms.Add(term);
        await db.Context.SaveChangesAsync();
        var controller = TimetableFor(db, db.AdminUserId);
        var request = new CreateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId, StaffId = assignment.StaffId,
            Day = SchoolDay.Monday, StartTime = new TimeOnly(8, 15), EndTime = new TimeOnly(9, 0), Room = null };
        request.StartTime = new TimeOnly(7, 30);
        var reserved = Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        Assert.Contains("Assembly", JsonSerializer.Serialize(reserved.Value));
        request.StartTime = new TimeOnly(8, 15);
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        var id = await db.Context.TimetableEntries.Select(x => x.Id).SingleAsync();
        request.StartTime = new TimeOnly(9, 0); request.EndTime = new TimeOnly(10, 0);
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        var update = new UpdateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId, StaffId = assignment.StaffId,
            Day = SchoolDay.Monday, StartTime = new TimeOnly(8, 15), EndTime = new TimeOnly(9, 30), Room = "11 A" };
        Assert.IsType<BadRequestObjectResult>(await controller.Update(id, update));
        update.StartTime = new TimeOnly(8, 0); update.EndTime = new TimeOnly(8, 15);
        Assert.Contains("Assembly", JsonSerializer.Serialize(Assert.IsType<BadRequestObjectResult>(await controller.Update(id, update)).Value));
        update.StartTime = new TimeOnly(8, 15); update.EndTime = new TimeOnly(8, 45);
        Assert.IsType<OkObjectResult>(await controller.Update(id, update));
        update.Room = "   ";
        Assert.IsType<OkObjectResult>(await controller.Update(id, update));
        Assert.Null((await db.Context.TimetableEntries.FindAsync(id))!.Room);
        update.Room = "11 A";
        term.IsActive = false; await db.Context.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.Update(id, update));
        term.IsActive = true; await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await controller.Deactivate(id));
        Assert.Equal(2, await db.Context.TimetableEntries.CountAsync());
        Assert.Equal(1, await db.Context.TimetableEntries.CountAsync(x => x.IsActive));
        Assert.IsType<ForbidResult>(await TimetableFor(db, db.TeacherUserIds[0]).Create(request));
    }

    [Fact]
    public async Task TimetableRejectsClassAndTeacherOverlapsAndUnassignedTeachers()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignments = new TeacherAssignmentsController(db.Context, db.Users);
        var subjectRequest = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await assignments.CreateAssignment(subjectRequest));
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = year!.StartDate, EndDate = year.EndDate };
        db.Context.AcademicTerms.Add(term);
        await db.Context.SaveChangesAsync();
        var controller = TimetableFor(db, db.AdminUserId);
        var request = new CreateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = db.ClassId, SubjectId = subjectRequest.SubjectId, StaffId = subjectRequest.StaffId,
            Day = SchoolDay.Monday, StartTime = new TimeOnly(8, 15), EndTime = new TimeOnly(9, 0), Room = "11 A" };
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        request.StaffId = db.TeacherIds[1];
        Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        subjectRequest.StaffId = db.TeacherIds[1];
        Assert.IsType<OkObjectResult>(await assignments.CreateAssignment(subjectRequest));
        var classConflict = Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        Assert.Contains("class already", JsonSerializer.Serialize(classConflict.Value));
        var otherClass = new SchoolClass { Name = "B", GradeId = (await db.Context.SchoolClasses.FindAsync(db.ClassId))!.GradeId };
        db.Context.SchoolClasses.Add(otherClass); await db.Context.SaveChangesAsync();
        subjectRequest.StaffId = db.TeacherIds[0]; subjectRequest.SchoolClassId = otherClass.Id;
        Assert.IsType<OkObjectResult>(await assignments.CreateAssignment(subjectRequest));
        request.StaffId = db.TeacherIds[0]; request.SchoolClassId = otherClass.Id;
        var teacherConflict = Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        Assert.Contains("teacher already", JsonSerializer.Serialize(teacherConflict.Value));
        request.StartTime = new TimeOnly(9, 0); request.EndTime = new TimeOnly(8, 0);
        Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        request.StartTime = new TimeOnly(10, 0); request.EndTime = new TimeOnly(11, 0);
        term.IsActive = false; await db.Context.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        Assert.Equal(1, await db.Context.TimetableEntries.CountAsync());
    }

    [Fact]
    public async Task TimetableDrivesTeacherFeedAndFirstPeriodAttendanceWindow()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(assignment));
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = year!.StartDate, EndDate = year.EndDate };
        db.Context.AcademicTerms.Add(term); await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await TimetableFor(db, db.AdminUserId).Create(new CreateTimetableEntryRequest
        {
            AcademicYearId = db.YearId, AcademicTermId = term.Id, SchoolClassId = db.ClassId,
            SubjectId = assignment.SubjectId, StaffId = assignment.StaffId,
            Day = SchoolDay.Monday, StartTime = new TimeOnly(8, 15), EndTime = new TimeOnly(8, 45), Room = "11 A"
        }));
        var feed = Assert.IsType<OkObjectResult>(await TimetableFor(db, db.TeacherUserIds[0]).GetTimetable(null, null, null, null, null, null, null));
        Assert.Equal(1, JsonSerializer.SerializeToElement(feed.Value).GetProperty("count").GetInt32());
        var otherFeed = Assert.IsType<OkObjectResult>(await TimetableFor(db, db.TeacherUserIds[1]).GetTimetable(null, null, null, null, null, null, null));
        Assert.Equal(0, JsonSerializer.SerializeToElement(otherFeed.Value).GetProperty("count").GetInt32());
        var monday = DateTime.Today;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Attendance:SchoolTimeZoneId"] = "UTC" }).Build();
        var window = await new AttendanceWindowService(db.Context, configuration).GetAsync(db.YearId, db.ClassId, monday);
        Assert.Equal(new TimeSpan(8, 15, 0), window.FirstPeriodStartsAt!.Value.TimeOfDay);
        Assert.Equal(new TimeSpan(9, 15, 0), window.RegularClosesAt!.Value.TimeOfDay);
    }

    private static TimetableController TimetableFor(TestDatabase db, string userId) => new(db.Context, db.Users)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) } }
    };

    [Fact]
    public async Task DevelopmentTeacherCanReadExamsButCannotManageThemAndSeedingIsIdempotent()
    {
        await using var db = await TestDatabase.CreateAsync();
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentUsers:AdminPassword"] = "TestOnly!Admin7392",
            ["DevelopmentUsers:TeacherPassword"] = "TestOnly!Teacher7392"
        }).Build();
        var seeder = new DevelopmentDataSeeder(db.Context, roles, db.Users, configuration);
        await seeder.SeedAsync();
        await seeder.SeedAsync();
        var teacherRole = await roles.FindByNameAsync("Teacher");
        Assert.Equal(1, await db.Context.RolePermissions.CountAsync(x =>
            x.RoleId == teacherRole!.Id && x.Permission.Name == "Exams.View"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[0]),
            new Claim(ClaimTypes.Role, "Teacher")], "Test"));
        var handler = new PermissionAuthorizationHandler(db.Context, roles);
        var readContext = new AuthorizationHandlerContext([new PermissionRequirement("Exams.View")], principal, null);
        await handler.HandleAsync(readContext);
        Assert.True(readContext.HasSucceeded);
        var manageContext = new AuthorizationHandlerContext([new PermissionRequirement("Exams.Manage")], principal, null);
        await handler.HandleAsync(manageContext);
        Assert.False(manageContext.HasSucceeded);
    }

    [Fact]
    public async Task SubjectTeacherGetsAttendanceAccessOnlyAfterClassTeacherAssignment()
    {
        await using var db = await TestDatabase.CreateAsync();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(db.SubjectRequest()));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Attendance:SchoolTimeZoneId"] = "UTC"
        }).Build();
        var controller = new AttendanceController(db.Context, db.Users, new AttendanceWindowService(db.Context, configuration))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[0]),
                    new Claim(ClaimTypes.Role, "Teacher")], "Test")) }
            }
        };
        var before = Assert.IsType<OkObjectResult>(await controller.GetMyAttendanceClasses());
        Assert.Equal(0, JsonSerializer.SerializeToElement(before.Value).GetProperty("count").GetInt32());
        Assert.IsType<OkObjectResult>(await db.ClassController(db.AdminUserId).AssignClassTeacher(db.ClassRequest(0)));
        var after = Assert.IsType<OkObjectResult>(await controller.GetMyAttendanceClasses());
        var result = JsonSerializer.SerializeToElement(after.Value);
        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.Equal(db.ClassId, result.GetProperty("classes")[0].GetProperty("schoolClassId").GetInt32());
    }

    [Fact]
    public async Task DisabledSubjectAssignmentIsReactivatedWithoutDuplicateRecords()
    {
        await using var db = await TestDatabase.CreateAsync();
        var controller = new TeacherAssignmentsController(db.Context, db.Users);
        var request = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await controller.CreateAssignment(request));
        var id = await db.Context.TeacherAssignments.Select(x => x.Id).SingleAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.CreateAssignment(request));
        Assert.IsType<OkObjectResult>(await controller.DeleteAssignment(id));
        var active = Assert.IsType<OkObjectResult>(await controller.GetAssignments());
        Assert.Equal(0, JsonSerializer.SerializeToElement(active.Value).GetArrayLength());
        Assert.IsType<OkObjectResult>(await controller.CreateAssignment(request));
        var assignment = await db.Context.TeacherAssignments.SingleAsync();
        Assert.Equal(id, assignment.Id);
        Assert.True(assignment.IsActive);
    }

    [Fact]
    public async Task SubjectAssignmentRejectsInactiveYearsAndNonTeacherOrInactiveAccounts()
    {
        await using var db = await TestDatabase.CreateAsync();
        var controller = new TeacherAssignmentsController(db.Context, db.Users);
        var request = db.SubjectRequest();
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        year!.IsActive = false;
        await db.Context.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.CreateAssignment(request));
        year.IsActive = true;
        await db.Context.SaveChangesAsync();
        request.StaffId = db.AdminStaffId;
        Assert.IsType<BadRequestObjectResult>(await controller.CreateAssignment(request));
        request.StaffId = db.TeacherIds[0];
        var teacher = await db.Users.FindByIdAsync(db.TeacherUserIds[0]);
        teacher!.IsActive = false;
        await db.Users.UpdateAsync(teacher);
        Assert.IsType<BadRequestObjectResult>(await controller.CreateAssignment(request));
        Assert.Equal(0, await db.Context.TeacherAssignments.CountAsync());
    }

    [Fact]
    public async Task ClassTeacherReplacementRequiresCurrentAssignmentAndPreservesHistory()
    {
        await using var db = await TestDatabase.CreateAsync();
        var controller = db.ClassController(db.AdminUserId);
        Assert.IsType<OkObjectResult>(await controller.AssignClassTeacher(db.ClassRequest(0)));
        var firstId = await db.Context.ClassTeacherAssignments.Select(x => x.Id).SingleAsync();
        var replace = db.ClassRequest(1);
        Assert.IsType<ConflictObjectResult>(await controller.AssignClassTeacher(replace));
        replace.ReplacesAssignmentId = firstId + 100;
        Assert.IsType<ConflictObjectResult>(await controller.AssignClassTeacher(replace));
        replace.ReplacesAssignmentId = firstId;
        Assert.IsType<OkObjectResult>(await controller.AssignClassTeacher(replace));
        var current = await db.Context.ClassTeacherAssignments.SingleAsync(x => x.IsActive);
        Assert.Equal(db.TeacherIds[1], current.StaffId);
        Assert.False((await db.Context.ClassTeacherAssignments.FindAsync(firstId))!.IsActive);
        var back = db.ClassRequest(0);
        back.ReplacesAssignmentId = current.Id;
        Assert.IsType<OkObjectResult>(await controller.AssignClassTeacher(back));
        Assert.Equal(2, await db.Context.ClassTeacherAssignments.CountAsync());
        Assert.Equal(firstId, await db.Context.ClassTeacherAssignments.Where(x => x.IsActive).Select(x => x.Id).SingleAsync());
        // No Firebase service is registered: permanent setup must not depend on push credentials.
    }

    [Fact]
    public async Task SectionHeadClassTeacherAccessIsScopedToSectionAndYear()
    {
        await using var db = await TestDatabase.CreateAsync();
        var admin = db.ClassController(db.AdminUserId);
        Assert.IsType<OkObjectResult>(await admin.AssignClassTeacher(db.ClassRequest(0)));
        var otherYear = new AcademicYear { Name = "Other year", IsActive = true, StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1) };
        db.Context.AcademicYears.Add(otherYear);
        await db.Context.SaveChangesAsync();
        var otherRequest = db.ClassRequest(1);
        otherRequest.AcademicYearId = otherYear.Id;
        Assert.IsType<OkObjectResult>(await admin.AssignClassTeacher(otherRequest));
        var headUser = await db.CreateUserAsync("head", "Section Head");
        var head = new Staff { StaffNumber = "HEAD", FullName = "Head", ApplicationUserId = headUser.Id };
        db.Context.Staff.Add(head);
        await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId });
        await db.Context.SaveChangesAsync();
        var controller = db.ClassController(headUser.Id);
        var result = Assert.IsType<OkObjectResult>(await controller.GetClassTeachers(null, null, null, null, true));
        var records = JsonSerializer.SerializeToElement(result.Value).GetProperty("classTeachers");
        Assert.Equal(1, records.GetArrayLength());
        Assert.Equal(db.YearId, records[0].GetProperty("academicYear").GetProperty("id").GetInt32());
        Assert.IsType<ForbidResult>(await controller.AssignClassTeacher(otherRequest));
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _name = "SchoolManagement_AssignmentTests_" + Guid.NewGuid().ToString("N");
        public ApplicationDbContext Context { get; }
        public ServiceProvider Services { get; }
        public UserManager<ApplicationUser> Users => Services.GetRequiredService<UserManager<ApplicationUser>>();
        public string AdminUserId { get; private set; } = "";
        public int AdminStaffId { get; private set; }
        public int YearId { get; private set; }
        public int ClassId { get; private set; }
        public int SectionId { get; private set; }
        private int _subjectId;
        public List<int> TeacherIds { get; } = [];
        public List<string> TeacherUserIds { get; } = [];

        private TestDatabase()
        {
            var sqlServer = Environment.GetEnvironmentVariable("SCHOOL_TEST_SQL_SERVER") ?? @"(localdb)\MSSQLLocalDB";
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer($"Server={sqlServer};Database={_name};Integrated Security=True;TrustServerCertificate=True", options => options.EnableRetryOnFailure()).Options);
            var services = new ServiceCollection().AddLogging().AddSingleton(Context);
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
            Services = services.BuildServiceProvider();
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var db = new TestDatabase();
            try
            {
                await db.Context.Database.EnsureCreatedAsync();
                var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
                foreach (var name in new[] { "Admin", "Teacher", "Section Head" })
                    Assert.True((await roles.CreateAsync(new IdentityRole(name))).Succeeded);
                var adminUser = await db.CreateUserAsync("admin", "Admin");
                db.AdminUserId = adminUser.Id;
                var admin = new Staff { StaffNumber = "ADMIN", FullName = "Admin", ApplicationUserId = adminUser.Id };
                db.Context.Staff.Add(admin);
                for (var i = 0; i < 2; i++)
                {
                    var user = await db.CreateUserAsync("teacher" + i, "Teacher");
                    var staff = new Staff { StaffNumber = "T" + i, FullName = "Teacher " + i, ApplicationUserId = user.Id };
                    db.Context.Staff.Add(staff);
                    await db.Context.SaveChangesAsync();
                    db.TeacherIds.Add(staff.Id);
                    db.TeacherUserIds.Add(user.Id);
                }
                var schoolClass = new SchoolClass { Name = "A", Grade = new Grade { Name = "11", Section = new Section { Name = "Secondary" } } };
                var year = new AcademicYear { Name = "Test year", IsActive = true, StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1) };
                var subject = new Subject { Name = "Math" };
                db.Context.AddRange(schoolClass, year, subject);
                await db.Context.SaveChangesAsync();
                db.ClassId = schoolClass.Id; db.SectionId = schoolClass.Grade.SectionId; db.YearId = year.Id;
                db._subjectId = subject.Id; db.AdminStaffId = admin.Id;
                return db;
            }
            catch { await db.DisposeAsync(); throw; }
        }

        public async Task<ApplicationUser> CreateUserAsync(string name, string role)
        {
            var user = new ApplicationUser { UserName = name, FullName = name, Email = name + "@example.test" };
            Assert.True((await Users.CreateAsync(user)).Succeeded);
            Assert.True((await Users.AddToRoleAsync(user, role)).Succeeded);
            return user;
        }

        public CreateTeacherAssignmentRequest SubjectRequest() => new() { AcademicYearId = YearId, SchoolClassId = ClassId, StaffId = TeacherIds[0], SubjectId = _subjectId };
        public AssignClassTeacherRequest ClassRequest(int teacher) => new() { AcademicYearId = YearId, SchoolClassId = ClassId, StaffId = TeacherIds[teacher] };
        public ClassTeacherController ClassController(string userId) => new(Context, Users, Services)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) }
            }
        };

        public async ValueTask DisposeAsync()
        {
            if (Context.Database.GetDbConnection().Database == _name && _name.StartsWith("SchoolManagement_AssignmentTests_", StringComparison.Ordinal))
                await Context.Database.EnsureDeletedAsync();
            await Services.DisposeAsync();
        }
    }
}
