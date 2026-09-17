using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities;

public class StaffLeaveRequest
{
    public int Id { get; set; }

    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;

    public StaffLeaveType LeaveType { get; set; }

    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public StaffLeaveStatus Status { get; set; }
        = StaffLeaveStatus.Pending;

    public DateTime RequestedAt { get; set; }
        = DateTime.UtcNow;

    public int? ReviewedByStaffId { get; set; }

    public Staff? ReviewedByStaff { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewRemarks { get; set; }

    public DateTime? CancelledAt { get; set; }
}