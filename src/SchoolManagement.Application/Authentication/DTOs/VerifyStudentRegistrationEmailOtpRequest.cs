namespace SchoolManagement.Application.Authentication.DTOs;

public class VerifyStudentRegistrationEmailOtpRequest
{
    public string IndexNumber { get; set; } =
        string.Empty;

    public string RegistrationCode { get; set; } =
        string.Empty;

    public string Otp { get; set; } =
        string.Empty;
}