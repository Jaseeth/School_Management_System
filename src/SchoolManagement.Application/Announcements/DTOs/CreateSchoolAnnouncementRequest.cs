namespace SchoolManagement.Application.Announcements.DTOs;

public class CreateSchoolAnnouncementRequest
{
    public List<int> StudentIds { get; set; } = [];
    public List<int> StaffIds { get; set; } = [];
    public List<int> ParentIds { get; set; } = [];

    public int? TeacherAssignmentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string AudienceType { get; set; } = string.Empty;

    public int? SectionId { get; set; }

    public int? GradeId { get; set; }

    public int? SchoolClassId { get; set; }

    public string? RoleName { get; set; }

    public DateTime? PublishAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
}