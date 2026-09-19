namespace SchoolManagement.Application.ClassTeachers.DTOs;

public class AssignClassTeacherRequest
{
    public int AcademicYearId { get; set; }

    public int SchoolClassId { get; set; }

    public int StaffId { get; set; }
}