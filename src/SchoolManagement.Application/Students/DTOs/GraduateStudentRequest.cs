namespace SchoolManagement.Application.Students.DTOs;

public class GraduateStudentRequest
{
    public int StudentId { get; set; }

    public DateOnly GraduationDate { get; set; }

    public string? Reason { get; set; }
}