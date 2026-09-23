namespace SchoolManagement.Application.Students.Import.DTOs;

public class StudentImportRowResultDto
{
    public int RowNumber { get; set; }

    public string? StudentIndexNumber { get; set; }

    public string? StudentFullName { get; set; }

    public bool IsValid { get; set; }

    public List<string> Errors { get; set; } =
        new();

    public List<string> Warnings { get; set; } =
        new();

    public StudentImportRowDto? Data { get; set; }
}