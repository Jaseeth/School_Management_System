namespace SchoolManagement.Application.Authentication.DTOs;

public class RequestStudentRegistrationEmailOtpRequest
{
    public string IndexNumber { get; set; } =
        string.Empty;

    public string RegistrationCode { get; set; } =
        string.Empty;
}