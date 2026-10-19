using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.API.Controllers;

public partial class StaffLeaveController
{
    private record CalendarFilePreview(int StaffId, int Year, string CountryCode, IReadOnlyList<GovernmentCalendarReader.Row> Rows);
    public record GovernmentCalendarImport(string PreviewToken, int[] Rows);

    [HttpGet("government-calendar/template")]
    public async Task<IActionResult> CalendarTemplate(int year, string format = "xlsx")
    {
        if (await PolicyAdmin() == null) return Forbid();
        if (year is < 1900 or > 2100 || format != "csv" && format != "xlsx") return BadRequest();
        if (format == "csv") return File(System.Text.Encoding.UTF8.GetBytes("From,Through,Name\r\n"), "text/csv", $"school-calendar-{year}-template.csv");
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Calendar");
        sheet.Cell(1, 1).Value = "From"; sheet.Cell(1, 2).Value = "Through"; sheet.Cell(1, 3).Value = "Name";
        sheet.Range("A1:C1").Style.Font.Bold = true; sheet.Column(1).Width = 18; sheet.Column(2).Width = 18; sheet.Column(3).Width = 55;
        sheet.Columns(1, 2).Style.DateFormat.Format = "yyyy-mm-dd";
        var notes = workbook.AddWorksheet("Instructions");
        notes.Cell(1, 1).Value = $"Enter approved holidays for {year} on the Calendar sheet. No example holidays are prefilled.";
        notes.Cell(2, 1).Value = "From: YYYY-MM-DD or an Excel date cell. Through: end of range, or blank for one day. Name: holiday/break name.";
        notes.Cell(3, 1).Value = "All imported holidays apply to staff and students. Weekends/overlapping holidays count only once.";
        notes.Cell(4, 1).Value = "Each row is a holiday or school closure (not a school working day). Use plain values, no formulas.";
        notes.Column(1).Width = 120;
        using var stream = new MemoryStream(); workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"school-calendar-{year}-template.xlsx");
    }

    [HttpPost("government-calendar/preview"), RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> PreviewGovernmentCalendar([FromForm] int year, [FromForm] IFormFile file, [FromServices] IMemoryCache cache)
    {
        var actor = await PolicyAdmin(); if (actor == null) return Forbid();
        if (year is < 1900 or > 2100 || file == null || file.Length is < 1 or > 2 * 1024 * 1024) return BadRequest(new { message = "Select a calendar year and an Excel/CSV file up to 2 MB." });
        var calendar = await _context.StaffLeaveCalendars.AsNoTracking().SingleOrDefaultAsync(c => c.Year == year);
        if (calendar == null) return BadRequest(new { message = "Save your school calendar’s country, dates and weekends before uploading holidays." });
        try {
            await using var stream = new MemoryStream(); await file.CopyToAsync(stream, HttpContext.RequestAborted); stream.Position = 0;
            var rows = GovernmentCalendarReader.Read(stream, Path.GetExtension(file.FileName), year);
            var existing = await _context.StaffLeaveHolidays.Where(h => h.Date.Year == year).Select(h => h.Date).ToListAsync();
            var token = Guid.NewGuid().ToString("N");
            cache.Set("government-calendar:" + token, new CalendarFilePreview(actor.Id, year, calendar.CountryCode, rows), TimeSpan.FromMinutes(20));
            return Ok(new { previewToken = token, year, countryCode = calendar.CountryCode, appliesTo = "Staff and students", expiresInMinutes = 20,
                rows = rows.Select(r => new { r.RowNumber, r.From, r.Through, r.Name, r.Error, r.Days, existingDates = r.Error == null ? existing.Count(d => d >= r.From && d <= r.Through) : 0 }) });
        } catch (CalendarFileException error) { return BadRequest(new { message = error.Message }); }
    }

    [HttpPost("government-calendar/import")]
    public async Task<IActionResult> ImportGovernmentCalendar(GovernmentCalendarImport request, [FromServices] IMemoryCache cache)
    {
        var actor = await PolicyAdmin(); if (actor == null) return Forbid();
        if (string.IsNullOrEmpty(request.PreviewToken) || request.Rows == null || request.Rows.Length is < 1 or > 1000) return BadRequest(new { message = "Preview the file and select valid holiday rows first." });
        if (!cache.TryGetValue<CalendarFilePreview>("government-calendar:" + request.PreviewToken, out var preview) || preview == null || preview.StaffId != actor.Id)
            return Conflict(new { message = "This preview expired or belongs to another administrator. Upload the file again." });
        var selected = request.Rows.Distinct().ToHashSet();
        if (selected.Any(row => !preview.Rows.Any(r => r.RowNumber == row && r.Error == null))) return BadRequest(new { message = "Select only valid rows from this file preview." });
        var dates = new Dictionary<DateOnly, HashSet<string>>();
        foreach (var row in preview.Rows.Where(r => selected.Contains(r.RowNumber)))
            for (var date = row.From!.Value; date <= row.Through!.Value; date = date.AddDays(1)) {
                if (!dates.TryGetValue(date, out var names)) dates[date] = names = []; names.Add(row.Name);
            }
        var result = await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await _context.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result=sp_getapplock @Resource='PublicHolidayImport', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Could not lock calendar import.', 1;");
            if (!await _context.StaffLeaveCalendars.AnyAsync(c => c.Year == preview.Year && c.CountryCode == preview.CountryCode)) return (IActionResult)Conflict(new { message = "Calendar country changed. Upload and review the file again." });
            var existing = await _context.StaffLeaveHolidays.Where(h => h.Date.Year == preview.Year).Select(h => h.Date).ToListAsync(); var added = 0;
            foreach (var entry in dates.Where(d => !existing.Contains(d.Key))) {
                var name = string.Join(" / ", entry.Value); if (name.Length > 200) name = name[..200];
                _context.StaffLeaveHolidays.Add(new StaffLeaveHoliday { Date = entry.Key, Name = name, UpdatedByStaffId = actor.Id }); added++;
            }
            await _context.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = $"Imported {added} holiday date(s) for staff and students. Existing dates were preserved; school-day totals and leave balances have been updated.", added, skipped = dates.Count - added });
        });
        if (result is OkObjectResult) cache.Remove("government-calendar:" + request.PreviewToken);
        return result;
    }
}
