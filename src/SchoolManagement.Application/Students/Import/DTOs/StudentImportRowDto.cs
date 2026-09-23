namespace SchoolManagement.Application.Students.Import.DTOs;

public class StudentImportRowDto
{
    public int RowNumber { get; set; }

    public string StudentIndexNumber { get; set; } = string.Empty;

    public string StudentFullName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public string? StudentEmail { get; set; }

    public string? StudentMobile { get; set; }

    public string? AcademicYear { get; set; }

    public string? Section { get; set; }

    public string? Grade { get; set; }

    public string? Class { get; set; }

    public List<string> Subjects { get; set; } =
        new();

    public ParentImportDto? Parent1 { get; set; }

    public ParentImportDto? Parent2 { get; set; }
}