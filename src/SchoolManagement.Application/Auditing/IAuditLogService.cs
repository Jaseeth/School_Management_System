namespace SchoolManagement.Application.Auditing;

public interface IAuditLogService
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId = null,
        string? description = null,
        object? oldValues = null,
        object? newValues = null);
}