namespace SchoolManagement.Application.Announcements.DTOs;

public class CreateSchoolAnnouncementRequest
{
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