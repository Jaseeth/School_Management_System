using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Services;

public class StaffLeaveBalanceService(ApplicationDbContext db)
{
    public record YearBalance(int Year, int LeaveType, string TypeName, int? Allowance, int Used, int Pending, int? Remaining, bool CalendarConfigured);
    public async Task<int> WorkingDays(DateTime from, DateTime to)
    {
        if (to.Date < from.Date) return 0;
        var calendars = await db.StaffLeaveCalendars.AsNoTracking().Where(c => c.Year >= from.Year && c.Year <= to.Year).ToDictionaryAsync(c => c.Year);
        var start = DateOnly.FromDateTime(from); var end = DateOnly.FromDateTime(to);
        var holidays = (await db.StaffLeaveHolidays.AsNoTracking().Where(h => h.IsActive && h.Date >= start && h.Date <= end).Select(h => h.Date).ToListAsync()).ToHashSet();
        var holidayEvents = await db.SchoolEvents.AsNoTracking().Where(e => !e.IsCancelled && e.IsHoliday && e.FromDate <= end && e.ThroughDate >= start).Select(e => new { e.FromDate, e.ThroughDate }).ToListAsync();
        foreach (var e in holidayEvents)
            for (var day = e.FromDate < start ? start : e.FromDate; day <= e.ThroughDate && day <= end; day = day.AddDays(1)) holidays.Add(day);
        var days = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            var mask = calendars.TryGetValue(day.Year, out var calendar) ? calendar.WeekendMask : 65;
            if (calendar != null && (calendar.SchoolStart.HasValue && day < calendar.SchoolStart.Value || calendar.SchoolEnd.HasValue && day > calendar.SchoolEnd.Value)) { if (day == end) break; continue; }
            if ((mask & (1 << (int)day.DayOfWeek)) == 0 && !holidays.Contains(day)) days++;
            if (day == end) break;
        }
        return days;
    }

    public async Task<List<YearBalance>> Balances(int staffId, int year, int? excludeLeaveId = null)
    {
        var start = new DateTime(year, 1, 1); var end = new DateTime(year, 12, 31);
        var leaves = await db.StaffLeaveRequests.AsNoTracking().Where(l => l.StaffId == staffId && l.Id != excludeLeaveId && l.FromDate <= end && l.ToDate >= start && (l.Status == StaffLeaveStatus.Approved || l.Status == StaffLeaveStatus.Pending)).ToListAsync();
        var configured = await db.StaffLeaveCalendars.AnyAsync(c => c.Year == year);
        var rows = new List<YearBalance>();
        foreach (var type in Enum.GetValues<StaffLeaveType>())
        {
            var used = 0; var pending = 0;
            foreach (var leave in leaves.Where(l => l.LeaveType == type))
            {
                var count = await WorkingDays(leave.FromDate < start ? start : leave.FromDate, leave.ToDate > end ? end : leave.ToDate);
                if (leave.Status == StaffLeaveStatus.Approved) used += count; else pending += count;
            }
            rows.Add(new(year, (int)type, type.ToString(), null, used, pending, null, configured));
        }
        return rows;
    }

    public async Task<YearBalance> Total(int staffId, int year, int? excludeLeaveId = null)
    {
        var rows = await Balances(staffId, year, excludeLeaveId);
        var allowance = await db.StaffLeaveAllowances.AsNoTracking().Where(a => a.Year == year && a.LeaveType == 0 && (a.StaffId == null || a.StaffId == staffId))
            .OrderByDescending(a => a.StaffId.HasValue).Select(a => (int?)a.Days).FirstOrDefaultAsync();
        var used = rows.Sum(r => r.Used);
        return new(year, 0, "All leave types", allowance, used, rows.Sum(r => r.Pending), allowance - used, rows.All(r => r.CalendarConfigured));
    }

    public async Task<object> Preview(int staffId, StaffLeaveType type, DateTime from, DateTime to, int? excludeLeaveId = null)
    {
        var rows = new List<object>(); var total = 0;
        for (var year = from.Year; year <= to.Year; year++)
        {
            var start = from > new DateTime(year, 1, 1) ? from : new DateTime(year, 1, 1);
            var end = to < new DateTime(year, 12, 31) ? to : new DateTime(year, 12, 31);
            var count = await WorkingDays(start, end); total += count;
            var balance = await Total(staffId, year, excludeLeaveId);
            rows.Add(new { year, workingDays = count, balance.Allowance, balance.Used, balance.Pending, balance.Remaining,
                exceedsBalance = balance.Remaining.HasValue && count > balance.Remaining.Value,
                balance.CalendarConfigured });
        }
        return new { workingDays = total, years = rows };
    }
}
