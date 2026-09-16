namespace SchoolManagement.Domain.Entities;

public class TeacherAssignment
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;

    public int SchoolClassId { get; set; }
    public SchoolClass SchoolClass { get; set; } = null!;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}