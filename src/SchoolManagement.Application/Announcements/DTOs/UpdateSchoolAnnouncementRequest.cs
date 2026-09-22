namespace SchoolManagement.Application.Announcements.DTOs;

public class UpdateSchoolAnnouncementRequest
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime? PublishAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; }
}