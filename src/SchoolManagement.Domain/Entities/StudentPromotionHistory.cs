namespace SchoolManagement.Domain.Entities;

public class StudentPromotionHistory
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int FromAcademicYearId { get; set; }
    public AcademicYear FromAcademicYear { get; set; } = null!;

    public int ToAcademicYearId { get; set; }
    public AcademicYear ToAcademicYear { get; set; } = null!;

    public int FromSchoolClassId { get; set; }
    public SchoolClass FromSchoolClass { get; set; } = null!;

    public int? ToSchoolClassId { get; set; }
    public SchoolClass? ToSchoolClass { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public int ProcessedByStaffId { get; set; }
    public Staff ProcessedByStaff { get; set; } = null!;

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}