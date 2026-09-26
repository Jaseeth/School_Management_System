namespace SchoolManagement.Application.Authentication.DTOs;

public class CompleteStudentRegistrationWithCodeRequest
{
    public string IndexNumber { get; set; } =
        string.Empty;

    public string RegistrationCode { get; set; } =
        string.Empty;

    public string Password { get; set; } =
        string.Empty;

    public string ConfirmPassword { get; set; } =
        string.Empty;
}