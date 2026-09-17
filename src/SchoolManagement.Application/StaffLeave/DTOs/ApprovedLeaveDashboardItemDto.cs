namespace SchoolManagement.Application.StaffLeave.DTOs;

public class ApprovedLeaveDashboardItemDto
{
    public int LeaveRequestId { get; set; }

    public int StaffId { get; set; }

    public string StaffNumber { get; set; } = string.Empty;

    public string StaffName { get; set; } = string.Empty;

    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public int? SchoolClassId { get; set; }

    public string? ClassName { get; set; }

    public string? GradeName { get; set; }

    public string? SectionName { get; set; }

    public bool HasPermanentClassTeacherAssignment { get; set; }

    public bool HasActiveTemporaryTeacher { get; set; }

    public int? TemporaryTeacherStaffId { get; set; }

    public string? TemporaryTeacherName { get; set; }

    public DateTime? TemporaryAccessExpiresAt { get; set; }
}