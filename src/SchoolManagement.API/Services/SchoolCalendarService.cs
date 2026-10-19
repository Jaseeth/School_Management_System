using Microsoft.EntityFrameworkCore;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Services;

public static class SchoolCalendarService
{
    public static async Task<object> Read(ApplicationDbContext _context, int year, bool canManage)
    {
        var config = await _context.StaffLeaveCalendars.AsNoTracking().SingleOrDefaultAsync(c => c.Year == year);
        var holidayRows = await _context.StaffLeaveHolidays.AsNoTracking().Where(h => h.IsActive && h.Date.Year == year).Select(h => new { h.Id, h.Date, h.Name }).ToListAsync();
        var holidays = holidayRows.ToDictionary(h => h.Date, h => h.Name);
        var holidayEvents = await _context.SchoolEvents.AsNoTracking().Where(e => !e.IsCancelled && e.IsHoliday && e.FromDate.Year <= year && e.ThroughDate.Year >= year).ToListAsync();
        foreach (var e in holidayEvents)
            for (var date = e.FromDate; date <= e.ThroughDate; date = date.AddDays(1))
                if (date.Year == year) holidays[date] = holidays.TryGetValue(date, out var name) ? name + "; " + e.Title : e.Title;
        var mask = config?.WeekendMask ?? 65;
        var start = config?.SchoolStart ?? new DateOnly(year, 1, 1); var end = config?.SchoolEnd ?? new DateOnly(year, 12, 31);
        var days = new List<object>(); var schoolDays = 0; var weekends = 0; var closed = 0;
        for (var date = new DateOnly(year, 1, 1); date.Year == year; date = date.AddDays(1)) {
            var weekend = (mask & (1 << (int)date.DayOfWeek)) != 0;
            holidays.TryGetValue(date, out var holiday);
            var outside = date < start || date > end;
            var schoolDay = !weekend && holiday == null && !outside;
            if (schoolDay) schoolDays++; if (weekend) weekends++; if (outside) closed++;
            days.Add(new { date, schoolDay, weekend, holiday, outsideSchoolPeriod = outside });
        }
        return new { year, configured = config != null, countryCode = config?.CountryCode ?? "", region = config?.Region ?? "", schoolStart = start, schoolEnd = end,
            weekendDays = Enumerable.Range(0, 7).Where(d => (mask & (1 << d)) != 0), holidays = holidayRows,
            canManage = canManage, days, summary = new { totalDays = days.Count, schoolDays, nonSchoolDays = days.Count - schoolDays, weekends, holidays = holidays.Count, outsideSchoolPeriod = closed } };
    }
}
