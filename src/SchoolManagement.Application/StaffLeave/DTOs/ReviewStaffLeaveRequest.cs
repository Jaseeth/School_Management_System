namespace SchoolManagement.Application.StaffLeave.DTOs;

public class ReviewStaffLeaveRequest
{
    public bool Approve { get; set; }

    public string? Remarks { get; set; }
}