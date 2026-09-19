using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities;

public class SpecialClassSession
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public int? AcademicTermId { get; set; }
    public AcademicTerm? AcademicTerm { get; set; }

    public int SchoolClassId { get; set; }
    public SchoolClass SchoolClass { get; set; } = null!;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;

    public DateOnly ClassDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Room { get; set; }

    public string? Reason { get; set; }

    public string? Remarks { get; set; }

    public SpecialClassStatus Status { get; set; }

    public int CreatedByStaffId { get; set; }
    public Staff CreatedByStaff { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? ReviewedByStaffId { get; set; }
    public Staff? ReviewedByStaff { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewRemarks { get; set; }

    public DateTime? CancelledAt { get; set; }

    public int? CancelledByStaffId { get; set; }
    public Staff? CancelledByStaff { get; set; }

    public bool IsActive { get; set; } = true;
}