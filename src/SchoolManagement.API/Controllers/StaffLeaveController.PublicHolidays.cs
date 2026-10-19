using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.API.Controllers;

public partial class StaffLeaveController
{
    public record ImportHolidaysRequest(int Year, string CountryCode, DateOnly[] Dates);

    [HttpGet("public-holidays/preview")]
    public async Task<IActionResult> PreviewPublicHolidays(int year, [FromServices] PublicHolidayService provider)
    {
        if (await PolicyAdmin() == null) return Forbid();
        if (year is < 1900 or > 2100) return BadRequest(new { message = "Choose a calendar year from 1900 to 2100." });
        var calendar = await _context.StaffLeaveCalendars.AsNoTracking().SingleOrDefaultAsync(c => c.Year == year);
        if (calendar == null || string.IsNullOrWhiteSpace(calendar.CountryCode)) return BadRequest(new { message = "Save the school calendar’s country code first." });
        try
        {
            var dates = await provider.Load(calendar.CountryCode, year, HttpContext.RequestAborted);
            var existing = await _context.StaffLeaveHolidays.Where(h => h.Date.Year == year).Select(h => h.Date).ToListAsync();
            return Ok(new { year, countryCode = calendar.CountryCode, provider = "Calendarific", holidays = dates.Select(h => new { h.Date, h.Name, alreadyListed = existing.Contains(h.Date) }) });
        }
        catch (PublicHolidayException error) { return StatusCode(503, new { message = error.Message }); }
    }

    [HttpPost("public-holidays/import")]
    public async Task<IActionResult> ImportPublicHolidays(ImportHolidaysRequest request, [FromServices] PublicHolidayService provider)
    {
        var actor = await PolicyAdmin(); if (actor == null) return Forbid();
        if (request.Year is < 1900 or > 2100 || request.Dates == null || request.Dates.Length is < 1 or > 366 || request.Dates.Any(d => d.Year != request.Year))
            return BadRequest(new { message = "Select holiday dates within the chosen year." });
        var calendar = await _context.StaffLeaveCalendars.AsNoTracking().SingleOrDefaultAsync(c => c.Year == request.Year);
        if (calendar == null || !string.Equals(calendar.CountryCode, request.CountryCode, StringComparison.OrdinalIgnoreCase)) return Conflict(new { message = "The country changed. Load the public holiday list again." });
        IReadOnlyList<PublicHolidayService.Holiday> holidays;
        try { holidays = await provider.Load(calendar.CountryCode, request.Year, HttpContext.RequestAborted); }
        catch (PublicHolidayException error) { return StatusCode(503, new { message = error.Message }); }
        var selected = request.Dates.Distinct().ToHashSet();
        if (selected.Any(date => !holidays.Any(h => h.Date == date))) return BadRequest(new { message = "The selection contains dates outside the provider’s holiday list. Load the list again." });
        return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await _context.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result=sp_getapplock @Resource='PublicHolidayImport', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Could not lock holiday import.', 1;");
            if (!await _context.StaffLeaveCalendars.AnyAsync(c => c.Year == request.Year && c.CountryCode == request.CountryCode.ToUpper())) return Conflict(new { message = "Calendar country changed. Reload holidays before importing." });
            var existing = await _context.StaffLeaveHolidays.Where(h => h.Date.Year == request.Year).Select(h => h.Date).ToListAsync();
            var added = 0;
            foreach (var holiday in holidays.Where(h => selected.Contains(h.Date) && !existing.Contains(h.Date))) {
                _context.StaffLeaveHolidays.Add(new StaffLeaveHoliday { Date = holiday.Date, Name = holiday.Name, UpdatedByStaffId = actor.Id }); added++;
            }
            await _context.SaveChangesAsync(); await transaction.CommitAsync();
            return (IActionResult)Ok(new { message = $"Imported {added} public holiday date(s). Existing school dates and manually removed holidays were preserved.", added, skipped = selected.Count - added });
        });
    }
}
