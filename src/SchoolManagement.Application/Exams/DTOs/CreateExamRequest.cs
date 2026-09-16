namespace SchoolManagement.Application.Exams.DTOs;

public class CreateExamRequest
{
    public string Name { get; set; } = string.Empty;

    public int AcademicTermId { get; set; }

    public DateTime ExamDate { get; set; }

    public decimal MaximumMarks { get; set; }
}