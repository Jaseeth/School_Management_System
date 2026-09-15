namespace SchoolManagement.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendOtpAsync(
        string email,
        string name,
        string otp);
}