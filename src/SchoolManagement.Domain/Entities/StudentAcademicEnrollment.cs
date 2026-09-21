namespace SchoolManagement.Domain.Entities;

public class StudentAcademicEnrollment
{
    public int Id { get; set; }


    // Student
    public int StudentId { get; set; }

    public Student Student { get; set; } = null!;


    // Academic Year
    public int AcademicYearId { get; set; }

    public AcademicYear AcademicYear { get; set; } = null!;


    // Class for this academic year
    public int SchoolClassId { get; set; }

    public SchoolClass SchoolClass { get; set; } = null!;


    // Date student was enrolled/promoted into this class
    public DateOnly EnrollmentDate { get; set; }


    // Current academic enrollment
    public bool IsCurrent { get; set; } = true;


    public bool IsActive { get; set; } = true;


    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;


    // Admin / authorized staff who created enrollment
    public int CreatedByStaffId { get; set; }

    public Staff CreatedByStaff { get; set; } = null!;
}