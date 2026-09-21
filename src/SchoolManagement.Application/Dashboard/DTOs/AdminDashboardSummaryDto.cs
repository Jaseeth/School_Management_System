namespace SchoolManagement.Application.Dashboard.DTOs;

public class AdminDashboardSummaryDto
{
    public int TotalStudents { get; set; }

    public int TotalStaff { get; set; }

    public int TotalSections { get; set; }

    public int TotalGrades { get; set; }

    public int TotalClasses { get; set; }

    public int TotalSubjects { get; set; }

    public int TodayTimetableCount { get; set; }

    public int TodaySpecialClassCount { get; set; }

    public int UpcomingSpecialClassCount { get; set; }

    public int PendingLeaveRequestCount { get; set; }

    public int ActiveAcademicYearId { get; set; }

    public string ActiveAcademicYearName { get; set; } = string.Empty;

    public int? ActiveAcademicTermId { get; set; }

    public string? ActiveAcademicTermName { get; set; }
}