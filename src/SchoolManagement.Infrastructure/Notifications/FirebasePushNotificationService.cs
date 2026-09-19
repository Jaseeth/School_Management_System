using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Notifications;

public class FirebasePushNotificationService
    : IPushNotificationService
{
    private readonly ApplicationDbContext _context;

    public FirebasePushNotificationService(
        ApplicationDbContext context,
        IOptions<FirebaseSettings> settings)
    {
        _context = context;

        var firebaseSettings = settings.Value;

        if (FirebaseApp.DefaultInstance == null)
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential =
                    GoogleCredential.FromFile(
                        firebaseSettings.CredentialsPath),

                ProjectId =
                    firebaseSettings.ProjectId
            });
        }
    }

    public async Task SendToStaffAsync(
        int staffId,
        string title,
        string message,
        string? referenceType = null,
        int? referenceId = null)
    {
        var tokens =
            await _context.StaffDeviceTokens
                .Where(x =>
                    x.StaffId == staffId &&
                    x.IsActive)
                .Select(x =>
                    x.Token)
                .ToListAsync();

        if (tokens.Count == 0)
        {
            return;
        }

        foreach (var token in tokens)
        {
            try
            {
                var firebaseMessage =
                    new Message
                    {
                        Token = token,

                        Notification =
                            new FirebaseAdmin.Messaging.Notification
                            {
                                Title = title,
                                Body = message
                            },

                        Data =
                            new Dictionary<string, string>
                            {
                                {
                                    "referenceType",
                                    referenceType ?? string.Empty
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
                    .SendAsync(firebaseMessage);
            }
            catch (FirebaseMessagingException)
            {
                // For now don't break the API if FCM fails.
                // Later we can deactivate invalid tokens.
            }
        }
    }
}