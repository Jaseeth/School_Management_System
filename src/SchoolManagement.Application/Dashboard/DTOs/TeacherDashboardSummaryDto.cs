namespace SchoolManagement.Application.Dashboard.DTOs;

public class TeacherDashboardSummaryDto
{
    public int StaffId { get; set; }

    public string StaffNumber { get; set; } = string.Empty;

    public string TeacherName { get; set; } = string.Empty;

    public int AssignedClassCount { get; set; }

    public int AssignedSubjectCount { get; set; }

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