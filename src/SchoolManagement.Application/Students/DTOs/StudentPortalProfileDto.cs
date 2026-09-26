namespace SchoolManagement.Application.Students.DTOs;

public class StudentPortalProfileDto
{
    public int Id { get; set; }

    public string IndexNumber { get; set; } =
        string.Empty;

    public string FullName { get; set; } =
        string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public string? Email { get; set; }

    public string? Mobile { get; set; }

    public bool IsActive { get; set; }

    public bool IsGraduated { get; set; }

    public DateOnly? GraduationDate { get; set; }

    public StudentPortalCurrentEnrollmentDto?
        CurrentEnrollment
    { get; set; }
}


public class StudentPortalCurrentEnrollmentDto
{
    public int EnrollmentId { get; set; }

    public int AcademicYearId { get; set; }

    public string AcademicYear { get; set; } =
        string.Empty;

    public int SchoolClassId { get; set; }

    public string Class { get; set; } =
        string.Empty;

    public int GradeId { get; set; }

    public string Grade { get; set; } =
        string.Empty;

    public int SectionId { get; set; }

    public string Section { get; set; } =
        string.Empty;

    public DateOnly EnrollmentDate { get; set; }
}