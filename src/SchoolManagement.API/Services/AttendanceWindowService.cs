using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Services;

public sealed class AttendanceWindowService
{
    private readonly ApplicationDbContext _context;
    private readonly TimeZoneInfo _schoolZone;

    public AttendanceWindowService(
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _context = context;

        _schoolZone = TimeZoneInfo.FindSystemTimeZoneById(
            configuration["Attendance:SchoolTimeZoneId"]
            ?? "Asia/Colombo");
    }

    public DateTimeOffset SchoolNow =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _schoolZone);

    public DateTimeOffset ToSchool(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, _schoolZone);

    public async Task<AttendanceWindowState> GetAsync(
        int academicYearId,
        int schoolClassId,
        DateTime attendanceDate)
    {
        var day = attendanceDate.Date;
        var now = SchoolNow;

        var activeTermIds = await _context.AcademicTerms
            .AsNoTracking()
            .Where(x =>
                x.AcademicYearId == academicYearId &&
                x.IsActive &&
                x.StartDate.Date <= day &&
                x.EndDate.Date >= day)
            .Select(x => x.Id)
            .ToListAsync();

        var startTimes = await _context.TimetableEntries
            .AsNoTracking()
            .Where(x =>
                x.AcademicYearId == academicYearId &&
                activeTermIds.Contains(x.AcademicTermId) &&
                x.SchoolClassId == schoolClassId &&
                x.IsActive &&
                x.Day == (SchoolDay)(int)day.DayOfWeek)
            .Select(x => x.StartTime)
            .ToListAsync();

        DateTimeOffset? firstPeriodStartsAt = null;
        DateTimeOffset? regularClosesAt = null;

        if (startTimes.Count > 0)
        {
            var firstTime = startTimes.Min();

            var localStart = DateTime.SpecifyKind(
                day.Add(firstTime.ToTimeSpan()),
                DateTimeKind.Unspecified);

            firstPeriodStartsAt = new DateTimeOffset(
                localStart,
                _schoolZone.GetUtcOffset(localStart));

            regularClosesAt = firstPeriodStartsAt.Value.AddHours(1);
        }

        var extension = await _context.AttendanceWindowExtensions
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AcademicYearId == academicYearId &&
                x.SchoolClassId == schoolClassId &&
                x.AttendanceDate == day);

        DateTimeOffset? extendedUntil = null;

        if (extension != null)
        {
            var utc = DateTime.SpecifyKind(
                extension.ExtendedUntilUtc,
                DateTimeKind.Utc);

            extendedUntil = TimeZoneInfo.ConvertTime(
                new DateTimeOffset(utc),
                _schoolZone);
        }

        var today = day == now.Date;

        var regularOpen =
            today &&
            firstPeriodStartsAt.HasValue &&
            now >= firstPeriodStartsAt.Value &&
            now < regularClosesAt!.Value;

        var extendedOpen =
            today &&
            firstPeriodStartsAt.HasValue &&
            extendedUntil.HasValue &&
            now >= firstPeriodStartsAt.Value &&
            now < extendedUntil.Value;

        return new AttendanceWindowState(
            day,
            now,
            firstPeriodStartsAt,
            regularClosesAt,
            extendedUntil,
            regularOpen || extendedOpen);
    }
}

public sealed record AttendanceWindowState(
    DateTime AttendanceDate,
    DateTimeOffset SchoolNow,
    DateTimeOffset? FirstPeriodStartsAt,
    DateTimeOffset? RegularClosesAt,
    DateTimeOffset? ExtendedUntil,
    bool CanSaveDirectly);