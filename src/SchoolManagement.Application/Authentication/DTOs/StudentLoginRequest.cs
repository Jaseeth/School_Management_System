namespace SchoolManagement.Application.Authentication.DTOs;

public class StudentLoginRequest
{
    public string IndexNumber { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}