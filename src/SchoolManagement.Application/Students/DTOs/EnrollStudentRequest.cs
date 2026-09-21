namespace SchoolManagement.Application.Students.DTOs;

public class EnrollStudentRequest
{
    public int StudentId { get; set; }

    public int AcademicYearId { get; set; }

    public int SchoolClassId { get; set; }

    public DateOnly EnrollmentDate { get; set; }
}