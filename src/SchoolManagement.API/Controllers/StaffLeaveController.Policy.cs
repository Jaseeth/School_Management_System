using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.API.Controllers;

public partial class StaffLeaveController
{
    public record CalendarRequest(int Year, string CountryCode, string? Region, int[] WeekendDays, DateOnly? SchoolStart = null, DateOnly? SchoolEnd = null);
    public record AllowanceRequest(int Year, int? StaffId, int Days);
    public record HolidayRequest(DateOnly Date, string Name, DateOnly? ToDate = null);
    private async Task<Staff?> PolicyAdmin()
    {
        var staff = await GetCurrentStaffAsync(); if (staff == null) return null;
        var user = await _userManager.FindByIdAsync(staff.ApplicationUserId!);
        return user != null && await _userManager.IsInRoleAsync(user, "Admin") ? staff : null;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> LeaveSettings(int year)
    {
        if (await PolicyAdmin() == null) return Forbid();
        if (year < 1900 || year > 2100) return BadRequest();
        var calendar = await _context.StaffLeaveCalendars.AsNoTracking().SingleOrDefaultAsync(c => c.Year == year);
        var holidays = await _context.StaffLeaveHolidays.AsNoTracking().Where(h => h.Date.Year == year && h.IsActive).OrderBy(h => h.Date).Select(h => new { h.Id, h.Date, h.Name }).ToListAsync();
        var allowances = await _context.StaffLeaveAllowances.AsNoTracking().Where(a => a.Year == year && a.LeaveType == 0).Select(a => new { a.Id, a.StaffId, a.Days }).ToListAsync();
        var staff = await _context.Staff.Where(s => s.IsActive).OrderBy(s => s.StaffNumber).Select(s => new { s.Id, s.FullName, s.StaffNumber }).ToListAsync();
        return Ok(new { year, configured = calendar != null, countryCode = calendar?.CountryCode ?? "", region = calendar?.Region ?? "",
            schoolStart = calendar?.SchoolStart ?? new DateOnly(year, 1, 1), schoolEnd = calendar?.SchoolEnd ?? new DateOnly(year, 12, 31),
            weekendDays = Enumerable.Range(0, 7).Where(d => ((calendar?.WeekendMask ?? 65) & (1 << d)) != 0), holidays, allowances, staff });
    }

    [HttpPut("settings/calendar")]
    public async Task<IActionResult> SaveCalendar(CalendarRequest request)
    {
        var actor = await PolicyAdmin(); if (actor == null) return Forbid();
        if (request.SchoolStart.HasValue && request.SchoolStart.Value.Year != request.Year || request.SchoolEnd.HasValue && request.SchoolEnd.Value.Year != request.Year || request.SchoolStart > request.SchoolEnd)
            return BadRequest(new { message = "Choose the school opening and closing dates within the selected calendar year." });
        if (request.Year < 1900 || request.Year > 2100 || request.CountryCode == null || request.CountryCode.Length != 2 || !request.CountryCode.All(char.IsAsciiLetter) || request.Region?.Length > 100 || request.WeekendDays == null || request.WeekendDays.Any(d => d < 0 || d > 6) || request.WeekendDays.Distinct().Count() == 7)
            return BadRequest(new { message = "Provide a year (1900–2100), two-letter country code and weekend days. At least one day must be a working day." });
        var calendar = await _context.StaffLeaveCalendars.SingleOrDefaultAsync(c => c.Year == request.Year);
        if (calendar == null) { calendar = new StaffLeaveCalendar { Year = request.Year }; _context.StaffLeaveCalendars.Add(calendar); }
        calendar.CountryCode = request.CountryCode.ToUpperInvariant(); calendar.Region = request.Region?.Trim(); calendar.WeekendMask = request.WeekendDays.Distinct().Sum(d => 1 << d); calendar.UpdatedByStaffId = actor.Id; calendar.UpdatedAt = DateTime.UtcNow;
        calendar.SchoolStart = request.SchoolStart; calendar.SchoolEnd = request.SchoolEnd;
        await _context.SaveChangesAsync(); return Ok(new { message = "School leave calendar saved. Leave days and balances use these settings." });
    }

    [HttpPut("settings/allowance")]
    public async Task<IActionResult> SaveAllowance(AllowanceRequest request)
    {
        var actor = await PolicyAdmin(); if (actor == null) return Forbid();
        if (request.Year < 1900 || request.Year > 2100 || request.Days < 0 || request.Days > 366 || request.StaffId.HasValue && !await _context.Staff.AnyAsync(s => s.Id == request.StaffId && s.IsActive))
            return BadRequest(new { message = "Choose a valid year, staff member and shared yearly allowance (0–366 days)." });
        var allowance = await _context.StaffLeaveAllowances.SingleOrDefaultAsync(a => a.Year == request.Year && a.LeaveType == 0 && a.StaffId == request.StaffId);
        if (allowance == null) { allowance = new StaffLeaveAllowance { Year = request.Year, LeaveType = 0, StaffId = request.StaffId }; _context.StaffLeaveAllowances.Add(allowance); }
        allowance.Days = request.Days; allowance.UpdatedByStaffId = actor.Id; allowance.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(); return Ok(new { message = "Shared yearly allowance saved. All leave types use this one total." });
    }

    [HttpPost("settings/holidays")]
    public async Task<IActionResult> SaveHoliday(HolidayRequest request)
    {
        var actor = await PolicyAdmin(); if (actor == null) return Forbid();
        if (request.Date.Year < 1900 || request.Date.Year > 2100 || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200) return BadRequest(new { message = "Provide a holiday date and name (up to 200 characters)." });
        var end = request.ToDate ?? request.Date;
        if (end < request.Date || end.Year != request.Date.Year) return BadRequest(new { message = "Choose a holiday range within the same calendar year." });
        var existing = await _context.StaffLeaveHolidays.Where(h => h.Date >= request.Date && h.Date <= end).ToListAsync();
        for (var date = request.Date; date <= end; date = date.AddDays(1)) {
            var holiday = existing.SingleOrDefault(h => h.Date == date);
            if (holiday == null) { holiday = new StaffLeaveHoliday { Date = date }; _context.StaffLeaveHolidays.Add(holiday); }
            holiday.Name = request.Name.Trim(); holiday.IsActive = true; holiday.UpdatedByStaffId = actor.Id; holiday.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(); return Ok(new { message = "Holiday saved. This date is excluded from leave-day calculations." });
    }

    [HttpDelete("settings/holidays/{id:int}")]
    public async Task<IActionResult> RemoveHoliday(int id)
    {
        var actor = await PolicyAdmin(); if (actor == null) return Forbid();
        var holiday = await _context.StaffLeaveHolidays.FindAsync(id); if (holiday == null) return NotFound();
        holiday.IsActive = false; holiday.UpdatedByStaffId = actor.Id; holiday.UpdatedAt = DateTime.UtcNow; await _context.SaveChangesAsync();
        return Ok(new { message = "Holiday removed from the school calendar. Leave-day calculations have been updated." });
    }

    [HttpGet("balances")]
    public async Task<IActionResult> LeaveBalances(int year, int? staffId = null)
    {
        var actor = await GetCurrentStaffAsync(); if (actor == null) return Forbid();
        if (year < 1900 || year > 2100) return BadRequest();
        var leaders = await LeadershipIdsAsync(); var target = staffId ?? actor.Id;
        if (target != actor.Id && !leaders.Contains(actor.Id)) return Forbid();
        var staff = await _context.Staff.Where(s => s.Id == target && s.IsActive).Select(s => new { s.Id, s.FullName, s.StaffNumber }).SingleOrDefaultAsync();
        if (staff == null) return NotFound();
        var options = await _context.Staff.Where(s => s.IsActive && (leaders.Contains(actor.Id) || s.Id == actor.Id)).Select(s => new { s.Id, s.FullName, s.StaffNumber }).ToListAsync();
        var service = new StaffLeaveBalanceService(_context);
        return Ok(new { staff, staffOptions = options, rows = await service.Balances(target, year), total = await service.Total(target, year) });
    }

    [HttpGet("school-calendar")]
    public async Task<IActionResult> SchoolCalendar(int year)
    {
        var actor = await GetCurrentStaffAsync(); if (actor == null) return Forbid();
        if (year < 1900 || year > 2100) return BadRequest();
        return Ok(await SchoolCalendarService.Read(_context, year, await PolicyAdmin() != null));
    }

    [HttpGet("preview")]
    public async Task<IActionResult> LeavePreview(StaffLeaveType leaveType, DateTime from, DateTime to)
    {
        var actor = await GetCurrentStaffAsync(); if (actor == null) return Forbid();
        if (!Enum.IsDefined(leaveType) || from.Year < 1900 || to.Year > 2100 || from.Date > to.Date || (to.Date - from.Date).TotalDays > 3660) return BadRequest(new { message = "Choose a valid leave type and date range of up to ten years." });
        return Ok(await new StaffLeaveBalanceService(_context).Preview(actor.Id, leaveType, from.Date, to.Date));
    }

    [HttpGet("review-balance/{id:int}")]
    public async Task<IActionResult> ReviewBalance(int id)
    {
        var actor = await GetCurrentStaffAsync(); if (actor == null) return Forbid();
        var leave = await _context.StaffLeaveRequests.AsNoTracking().SingleOrDefaultAsync(l => l.Id == id); if (leave == null) return NotFound();
        var leaders = await LeadershipIdsAsync();
        if (leave.StaffId != actor.Id && !leaders.Contains(actor.Id) && !await SectionScope(_context.StaffLeaveRequests, actor.Id, true).AnyAsync(l => l.Id == id)) return Forbid();
        return Ok(await new StaffLeaveBalanceService(_context).Preview(leave.StaffId, leave.LeaveType, leave.FromDate, leave.ToDate, leave.Id));
    }
}
