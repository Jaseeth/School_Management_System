namespace SchoolManagement.Application.Students.Import.DTOs;

public class ResolvedStudentImportRowDto
{
    public int RowNumber { get; set; }

    public string StudentIndexNumber { get; set; } =
        string.Empty;

    public string StudentFullName { get; set; } =
        string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public string? StudentEmail { get; set; }

    public string? StudentMobile { get; set; }

    public int? AcademicYearId { get; set; }

    public string? AcademicYearName { get; set; }

    public int? SchoolClassId { get; set; }

    public string? SectionName { get; set; }

    public string? GradeName { get; set; }

    public string? ClassName { get; set; }

    public List<ResolvedSubjectImportDto> Subjects { get; set; } =
        new();

    public ResolvedParentImportDto? Parent1 { get; set; }

    public ResolvedParentImportDto? Parent2 { get; set; }
}