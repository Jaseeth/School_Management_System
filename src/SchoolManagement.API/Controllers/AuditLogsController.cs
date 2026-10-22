using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Infrastructure.Persistence;
using System.Text.Json;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "Admin,Principal,Deputy Principal")]
public class AuditLogsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AuditLogsController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // GET AUDIT LOGS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        string? action = null,
        string? entityName = null,
        int? staffId = null,
        int? studentId = null,
        int? parentGuardianId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 50)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 50;
        }

        if (pageSize > 100)
        {
            pageSize = 100;
        }

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value > toDate.Value)
        {
            return BadRequest(new
            {
                message =
                    "FromDate cannot be later than ToDate."
            });
        }

        var query =
            _context.AuditLogs
                .AsNoTracking()
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var actionValue =
                action.Trim();

            query =
                query.Where(x =>
                    x.Action == actionValue);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var entityValue =
                entityName.Trim();

            query =
                query.Where(x =>
                    x.EntityName == entityValue);
        }

        if (staffId.HasValue)
        {
            query =
                query.Where(x =>
                    x.StaffId == staffId.Value);
        }

        if (studentId.HasValue)
        {
            query =
                query.Where(x =>
                    x.StudentId == studentId.Value);
        }

        if (parentGuardianId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ParentGuardianId ==
                    parentGuardianId.Value);
        }

        if (fromDate.HasValue)
        {
            query =
                query.Where(x =>
                    x.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query =
                query.Where(x =>
                    x.CreatedAt <= toDate.Value);
        }

        var totalCount =
            await query.CountAsync();

        var auditLogs =
            await query
                .OrderByDescending(x =>
                    x.CreatedAt)
                .ThenByDescending(x =>
                    x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,

                    x.UserId,
                    actorName = _context.Users.Where(u => u.Id == x.UserId).Select(u => u.FullName).FirstOrDefault()
                        ?? _context.Staff.Where(s => s.Id == x.StaffId).Select(s => s.FullName).FirstOrDefault()
                        ?? _context.Students.Where(s => s.Id == x.StudentId).Select(s => s.FullName).FirstOrDefault()
                        ?? _context.ParentGuardians.Where(p => p.Id == x.ParentGuardianId).Select(p => p.FullName).FirstOrDefault(),

                    x.StaffId,

                    x.StudentId,

                    x.ParentGuardianId,

                    x.Action,

                    x.EntityName,

                    x.EntityId,

                    x.Description,

                    x.OldValues,

                    x.NewValues,

                    x.IpAddress,

                    x.UserAgent,

                    x.CreatedAt
                })
                .ToListAsync();

        var totalPages =
            totalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    totalCount /
                    (double)pageSize);

        return Ok(new
        {
            page,

            pageSize,

            totalCount,

            totalPages,

            auditLogs
        });
    }

    // ============================================================
    // GET AUDIT LOG BY ID
    // ============================================================

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetAuditLogById(
        long id)
    {
        var auditLog =
            await _context.AuditLogs
                .AsNoTracking()
                .Where(x =>
                    x.Id == id)
                .Select(x => new
                {
                    x.Id,

                    x.UserId,
                    actorName = _context.Users.Where(u => u.Id == x.UserId).Select(u => u.FullName).FirstOrDefault()
                        ?? _context.Staff.Where(s => s.Id == x.StaffId).Select(s => s.FullName).FirstOrDefault()
                        ?? _context.Students.Where(s => s.Id == x.StudentId).Select(s => s.FullName).FirstOrDefault()
                        ?? _context.ParentGuardians.Where(p => p.Id == x.ParentGuardianId).Select(p => p.FullName).FirstOrDefault(),

                    x.StaffId,

                    x.StudentId,

                    x.ParentGuardianId,

                    x.Action,

                    x.EntityName,

                    x.EntityId,

                    x.Description,

                    x.OldValues,

                    x.NewValues,

                    x.IpAddress,

                    x.UserAgent,

                    x.CreatedAt
                })
                .FirstOrDefaultAsync();

        if (auditLog == null)
        {
            return NotFound(new
            {
                message =
                    "Audit log not found."
            });
        }

        object? recipientContext = null;
        var hasSavedAudience = false;
        try {
            using var snapshot = JsonDocument.Parse(auditLog.NewValues ?? "{}");
            hasSavedAudience = snapshot.RootElement.ValueKind == JsonValueKind.Object && snapshot.RootElement.EnumerateObject()
                .Any(p => new[] { "students", "staff", "parents" }.Contains(p.Name.ToLowerInvariant()));
        } catch (JsonException) { }
        if (!hasSavedAudience && auditLog.EntityName == "SchoolAnnouncement" && int.TryParse(auditLog.EntityId, out var announcementId))
        {
            var announcement = await _context.SchoolAnnouncements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == announcementId);
            // Older audit snapshots did not retain recipient identities. Current record data is labelled separately.
            if (announcement != null)
            {
                var students = await _context.Students.Where(s => announcement.StudentIds.Contains(s.Id))
                    .Select(s => s.FullName + " (" + s.IndexNumber + ")").ToListAsync();
                var staff = await _context.Staff.Where(s => announcement.StaffIds.Contains(s.Id))
                    .Select(s => s.FullName + " (" + s.StaffNumber + ")").ToListAsync();
                var parents = await _context.ParentGuardians.Where(p => announcement.ParentIds.Contains(p.Id))
                    .Select(p => p.FullName + " (" + p.ParentNumber + ")").ToListAsync();
                recipientContext = new { note = "Current announcement audience. Older audit entries may not contain the original recipient list; this is not proof of delivery.",
                    details = new { students, staff, parents,
                        section = await _context.Sections.Where(s => s.Id == announcement.SectionId).Select(s => s.Name).FirstOrDefaultAsync(),
                        grade = await _context.Grades.Where(g => g.Id == announcement.GradeId).Select(g => g.Name).FirstOrDefaultAsync(),
                        schoolClass = await _context.SchoolClasses.Where(c => c.Id == announcement.SchoolClassId).Select(c => c.Grade.Name + " " + c.Name).FirstOrDefaultAsync() } };
            }
        }
        var displayDescription = auditLog.Description;
        if (auditLog.Action == "RemoveSectionHeadAssignment" && int.TryParse(auditLog.EntityId, out var assignmentId))
        {
            var assignment = await _context.SectionHeadAssignments.AsNoTracking().Where(a => a.Id == assignmentId)
                .Select(a => new { a.Staff.FullName, a.Staff.StaffNumber, section = a.Section.Name, year = a.AcademicYear.Name }).FirstOrDefaultAsync();
            if (assignment != null && (auditLog.Description?.StartsWith("Deactivated staff ") ?? false))
                displayDescription = $"Removed {assignment.FullName} ({assignment.StaffNumber}) as section head of {assignment.section} for {assignment.year}. Names are from the linked record.";
        }
        return Ok(new { auditLog.Id, auditLog.UserId, auditLog.actorName, auditLog.StaffId, auditLog.StudentId,
            auditLog.ParentGuardianId, auditLog.Action, auditLog.EntityName, auditLog.EntityId, Description = displayDescription,
            auditLog.OldValues, auditLog.NewValues, auditLog.IpAddress, auditLog.UserAgent, auditLog.CreatedAt, recipientContext });
    }
}
