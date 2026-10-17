using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using SchoolManagement.Infrastructure.Email;

namespace SchoolManagement.IntegrationTests;

public class MailpitEmailTests
{
    [Fact]
    public async Task DevelopmentOtpUsesStudentRecipientAndEscapesHtml()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var capture = CaptureAsync(listener, timeout.Token);
            var service = new MailpitEmailService(Options.Create(new MailpitSettings { Port = port }));
            await service.SendOtpAsync("student@example.test", "<script>student</script>", "123456");
            var rawMessage = await capture;
            var message = System.Text.RegularExpressions.Regex.Replace(rawMessage, "=\\r?\\n", "");
            message = System.Text.RegularExpressions.Regex.Replace(message, "=([0-9A-Fa-f]{2})",
                match => ((char)Convert.ToByte(match.Groups[1].Value, 16)).ToString());
            Assert.Contains("student@example.test", message);
            Assert.Contains("123456", message);
            Assert.Contains("&lt;script&gt;student&lt;/script&gt;", message);
            Assert.DoesNotContain("<script>student</script>", message);
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task DevelopmentEmailRejectsNonLocalSmtpHost()
    {
        var service = new MailpitEmailService(Options.Create(new MailpitSettings { Host = "smtp.example.test" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendOtpAsync("student@example.test", "Student", "123456"));
    }

    private static async Task<string> CaptureAsync(TcpListener listener, CancellationToken token)
    {
        using var client = await listener.AcceptTcpClientAsync(token);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
        await writer.WriteLineAsync("220 localhost test inbox");
        var message = new StringBuilder();
        while (await reader.ReadLineAsync(token) is { } line)
        {
            if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 Send message");
                while (await reader.ReadLineAsync(token) is { } body && body != ".") message.AppendLine(body);
                await writer.WriteLineAsync("250 Message captured");
            }
            else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 Goodbye");
                break;
            }
            else { message.AppendLine(line); await writer.WriteLineAsync("250 OK"); }
        }
        return message.ToString();
    }
}
