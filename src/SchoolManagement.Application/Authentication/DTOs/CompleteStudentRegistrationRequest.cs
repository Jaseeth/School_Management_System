namespace SchoolManagement.Application.Authentication.DTOs;

public class CompleteStudentRegistrationRequest
{
    public string IndexNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}