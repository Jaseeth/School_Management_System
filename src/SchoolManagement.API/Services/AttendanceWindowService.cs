using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Services;

public sealed class AttendanceWindowService
{
    private readonly ApplicationDbContext _context;
    private readonly TimeZoneInfo _schoolZone;
    private readonly TimeProvider _clock;

    public AttendanceWindowService(
        ApplicationDbContext context,
        IConfiguration configuration, TimeProvider? clock = null)
    {
        _context = context;
        _clock = clock ?? TimeProvider.System;

        _schoolZone = TimeZoneInfo.FindSystemTimeZoneById(
            configuration["Attendance:SchoolTimeZoneId"]
            ?? "Asia/Colombo");
    }

    public DateTimeOffset SchoolNow =>
        TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), _schoolZone);

    public DateTimeOffset ToSchool(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, _schoolZone);

    public async Task<AttendanceWindowState> GetAsync(
        int academicYearId,
        int schoolClassId,
        DateTime attendanceDate,
        int? staffId = null)
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

        var hasSavedAttendance = await _context.StudentAttendances.AsNoTracking().AnyAsync(x =>
            x.AcademicYearId == academicYearId && x.SchoolClassId == schoolClassId && x.AttendanceDate == day);
        var permissionUntil = staffId.HasValue ? await _context.AttendanceChangeRequests.AsNoTracking()
            .Where(x => x.IsPermissionRequest && x.Status == AttendanceChangeStatus.Approved &&
                x.RequestedByStaffId == staffId.Value && x.AcademicYearId == academicYearId &&
                x.SchoolClassId == schoolClassId && x.AttendanceDate == day && x.GrantedUntilUtc > now.UtcDateTime)
            .Select(x => x.GrantedUntilUtc).MaxAsync() : null;
        var hasEntryPermission = permissionUntil.HasValue || extendedOpen;

        return new AttendanceWindowState(
            day,
            now,
            firstPeriodStartsAt,
            regularClosesAt,
            permissionUntil.HasValue ? ToSchool(new DateTimeOffset(DateTime.SpecifyKind(permissionUntil.Value, DateTimeKind.Utc))) : extendedUntil,
            hasEntryPermission || (!hasSavedAttendance && regularOpen),
            activeTermIds.Count == 0
                ? "No active academic term covers this date. Check the term start and end dates in Academic Setup."
                : startTimes.Count == 0 ? "No teaching period is configured for this class and weekday in the term covering this date."
                : null,
            hasSavedAttendance,
            regularOpen || hasEntryPermission,
            hasEntryPermission);
    }
}

public sealed record AttendanceWindowState(
    DateTime AttendanceDate,
    DateTimeOffset SchoolNow,
    DateTimeOffset? FirstPeriodStartsAt,
    DateTimeOffset? RegularClosesAt,
    DateTimeOffset? ExtendedUntil,
    bool CanSaveDirectly,
    string? UnavailableReason = null,
    bool HasSavedAttendance = false,
    bool IsWindowOpen = false,
    bool HasEntryPermission = false);