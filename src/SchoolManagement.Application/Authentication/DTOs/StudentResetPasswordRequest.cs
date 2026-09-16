namespace SchoolManagement.Application.Authentication.DTOs;

public class StudentResetPasswordRequest
{
    public string IndexNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}