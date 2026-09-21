namespace SchoolManagement.Application.Dashboard.DTOs;

public class SectionHeadDashboardSummaryDto
{
    public int StaffId { get; set; }

    public string StaffNumber { get; set; } = string.Empty;

    public string SectionHeadName { get; set; } = string.Empty;

    public int SectionId { get; set; }

    public string SectionName { get; set; } = string.Empty;

    public int TotalStudents { get; set; }

    public int TotalClasses { get; set; }

    public int TotalTeachers { get; set; }

    public int TodayTimetableCount { get; set; }

    public int TodaySpecialClassCount { get; set; }

    public int UpcomingSpecialClassCount { get; set; }

    public int PendingLeaveRequestCount { get; set; }

    public int UnreadNotificationCount { get; set; }

    public int ActiveAcademicYearId { get; set; }

    public string ActiveAcademicYearName { get; set; } = string.Empty;

    public int? ActiveAcademicTermId { get; set; }

    public string? ActiveAcademicTermName { get; set; }
}