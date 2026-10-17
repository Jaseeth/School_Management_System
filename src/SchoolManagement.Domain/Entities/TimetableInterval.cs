using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities;

public class TimetableInterval
{
    public int Id { get; set; }
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public int AcademicTermId { get; set; }
    public AcademicTerm AcademicTerm { get; set; } = null!;
    public SchoolDay Day { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsActive { get; set; } = true;
}
