namespace SchoolManagement.Domain.Entities;

public class AcademicTerm
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int AcademicYearId { get; set; }

    public AcademicYear AcademicYear { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;
}