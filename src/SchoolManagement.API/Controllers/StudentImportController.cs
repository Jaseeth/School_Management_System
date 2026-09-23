using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Students.Import;
using SchoolManagement.Application.Students.Import.DTOs;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;
using SchoolManagement.Application.Auditing;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-import")]
[Authorize(Roles = "Admin,Principal,Deputy Principal")]
public class StudentImportController : ControllerBase
{
    private const long MaxFileSize =
        5 * 1024 * 1024; // 5 MB

    private readonly IStudentImportExcelReader
        _excelReader;

    private readonly IStudentImportValidationService
        _validationService;

    private readonly IStudentImportReferenceResolver
        _referenceResolver;

    private readonly IStudentImportService
        _studentImportService;

    private readonly ApplicationDbContext
        _context;

    private readonly IAuditLogService
    _auditLogService;
    private readonly IStudentImportErrorReportService
    _errorReportService;

    public StudentImportController(
    IStudentImportExcelReader excelReader,
    IStudentImportValidationService validationService,
    IStudentImportReferenceResolver referenceResolver,
    IStudentImportService studentImportService,
    ApplicationDbContext context,
    IAuditLogService auditLogService,
    IStudentImportErrorReportService errorReportService)
    {
        _excelReader =
            excelReader;

        _validationService =
            validationService;

        _referenceResolver =
            referenceResolver;

        _studentImportService =
            studentImportService;

        _context =
            context;

        _auditLogService =
            auditLogService;

        _errorReportService =
    errorReportService;
    }

    // ============================================================
    // PREVIEW STUDENT IMPORT
    //
    // Reads and validates Excel.
    // NOTHING is saved to database.
    // ============================================================

    [HttpPost("preview")]
    [RequestSizeLimit(MaxFileSize)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = MaxFileSize)]
    public async Task<IActionResult> Preview(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        // ========================================================
        // FILE REQUIRED
        // ========================================================

        if (file == null ||
            file.Length == 0)
        {
            return BadRequest(new
            {
                message =
                    "Please select an Excel file."
            });
        }

        // ========================================================
        // FILE SIZE
        // ========================================================

        if (file.Length >
            MaxFileSize)
        {
            return BadRequest(new
            {
                message =
                    "Excel file size cannot exceed 5 MB."
            });
        }

        // ========================================================
        // FILE EXTENSION
        // ========================================================

        var extension =
            Path.GetExtension(
                file.FileName);

        if (!string.Equals(
            extension,
            ".xlsx",
            StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Only .xlsx Excel files are allowed."
            });
        }

        // ========================================================
        // COPY TO MEMORY
        //
        // File is NOT stored on server.
        // ========================================================

        await using var memoryStream =
            new MemoryStream();

        await file.CopyToAsync(
            memoryStream,
            cancellationToken);

        // ========================================================
        // BASIC XLSX SIGNATURE CHECK
        // ========================================================

        if (!LooksLikeXlsx(
            memoryStream))
        {
            return BadRequest(new
            {
                message =
                    "The uploaded file is not a valid Excel .xlsx file."
            });
        }

        memoryStream.Position =
            0;

        try
        {
            // ====================================================
            // READ EXCEL
            // ====================================================

            var rows =
                await _excelReader
                    .ReadAsync(
                        memoryStream,
                        cancellationToken);

            if (rows.Count == 0)
            {
                return BadRequest(new
                {
                    message =
                        "The Excel file does not contain any student rows."
                });
            }

            // ====================================================
            // VALIDATE
            // ====================================================

            var validation =
                await _validationService
                    .ValidateAsync(
                        rows,
                        cancellationToken);

            // ====================================================
            // GET VALID ROWS
            // ====================================================

            var validRowNumbers =
                validation.Rows
                    .Where(x =>
                        x.IsValid)
                    .Select(x =>
                        x.RowNumber)
                    .ToHashSet();

            var validRows =
                rows
                    .Where(x =>
                        validRowNumbers.Contains(
                            x.RowNumber))
                    .ToList();

            IReadOnlyList<
                ResolvedStudentImportRowDto>
                resolvedRows =
                    Array.Empty<
                        ResolvedStudentImportRowDto>();

            // ====================================================
            // RESOLVE ONLY VALID ROWS
            // ====================================================

            if (validRows.Count > 0)
            {
                resolvedRows =
                    await _referenceResolver
                        .ResolveAsync(
                            validRows,
                            cancellationToken);
            }

            // ====================================================
            // RESPONSE
            // ====================================================

            var response =
                new StudentImportPreviewResponseDto
                {
                    Validation =
                        validation,

                    ResolvedRows =
                        resolvedRows.ToList()
                };

            return Ok(
                response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message =
                    ex.Message
            });
        }
        catch (ArgumentException)
        {
            return BadRequest(new
            {
                message =
                    "Unable to read the Excel file. Please use the provided student import template."
            });
        }
    }

    // ============================================================
    // CONFIRM STUDENT IMPORT
    //
    // This endpoint actually saves records.
    //
    // IMPORTANT:
    // The service validates everything AGAIN before saving.
    // ============================================================

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(
        ConfirmStudentImportRequest request,
        CancellationToken cancellationToken)
    {
        // ========================================================
        // REQUEST VALIDATION
        // ========================================================

        if (request == null ||
            request.Rows == null ||
            request.Rows.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "No student rows were provided for import."
            });
        }

        // ========================================================
        // BULK SAFETY LIMIT
        // ========================================================

        if (request.Rows.Count > 2000)
        {
            return BadRequest(new
            {
                message =
                    "A maximum of 2000 students can be imported in one request."
            });
        }

        // ========================================================
        // GET LOGGED-IN USER ID
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
        // GET LOGGED-IN STAFF RECORD
        //
        // We need Staff.Id because StudentAcademicEnrollment
        // requires CreatedByStaffId.
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

        try
        {
            // ====================================================
            // IMPORT
            //
            // Service will:
            //
            // 1. Validate again
            // 2. Resolve references again
            // 3. Open database transaction
            // 4. Create students
            // 5. Create/reuse parents
            // 6. Create relationships
            // 7. Create academic enrollments
            // 8. Create subject enrollments
            // 9. Commit
            // ====================================================

            var result =
                await _studentImportService
                    .ImportAsync(
                        request.Rows,
                        currentStaff.Id,
                        cancellationToken);

            // ====================================================
            // AUDIT LOG
            // ====================================================

            var importBatchId =
                Guid.NewGuid()
                    .ToString("N");

            await _auditLogService.LogAsync(
                action: "BulkImport",
                entityName: "StudentBulkImport",
                entityId: importBatchId,
                description:
                    $"{result.ImportedStudents} student(s) were imported through the bulk student import.",
                newValues: new
                {
                    BatchId =
                        importBatchId,

                    ImportedByStaffId =
                        currentStaff.Id,

                    TotalRows =
                        result.TotalRows,

                    ImportedStudents =
                        result.ImportedStudents,

                    CreatedParents =
                        result.CreatedParents,

                    ReusedParents =
                        result.ReusedParents,

                    CreatedRelationships =
                        result.CreatedRelationships,

                    CreatedAcademicEnrollments =
                        result.CreatedAcademicEnrollments,

                    CreatedSubjectEnrollments =
                        result.CreatedSubjectEnrollments
                });

            // ====================================================
            // SUCCESS
            // ====================================================

            return Ok(new
            {
                message =
                    "Student import completed successfully.",

                result
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message =
                    ex.Message
            });
        }
    }

    // ============================================================
    // DOWNLOAD STUDENT IMPORT ERROR REPORT
    // ============================================================

    [HttpPost("error-report")]
    public async Task<IActionResult> DownloadErrorReport(
        ConfirmStudentImportRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null ||
            request.Rows == null ||
            request.Rows.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "No student rows were provided."
            });
        }

        if (request.Rows.Count > 2000)
        {
            return BadRequest(new
            {
                message =
                    "A maximum of 2000 rows is allowed."
            });
        }

        var report =
            await _errorReportService
                .GenerateAsync(
                    request.Rows,
                    cancellationToken);

        var fileName =
            $"Student_Import_Error_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(
            report,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // ============================================================
    // BASIC XLSX SIGNATURE CHECK
    // ============================================================

    private static bool LooksLikeXlsx(
        MemoryStream stream)
    {
        if (stream.Length < 4)
        {
            return false;
        }

        var originalPosition =
            stream.Position;

        stream.Position =
            0;

        Span<byte> header =
            stackalloc byte[4];

        var bytesRead =
            stream.Read(
                header);

        stream.Position =
            originalPosition;

        if (bytesRead < 4)
        {
            return false;
        }

        return
            header[0] == 0x50 &&
            header[1] == 0x4B &&
            (
                (
                    header[2] == 0x03 &&
                    header[3] == 0x04
                )
                ||
                (
                    header[2] == 0x05 &&
                    header[3] == 0x06
                )
                ||
                (
                    header[2] == 0x07 &&
                    header[3] == 0x08
                )
            );
    }
}