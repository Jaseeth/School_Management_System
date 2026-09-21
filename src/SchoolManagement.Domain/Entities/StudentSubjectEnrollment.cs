namespace SchoolManagement.Domain.Entities;

public class StudentSubjectEnrollment
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    public int EnrolledByStaffId { get; set; }
    public Staff EnrolledByStaff { get; set; } = null!;
}