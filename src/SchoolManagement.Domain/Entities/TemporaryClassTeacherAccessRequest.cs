using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities;

public class TemporaryClassTeacherAccessRequest
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public int SchoolClassId { get; set; }
    public SchoolClass SchoolClass { get; set; } = null!;

    public int RequestedByStaffId { get; set; }
    public Staff RequestedByStaff { get; set; } = null!;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public string? Reason { get; set; }

    public TemporaryClassTeacherRequestStatus Status { get; set; }
        = TemporaryClassTeacherRequestStatus.Pending;

    public int? ReviewedByStaffId { get; set; }
    public Staff? ReviewedByStaff { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewRemarks { get; set; }

    public int? TemporaryClassTeacherAssignmentId { get; set; }

    public TemporaryClassTeacherAssignment?
        TemporaryClassTeacherAssignment
    { get; set; }
}