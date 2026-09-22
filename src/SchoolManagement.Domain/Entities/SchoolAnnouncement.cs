namespace SchoolManagement.Domain.Entities;

public class SchoolAnnouncement
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string AudienceType { get; set; } = string.Empty;

    public int? SectionId { get; set; }

    public Section? Section { get; set; }

    public int? GradeId { get; set; }

    public Grade? Grade { get; set; }

    public int? SchoolClassId { get; set; }

    public SchoolClass? SchoolClass { get; set; }

    public string? RoleName { get; set; }

    public DateTime PublishAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public int CreatedByStaffId { get; set; }

    public Staff CreatedByStaff { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}