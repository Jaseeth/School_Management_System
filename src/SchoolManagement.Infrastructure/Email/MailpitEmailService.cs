using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Common.Interfaces;

namespace SchoolManagement.Infrastructure.Email;

public sealed class MailpitSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 1025;
    public string SenderEmail { get; set; } = "noreply@school.test";
    public string SenderName { get; set; } = "School Management (Development)";
}

public sealed class MailpitEmailService(IOptions<MailpitSettings> options) : IEmailService
{
    public async Task SendOtpAsync(string email, string name, string otp)
    {
        var settings = options.Value;
        if (settings.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Development Mailpit must use a loopback host.");
        using var message = new MailMessage
        {
            From = new MailAddress(settings.SenderEmail, settings.SenderName),
            Subject = "School Management - Verification OTP",
            IsBodyHtml = true,
            Body = $"<h2>Email Verification</h2><p>Hello {WebUtility.HtmlEncode(name)},</p>" +
                $"<p>Your verification OTP is:</p><h1>{WebUtility.HtmlEncode(otp)}</h1>" +
                "<p>This OTP will expire in 5 minutes. Please do not share it.</p>"
        };
        message.To.Add(new MailAddress(email));
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = false,
            UseDefaultCredentials = false,
            Timeout = 10000
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await client.SendMailAsync(message, timeout.Token); }
        catch (Exception exception) when (exception is SmtpException or OperationCanceledException)
        {
            throw new InvalidOperationException("Unable to reach the development email inbox. Run scripts/start-mailpit.ps1 and retry.", exception);
        }
    }
}
