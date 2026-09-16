namespace SchoolManagement.Application.Authentication.DTOs;

public class StudentForgotPasswordRequest
{
    public string IndexNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}