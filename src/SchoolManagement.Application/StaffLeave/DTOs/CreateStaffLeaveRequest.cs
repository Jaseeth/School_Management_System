using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.StaffLeave.DTOs;

public class CreateStaffLeaveRequest
{
    public StaffLeaveType LeaveType { get; set; }

    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public string Reason { get; set; } = string.Empty;
}