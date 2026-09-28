namespace SchoolManagement.Application.Staff.DTOs;

public class UpdateMyStaffProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CurrentPassword { get; set; }
}