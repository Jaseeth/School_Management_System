namespace SchoolManagement.Domain.Entities;

public class SectionHeadAssignment
{
    public int Id { get; set; }

    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;

    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}