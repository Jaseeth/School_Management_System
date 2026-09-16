namespace SchoolManagement.Domain.Entities;

public class Exam
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int AcademicTermId { get; set; }

    public AcademicTerm AcademicTerm { get; set; } = null!;

    public DateTime ExamDate { get; set; }

    public decimal MaximumMarks { get; set; }

    public bool IsActive { get; set; } = true;
}