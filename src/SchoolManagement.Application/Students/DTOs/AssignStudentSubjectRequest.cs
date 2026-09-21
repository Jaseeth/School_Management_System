namespace SchoolManagement.Application.Students.DTOs;

public class AssignStudentSubjectRequest
{
    public int StudentId { get; set; }

    public int AcademicYearId { get; set; }

    public int SubjectId { get; set; }
}
