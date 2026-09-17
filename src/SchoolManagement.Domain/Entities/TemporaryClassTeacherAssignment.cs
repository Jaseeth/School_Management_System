namespace SchoolManagement.Domain.Entities;

public class TemporaryClassTeacherAssignment
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public int SchoolClassId { get; set; }
    public SchoolClass SchoolClass { get; set; } = null!;

    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;

    public int AssignedByStaffId { get; set; }
    public Staff AssignedByStaff { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; } = false;

    public DateTime? RevokedAt { get; set; }

    public int? RevokedByStaffId { get; set; }
    public Staff? RevokedByStaff { get; set; }

    public string? Reason { get; set; }
}