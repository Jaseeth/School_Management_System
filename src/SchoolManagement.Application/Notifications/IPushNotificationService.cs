namespace SchoolManagement.Application.Notifications;

public interface IPushNotificationService
{
    Task SendToStaffAsync(
        int staffId,
        string title,
        string message,
        string? referenceType = null,
        int? referenceId = null);

    Task SendToStudentAsync(
        int studentId,
        string title,
        string message,
        string? referenceType = null,
        int? referenceId = null);

    Task SendToParentAsync(
        int parentGuardianId,
        string title,
        string message,
        string? referenceType = null,
        int? referenceId = null);
}