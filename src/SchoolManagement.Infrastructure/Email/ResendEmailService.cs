using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Infrastructure.Persistence;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SchoolManagement.Infrastructure.Email;

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ResendSettings _resendSettings;
    private readonly ApplicationDbContext _context;

    public ResendEmailService(
        HttpClient httpClient,
        IOptions<ResendSettings> resendSettings,
        ApplicationDbContext context)
    {
        _httpClient = httpClient;
        _resendSettings = resendSettings.Value;
        _context = context;
    }

    public async Task SendOtpAsync(
        string email,
        string name,
        string otp)
    {
        if (string.IsNullOrWhiteSpace(_resendSettings.ApiKey))
        {
            throw new Exception("Resend API key is not configured.");
        }

        var emailSetting = await _context.EmailSettings
            .FirstOrDefaultAsync();

        if (emailSetting == null)
        {
            throw new Exception(
                "Email sender settings have not been configured.");
        }

        var requestBody = new
        {
            from = $"{emailSetting.SenderName} <{emailSetting.SenderEmail}>",

            to = new[]
            {
                email
            },

            subject = "School Management - Verification OTP",

            html = $"""
                <h2>Email Verification</h2>

                <p>Hello {name},</p>

                <p>Your verification OTP is:</p>

                <h1>{otp}</h1>

                <p>This OTP will expire in 5 minutes.</p>

                <p>Please do not share this OTP with anyone.</p>
                """
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.resend.com/emails");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _resendSettings.ApiKey);

        request.Content = JsonContent.Create(requestBody);

        var response = await _httpClient.SendAsync(request);

        var responseContent =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Unable to send email through Resend. " +
                $"Status: {(int)response.StatusCode}. " +
                $"Response: {responseContent}");
        }

        Console.WriteLine(
            $"Resend email sent successfully: {responseContent}");
    }
}