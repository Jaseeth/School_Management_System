namespace SchoolManagement.Application.Students.DTOs;

public class StudentEnrollmentHistoryDto
{
    public int EnrollmentId { get; set; }

    public int AcademicYearId { get; set; }

    public string AcademicYearName { get; set; } = string.Empty;

    public int SchoolClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string GradeName { get; set; } = string.Empty;

    public string SectionName { get; set; } = string.Empty;

    public DateOnly EnrollmentDate { get; set; }

    public bool IsCurrent { get; set; }

    public bool IsActive { get; set; }

    public int CreatedByStaffId { get; set; }

    public string CreatedByStaffName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}