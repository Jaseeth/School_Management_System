namespace SchoolManagement.Application.Students.DTOs;

public class StudentSubjectEnrollmentDto
{
    public int EnrollmentId { get; set; }

    public int SubjectId { get; set; }

    public string SubjectName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime EnrolledAt { get; set; }

    public int EnrolledByStaffId { get; set; }

    public string EnrolledByStaffName { get; set; } = string.Empty;
}