namespace SchoolManagement.Application.Students.Export.DTOs;

public class StudentExportRowDto
{
    public string StudentIndexNumber { get; set; } =
        string.Empty;

    public string StudentFullName { get; set; } =
        string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public string AcademicYear { get; set; } =
        string.Empty;

    public string Section { get; set; } =
        string.Empty;

    public string Grade { get; set; } =
        string.Empty;

    public string Class { get; set; } =
        string.Empty;

    public string Subjects { get; set; } =
        string.Empty;

    public bool IsActive { get; set; }

    public bool IsGraduated { get; set; }

    public string? Parent1Number { get; set; }

    public string? Parent1FullName { get; set; }

    public string? Parent1Relationship { get; set; }

    public string? Parent1Email { get; set; }

    public string? Parent1Mobile { get; set; }

    public bool? Parent1Primary { get; set; }

    public bool? Parent1Emergency { get; set; }

    public string? Parent2Number { get; set; }

    public string? Parent2FullName { get; set; }

    public string? Parent2Relationship { get; set; }

    public string? Parent2Email { get; set; }

    public string? Parent2Mobile { get; set; }

    public bool? Parent2Primary { get; set; }

    public bool? Parent2Emergency { get; set; }
}