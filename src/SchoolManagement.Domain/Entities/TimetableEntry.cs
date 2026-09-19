using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities;

public class TimetableEntry
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public int AcademicTermId { get; set; }
    public AcademicTerm AcademicTerm { get; set; } = null!;

    public int SchoolClassId { get; set; }
    public SchoolClass SchoolClass { get; set; } = null!;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;

    public SchoolDay Day { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Room { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int CreatedByStaffId { get; set; }
    public Staff CreatedByStaff { get; set; } = null!;
}