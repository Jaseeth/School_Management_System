namespace SchoolManagement.Application.Notifications;

public interface IPushNotificationService
{
    Task SendToStaffAsync(
        int staffId,
        string title,
        string message,
        string? referenceType = null,
        int? referenceId = null);
}