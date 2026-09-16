using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities;

public class MarksSubmission
{
    public int Id { get; set; }

    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;

    public int TeacherAssignmentId { get; set; }
    public TeacherAssignment TeacherAssignment { get; set; } = null!;

    public MarksSubmissionStatus Status { get; set; }
        = MarksSubmissionStatus.Draft;

    public int SubmittedByStaffId { get; set; }
    public Staff SubmittedByStaff { get; set; } = null!;

    public DateTime? SubmittedAt { get; set; }

    public int? ReviewedByStaffId { get; set; }
    public Staff? ReviewedByStaff { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewComment { get; set; }

    public int? PublishedByStaffId { get; set; }
    public Staff? PublishedByStaff { get; set; }

    public DateTime? PublishedAt { get; set; }
}