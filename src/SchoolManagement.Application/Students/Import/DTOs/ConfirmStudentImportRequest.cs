namespace SchoolManagement.Application.Students.Import.DTOs;

public class ConfirmStudentImportRequest
{
    public List<StudentImportRowDto> Rows { get; set; } =
        new();
}