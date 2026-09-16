namespace SchoolManagement.Application.TeacherAssignments.DTOs;

public class CreateTeacherAssignmentRequest
{
    public int AcademicYearId { get; set; }

    public int StaffId { get; set; }

    public int SchoolClassId { get; set; }

    public int SubjectId { get; set; }
}