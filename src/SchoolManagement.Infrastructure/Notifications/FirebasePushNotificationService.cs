using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Notifications;

public class FirebasePushNotificationService
    : IPushNotificationService, IBrowserPushSender
{
    private readonly ApplicationDbContext _context;
    private readonly FirebaseSettings _settings;
    private static readonly object InitializationLock = new();

    private readonly ILogger<FirebasePushNotificationService>
        _logger;

    public FirebasePushNotificationService(
        ApplicationDbContext context,
        IOptions<FirebaseSettings> settings,
        ILogger<FirebasePushNotificationService> logger)
    {
        _context = context;
        _logger = logger;

        _settings = settings.Value;
    }

    private void EnsureFirebaseInitialized()
    {
        lock (InitializationLock)
        {
            if (FirebaseApp.DefaultInstance != null) return;
            FirebaseApp.Create(
                new AppOptions
                {
                    Credential =
                        GoogleCredential.FromFile(
                            _settings.CredentialsPath),

                    ProjectId =
                        _settings.ProjectId
                });
        }
    }


    // ============================================================
    // SEND PUSH TO STAFF
    // ============================================================

    public async Task SendToStaffAsync(
        int staffId,
        string title,
        string message,
        string? referenceType = null,
        int? referenceId = null)
    {
        var recipientUserId = await _context.Staff
            .Where(s => s.Id == staffId && s.IsActive && _context.Users.Any(u => u.Id == s.ApplicationUserId && u.IsActive))
            .Select(s => s.ApplicationUserId).FirstOrDefaultAsync();
        if (recipientUserId == null) return;

        var deviceTokens =
            await _context.StaffDeviceTokens
                .Where(x =>
                    x.StaffId == staffId &&
                    x.IsActive)
                .ToListAsync();

        if (deviceTokens.Count == 0)
        {
            return;
        }

        foreach (var deviceToken in deviceTokens)
        {
            var result =
                await SendFirebaseMessageAsync(
                    deviceToken.Token,
                    title,
                    message,
                    referenceType,
                    referenceId, deviceToken.Platform == SchoolManagement.Domain.Enums.DevicePlatform.Web ? "staff" : null, recipientUserId);

            if (result.ShouldDeactivateToken)
            {
                deviceToken.IsActive = false;
                deviceToken.UpdatedAt =
                    DateTime.UtcNow;
            }
            else if (result.Success)
            {
                deviceToken.LastUsedAt =
                    DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }


    // ============================================================
    // SEND PUSH TO STUDENT
    // ============================================================

    public async Task SendToStudentAsync(
        int studentId,
        string title,
        string message,
        string? referenceType = null,
        int? referenceId = null)
    {
        var recipientUserId = await _context.Students
            .Where(s => s.Id == studentId && s.IsActive && _context.Users.Any(u => u.Id == s.ApplicationUserId && u.IsActive))
            .Select(s => s.ApplicationUserId).FirstOrDefaultAsync();
        if (recipientUserId == null) return;

        var deviceTokens =
            await _context.StudentDeviceTokens
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsActive)
                .ToListAsync();

        if (deviceTokens.Count == 0)
        {
            return;
        }

        foreach (var deviceToken in deviceTokens)
        {
            var result =
                await SendFirebaseMessageAsync(
                    deviceToken.Token,
                    title,
                    message,
                    referenceType,
                    referenceId, deviceToken.Platform == SchoolManagement.Domain.Enums.DevicePlatform.Web ? "student" : null, recipientUserId);

            if (result.ShouldDeactivateToken)
            {
                deviceToken.IsActive = false;
                deviceToken.UpdatedAt =
                    DateTime.UtcNow;
            }
            else if (result.Success)
            {
                deviceToken.LastUsedAt =
                    DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    // ============================================================
    // SEND PUSH TO PARENT
    // ============================================================

    public async Task SendToParentAsync(
        int parentGuardianId,
        string title,
        string message,
        string? referenceType = null,
        int? referenceId = null)
    {
        var recipientUserId = await _context.ParentGuardians
            .Where(s => s.Id == parentGuardianId && s.IsActive && _context.Users.Any(u => u.Id == s.ApplicationUserId && u.IsActive))
            .Select(s => s.ApplicationUserId).FirstOrDefaultAsync();
        if (recipientUserId == null) return;

        var deviceTokens =
            await _context.ParentDeviceTokens
                .Where(x =>
                    x.ParentGuardianId ==
                        parentGuardianId &&
                    x.IsActive)
                .ToListAsync();

        if (deviceTokens.Count == 0)
        {
            return;
        }

        foreach (var deviceToken in deviceTokens)
        {
            var result =
                await SendFirebaseMessageAsync(
                    deviceToken.Token,
                    title,
                    message,
                    referenceType,
                    referenceId, deviceToken.IsBrowser ? "parent" : null, recipientUserId);

            if (result.ShouldDeactivateToken)
            {
                deviceToken.IsActive =
                    false;

                deviceToken.UpdatedAt =
                    DateTime.UtcNow;
            }
            else if (result.Success)
            {
                deviceToken.LastUsedAt =
                    DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }


    public async Task<bool> SendBrowserTestAsync(string token, string portal, string recipientUserId)
        => (await SendFirebaseMessageAsync(token, "School Management", "Browser alert test", null, null, portal, recipientUserId)).Success;

    // ============================================================
    // COMMON FIREBASE SEND METHOD
    // ============================================================

    private async Task<FirebaseSendResult>
        SendFirebaseMessageAsync(
            string token,
            string title,
            string message,
            string? referenceType,
            int? referenceId, string? browserPortal = null, string? recipientUserId = null)
    {
        try
        {
            EnsureFirebaseInitialized();
            var firebaseMessage =
                new Message
                {
                    Token =
                        token,

                    Notification = browserPortal != null ? null :
                        new FirebaseAdmin.Messaging.Notification
                        {
                            Title =
                                title,

                            Body =
                                message
                        },

                    Data =
                        new Dictionary<string, string>
                        {
                            {
                                "referenceType",
                                referenceType
                                    ?? string.Empty
                            },
                            {
                                "referenceId",
                                referenceId?.ToString()
                                    ?? string.Empty
                            }
                        }
                };

            if (browserPortal != null) {
                firebaseMessage = BrowserPushMessage.Create(token, browserPortal, recipientUserId ?? "", referenceType == "SchoolSos", referenceId);
            }

            await FirebaseMessaging
                .DefaultInstance
                .SendAsync(
                    firebaseMessage);

            return new FirebaseSendResult
            {
                Success = true
            };
        }
        catch (FirebaseMessagingException ex)
        {
            var shouldDeactivate =
                ex.MessagingErrorCode ==
                    MessagingErrorCode.Unregistered ||
                ex.MessagingErrorCode ==
                    MessagingErrorCode.SenderIdMismatch;

            if (shouldDeactivate)
            {
                _logger.LogWarning(
                    "Firebase device token was deactivated. Error: {MessagingErrorCode}",
                    ex.MessagingErrorCode);
            }
            else
            {
                _logger.LogError(
                    ex,
                    "Firebase push notification failed. Error: {MessagingErrorCode}",
                    ex.MessagingErrorCode);
            }

            return new FirebaseSendResult
            {
                Success = false,
                ShouldDeactivateToken =
                    shouldDeactivate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while sending Firebase push notification.");

            return new FirebaseSendResult
            {
                Success = false,
                ShouldDeactivateToken = false
            };
        }
    }


    // ============================================================
    // INTERNAL RESULT
    // ============================================================

    private sealed class FirebaseSendResult
    {
        public bool Success { get; set; }

        public bool ShouldDeactivateToken { get; set; }
    }
}
