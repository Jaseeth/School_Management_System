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
    : IPushNotificationService
{
    private readonly ApplicationDbContext _context;

    private readonly ILogger<FirebasePushNotificationService>
        _logger;

    public FirebasePushNotificationService(
        ApplicationDbContext context,
        IOptions<FirebaseSettings> settings,
        ILogger<FirebasePushNotificationService> logger)
    {
        _context = context;
        _logger = logger;

        var firebaseSettings =
            settings.Value;

        if (FirebaseApp.DefaultInstance == null)
        {
            FirebaseApp.Create(
                new AppOptions
                {
                    Credential =
                        GoogleCredential.FromFile(
                            firebaseSettings.CredentialsPath),

                    ProjectId =
                        firebaseSettings.ProjectId
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
                    referenceId);

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
                    referenceId);

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
    // COMMON FIREBASE SEND METHOD
    // ============================================================

    private async Task<FirebaseSendResult>
        SendFirebaseMessageAsync(
            string token,
            string title,
            string message,
            string? referenceType,
            int? referenceId)
    {
        try
        {
            var firebaseMessage =
                new Message
                {
                    Token =
                        token,

                    Notification =
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