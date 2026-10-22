using FirebaseAdmin.Messaging;

namespace SchoolManagement.Infrastructure.Notifications;

public static class BrowserPushMessage
{
    public static Message Create(string token, string portal, string recipientUserId, bool sos = false, int? referenceId = null)
    {
        if (portal is not ("staff" or "student" or "parent") || string.IsNullOrWhiteSpace(recipientUserId)) throw new ArgumentException("Invalid browser recipient.");
        var data = new Dictionary<string, string> { ["kind"] = sos ? "school-sos" : "school-update", ["portal"] = portal, ["recipientUserId"] = recipientUserId };
        if (sos) data["alertId"] = referenceId?.ToString() ?? "";
        return new Message {
            Token = token,
            // No notification payload: Firebase must not bypass our worker's account binding check.
            Data = data,
            Webpush = new WebpushConfig { Headers = new Dictionary<string, string> { ["TTL"] = "3600", ["Urgency"] = sos ? "high" : "normal" } }
        };
    }
}
