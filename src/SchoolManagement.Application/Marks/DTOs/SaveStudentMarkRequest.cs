namespace SchoolManagement.Application.Marks.DTOs;

public class SaveStudentMarkRequest
{
    public int StudentId { get; set; }

    public decimal MarksObtained { get; set; }
}