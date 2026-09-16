namespace SchoolManagement.Application.Marks.DTOs;

public class SaveMarksRequest
{
    public int ExamId { get; set; }

    public int TeacherAssignmentId { get; set; }

    public List<SaveStudentMarkRequest> Marks { get; set; } = new();
}