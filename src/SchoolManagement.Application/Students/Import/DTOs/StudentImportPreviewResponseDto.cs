namespace SchoolManagement.Application.Students.Import.DTOs;

public class StudentImportPreviewResponseDto
{
    public StudentImportPreviewDto Validation { get; set; } =
        new();

    public List<ResolvedStudentImportRowDto> ResolvedRows { get; set; } =
        new();
}