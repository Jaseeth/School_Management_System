namespace SchoolManagement.Application.Authentication.DTOs;

public class StudentRegistrationStartRequest
{
    public string IndexNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}