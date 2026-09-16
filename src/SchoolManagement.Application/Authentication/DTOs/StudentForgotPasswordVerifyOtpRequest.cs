namespace SchoolManagement.Application.Authentication.DTOs;

public class StudentForgotPasswordVerifyOtpRequest
{
    public string IndexNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Otp { get; set; } = string.Empty;
}