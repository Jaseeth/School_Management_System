using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Auditing;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Auditing;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId = null,
        string? description = null,
        object? oldValues = null,
        object? newValues = null)
    {
        var httpContext =
            _httpContextAccessor.HttpContext;

        var userId =
            httpContext?.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        int? staffId = null;
        int? studentId = null;
        int? parentGuardianId = null;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            staffId =
                await _context.Staff
                    .Where(x =>
                        x.ApplicationUserId == userId)
                    .Select(x =>
                        (int?)x.Id)
                    .FirstOrDefaultAsync();

            if (!staffId.HasValue)
            {
                studentId =
                    await _context.Students
                        .Where(x =>
                            x.ApplicationUserId == userId)
                        .Select(x =>
                            (int?)x.Id)
                        .FirstOrDefaultAsync();
            }

            if (!staffId.HasValue &&
                !studentId.HasValue)
            {
                parentGuardianId =
                    await _context.ParentGuardians
                        .Where(x =>
                            x.ApplicationUserId == userId)
                        .Select(x =>
                            (int?)x.Id)
                        .FirstOrDefaultAsync();
            }
        }

        var auditLog =
            new AuditLog
            {
                UserId =
                    userId,

                StaffId =
                    staffId,

                StudentId =
                    studentId,

                ParentGuardianId =
                    parentGuardianId,

                Action =
                    action,

                EntityName =
                    entityName,

                EntityId =
                    entityId,

                Description =
                    description,

                OldValues =
                    oldValues == null
                        ? null
                        : JsonSerializer.Serialize(
                            oldValues),

                NewValues =
                    newValues == null
                        ? null
                        : JsonSerializer.Serialize(
                            newValues),

                IpAddress =
                    httpContext?
                        .Connection
                        .RemoteIpAddress?
                        .ToString(),

                UserAgent =
                    httpContext?
                        .Request
                        .Headers["User-Agent"]
                        .ToString(),

                CreatedAt =
                    DateTime.UtcNow
            };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync();
    }
}