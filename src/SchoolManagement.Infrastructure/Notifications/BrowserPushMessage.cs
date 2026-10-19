using FirebaseAdmin.Messaging;

namespace SchoolManagement.Infrastructure.Notifications;

public static class BrowserPushMessage
{
    public static Message Create(string token, string portal, string recipientUserId)
    {
        if (portal is not ("staff" or "student" or "parent") || string.IsNullOrWhiteSpace(recipientUserId)) throw new ArgumentException("Invalid browser recipient.");
        return new Message {
            Token = token,
            // No notification payload: Firebase must not bypass our worker's account binding check.
            Data = new Dictionary<string, string> { ["kind"] = "school-update", ["portal"] = portal, ["recipientUserId"] = recipientUserId },
            Webpush = new WebpushConfig { Headers = new Dictionary<string, string> { ["TTL"] = "3600", ["Urgency"] = "normal" } }
        };
    }
}
