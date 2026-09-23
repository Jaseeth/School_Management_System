namespace SchoolManagement.Application.Students.Import.DTOs;

public class StudentImportPreviewDto
{
    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public int InvalidRows { get; set; }

    public int WarningRows { get; set; }

    public List<StudentImportRowResultDto> Rows { get; set; } =
        new();
}