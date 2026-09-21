namespace SchoolManagement.Application.Students.DTOs;

public class BulkAssignStudentSubjectRequest
{
    public List<int> StudentIds { get; set; } = new();

    public int AcademicYearId { get; set; }

    public int SubjectId { get; set; }
}