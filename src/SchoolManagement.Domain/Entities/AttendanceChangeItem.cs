using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities;

public class AttendanceChangeItem
{
    public int Id { get; set; }

    public int AttendanceChangeRequestId { get; set; }
    public AttendanceChangeRequest Request { get; set; } = null!;

    public int StudentId { get; set; }
    public string StudentIndexNumber { get; set; } = string.Empty;
    public string StudentFullName { get; set; } = string.Empty;

    public AttendanceStatus ProposedStatus { get; set; }
    public string? ProposedRemarks { get; set; }

    public AttendanceStatus? PreviousStatus { get; set; }
    public string? PreviousRemarks { get; set; }
}