using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Students.Export;
using SchoolManagement.Application.Students.Export.DTOs;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Auditing;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-export")]
[Authorize(
    Roles =
        "Admin,Principal,Deputy Principal")]
public class StudentExportController :
    ControllerBase
{
    private readonly IStudentExportService
        _studentExportService;

    private readonly ApplicationDbContext
    _context;

    private readonly IAuditLogService
        _auditLogService;

    public StudentExportController(
    IStudentExportService studentExportService,
    ApplicationDbContext context,
    IAuditLogService auditLogService)
    {
        _studentExportService =
            studentExportService;

        _context =
            context;

        _auditLogService =
            auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Export(
    [FromQuery] StudentExportFilterDto filter,
    CancellationToken cancellationToken)
    {
        // ========================================================
        // GET LOGGED-IN USER
        // ========================================================

        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return Unauthorized();
        }

        // ========================================================
        // GET LOGGED-IN STAFF
        // ========================================================

        var currentStaff =
            await _context.Staff
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.ApplicationUserId ==
                            userId &&
                        x.IsActive,
                    cancellationToken);

        if (currentStaff == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active staff record."
            });
        }

        // ========================================================
        // GENERATE EXPORT
        // ========================================================

        var file =
            await _studentExportService
                .ExportAsync(
                    filter,
                    cancellationToken);

        // ========================================================
        // AUDIT LOG
        // ========================================================

        var exportBatchId =
            Guid.NewGuid()
                .ToString("N");

        await _auditLogService.LogAsync(
            action: "Export",
            entityName: "StudentBulkExport",
            entityId: exportBatchId,
            description:
                "Student bulk export was generated.",
            newValues: new
            {
                ExportBatchId =
                    exportBatchId,

                ExportedByStaffId =
                    currentStaff.Id,

                AcademicYearId =
                    filter.AcademicYearId,

                SectionId =
                    filter.SectionId,

                GradeId =
                    filter.GradeId,

                SchoolClassId =
                    filter.SchoolClassId,

                IsActive =
                    filter.IsActive
            });

        // ========================================================
        // DOWNLOAD
        // ========================================================

        var fileName =
            $"Students_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(
            file,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}