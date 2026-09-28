namespace SchoolManagement.Application.Parents.DTOs;

public class UpdateMyParentProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? CurrentPassword { get; set; }
}