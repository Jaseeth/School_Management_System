namespace SchoolManagement.Domain.Entities;

public class Student
{
    public int Id { get; set; }

    public string IndexNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public int SchoolClassId { get; set; }

    public SchoolClass SchoolClass { get; set; } = null!;

    public string? ApplicationUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsGraduated { get; set; } = false;

    public DateOnly? GraduationDate { get; set; }

    public int? GraduationAcademicYearId { get; set; }

    public AcademicYear? GraduationAcademicYear { get; set; }
}