namespace SchoolManagement.Application.Dashboard.DTOs;

public class StudentDashboardSummaryDto
{
    public int StudentId { get; set; }

    public string IndexNumber { get; set; } = string.Empty;

    public string StudentName { get; set; } = string.Empty;

    public int SchoolClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string GradeName { get; set; } = string.Empty;

    public string SectionName { get; set; } = string.Empty;

    public int TodayTimetableCount { get; set; }

    public int TodaySpecialClassCount { get; set; }

    public int UpcomingSpecialClassCount { get; set; }

    public int UnreadNotificationCount { get; set; }

    public int PublishedResultCount { get; set; }

    public int TotalAttendanceDays { get; set; }

    public int PresentDays { get; set; }

    public int AbsentDays { get; set; }

    public double AttendancePercentage { get; set; }

    public int ActiveAcademicYearId { get; set; }

    public string ActiveAcademicYearName { get; set; } = string.Empty;

    public int? ActiveAcademicTermId { get; set; }

    public string? ActiveAcademicTermName { get; set; }
}