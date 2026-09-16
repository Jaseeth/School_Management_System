namespace SchoolManagement.Domain.Entities;

public class StudentMark
{
    public int Id { get; set; }

    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int TeacherAssignmentId { get; set; }
    public TeacherAssignment TeacherAssignment { get; set; } = null!;

    public decimal MarksObtained { get; set; }

    public bool IsSubmitted { get; set; } = false;

    public DateTime? SubmittedAt { get; set; }

    public bool IsPublished { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}