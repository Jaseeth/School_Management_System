namespace SchoolManagement.Application.Students.DTOs;

public class CreateStudentRequest
{
    public string IndexNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public int SchoolClassId { get; set; }
}