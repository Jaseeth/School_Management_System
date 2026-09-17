namespace SchoolManagement.Application.Authentication.DTOs;

public class StaffForgotPasswordRequest
{
    public string StaffNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}