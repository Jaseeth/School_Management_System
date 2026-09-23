using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Infrastructure.Persistence;

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

        return Ok(auditLog);
    }
}