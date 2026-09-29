using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities;

public class AttendanceChangeRequest
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }
    public int SchoolClassId { get; set; }
    public DateTime AttendanceDate { get; set; }

    public int RequestedByStaffId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsWholeClass { get; set; }

    public AttendanceChangeStatus Status { get; set; }
        = AttendanceChangeStatus.Pending;

    public DateTime RequestedAtUtc { get; set; }
        = DateTime.UtcNow;

    public int? ReviewedByStaffId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewRemarks { get; set; }

    public ICollection<AttendanceChangeItem> Items { get; set; }
        = new List<AttendanceChangeItem>();
}