namespace SchoolManagement.Application.Notifications;

public interface IBrowserPushSender
{
    Task<bool> SendBrowserTestAsync(string token, string portal, string recipientUserId);
}
