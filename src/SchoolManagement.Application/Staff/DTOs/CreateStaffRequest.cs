namespace SchoolManagement.Application.Staff.DTOs;

public class CreateStaffRequest
{
    public string StaffNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Designation { get; set; }

    public string RoleId { get; set; } = string.Empty;

    public string TemporaryPassword { get; set; } = string.Empty;
}