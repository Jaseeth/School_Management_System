namespace SchoolManagement.Application.Students.DTOs;

public class UpdateMyStudentProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? CurrentPassword { get; set; }
}