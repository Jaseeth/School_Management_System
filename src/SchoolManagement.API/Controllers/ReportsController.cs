using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using SchoolManagement.API.Reports;
using SchoolManagement.Application.Reports.DTOs;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin,Principal,Deputy Principal")]
public class ReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ReportsController(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // ============================================================
    // STUDENT ACADEMIC PROFILE JSON REPORT
    // ============================================================

    [HttpGet("students/{studentId:int}/academic-profile")]
    public async Task<IActionResult> GetStudentAcademicProfile(
        int studentId)
    {
        var report =
            await BuildStudentAcademicProfileAsync(
                studentId);

        if (report == null)
        {
            return NotFound(new
            {
                message = "Student not found."
            });
        }

        return Ok(report);
    }


    // ============================================================
    // STUDENT ACADEMIC PROFILE PDF REPORT
    // ============================================================

    [HttpGet("students/{studentId:int}/academic-profile/pdf")]
    public async Task<IActionResult> GetStudentAcademicProfilePdf(
        int studentId)
    {
        var report =
            await BuildStudentAcademicProfileAsync(
                studentId);

        if (report == null)
        {
            return NotFound(new
            {
                message = "Student not found."
            });
        }

        // ========================================================
        // SCHOOL LOGO
        // ========================================================

        var webRootPath =
            _environment.WebRootPath;

        var logoPath =
            string.IsNullOrWhiteSpace(webRootPath)
                ? string.Empty
                : Path.Combine(
                    webRootPath,
                    "images",
                    "school-logo.png");

        if (string.IsNullOrWhiteSpace(logoPath) ||
            !System.IO.File.Exists(logoPath))
        {
            logoPath =
                string.Empty;
        }

        // ========================================================
        // GENERATE PDF
        // ========================================================

        var document =
            new StudentAcademicProfilePdfDocument(
                report,
                logoPath);

        var pdfBytes =
            document.GeneratePdf();

        var fileName =
            $"Student-Academic-Profile-{report.IndexNumber}.pdf";

        return File(
            pdfBytes,
            "application/pdf",
            fileName);
    }


    // ============================================================
    // BUILD STUDENT ACADEMIC PROFILE
    // ============================================================

    private async Task<StudentAcademicProfileReportDto?>
        BuildStudentAcademicProfileAsync(
            int studentId)
    {
        // ========================================================
        // STUDENT
        // ========================================================

        var student =
            await _context.Students
                .AsNoTracking()
                .Include(x =>
                    x.GraduationAcademicYear)
                .FirstOrDefaultAsync(x =>
                    x.Id == studentId);

        if (student == null)
        {
            return null;
        }


        // ========================================================
        // CURRENT ENROLLMENT
        // ========================================================

        var currentEnrollment =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsCurrent &&
                    x.IsActive)
                .Select(x =>
                    new CurrentEnrollmentReportDto
                    {
                        AcademicYearId =
                            x.AcademicYearId,

                        AcademicYearName =
                            x.AcademicYear.Name,

                        SchoolClassId =
                            x.SchoolClassId,

                        ClassName =
                            x.SchoolClass.Name,

                        GradeName =
                            x.SchoolClass
                                .Grade.Name,

                        SectionName =
                            x.SchoolClass
                                .Grade
                                .Section.Name
                    })
                .FirstOrDefaultAsync();


        // ========================================================
        // ENROLLMENT HISTORY
        // ========================================================

        var enrollmentHistory =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsActive)
                .OrderByDescending(x =>
                    x.AcademicYear.StartDate)
                .Select(x =>
                    new EnrollmentHistoryReportDto
                    {
                        AcademicYearId =
                            x.AcademicYearId,

                        AcademicYearName =
                            x.AcademicYear.Name,

                        GradeName =
                            x.SchoolClass
                                .Grade.Name,

                        ClassName =
                            x.SchoolClass.Name,

                        SectionName =
                            x.SchoolClass
                                .Grade
                                .Section.Name,

                        EnrollmentDate =
                            x.EnrollmentDate,

                        IsCurrent =
                            x.IsCurrent
                    })
                .ToListAsync();


        // ========================================================
        // PROMOTION HISTORY
        // ========================================================

        var promotionHistory =
            await _context.StudentPromotionHistories
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId)
                .OrderByDescending(x =>
                    x.ProcessedAt)
                .Select(x =>
                    new PromotionHistoryReportDto
                    {
                        Action =
                            x.Action,

                        Reason =
                            x.Reason,

                        FromAcademicYearName =
                            x.FromAcademicYear.Name,

                        ToAcademicYearName =
                            x.ToAcademicYear.Name,

                        FromGradeName =
                            x.FromSchoolClass
                                .Grade.Name,

                        FromClassName =
                            x.FromSchoolClass.Name,

                        ToGradeName =
                            x.ToSchoolClass != null
                                ? x.ToSchoolClass
                                    .Grade.Name
                                : null,

                        ToClassName =
                            x.ToSchoolClass != null
                                ? x.ToSchoolClass.Name
                                : null,

                        ProcessedByStaffName =
                            x.ProcessedByStaff
                                .FullName,

                        ProcessedAt =
                            x.ProcessedAt
                    })
                .ToListAsync();


        // ========================================================
        // SUBJECTS
        // ========================================================

        var subjects =
            await _context.StudentSubjectEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId)
                .OrderByDescending(x =>
                    x.AcademicYear.StartDate)
                .ThenBy(x =>
                    x.Subject.Name)
                .Select(x =>
                    new StudentSubjectReportDto
                    {
                        AcademicYearId =
                            x.AcademicYearId,

                        AcademicYearName =
                            x.AcademicYear.Name,

                        SubjectId =
                            x.SubjectId,

                        SubjectName =
                            x.Subject.Name,

                        IsActive =
                            x.IsActive
                    })
                .ToListAsync();


        // ========================================================
        // ATTENDANCE
        // ========================================================

        var attendanceRecords =
            await _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId)
                .ToListAsync();

        var totalDays =
            attendanceRecords.Count;

        var presentDays =
            attendanceRecords.Count(x =>
                x.Status ==
                    AttendanceStatus.Present);

        var absentDays =
            attendanceRecords.Count(x =>
                x.Status ==
                    AttendanceStatus.Absent);

        var attendancePercentage =
            totalDays == 0
                ? 0
                : Math.Round(
                    (decimal)presentDays /
                    totalDays * 100,
                    2);


        // ========================================================
        // PUBLISHED RESULTS
        // ========================================================

        var publishedResults =
            await _context.StudentMarks
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsPublished)
                .OrderByDescending(x =>
                    x.Exam
                        .AcademicTerm
                        .AcademicYear
                        .StartDate)
                .ThenBy(x =>
                    x.Exam.Name)
                .ThenBy(x =>
                    x.TeacherAssignment
                        .Subject.Name)
                .Select(x =>
                    new PublishedResultReportDto
                    {
                        ExamId =
                            x.ExamId,

                        ExamName =
                            x.Exam.Name,

                        SubjectName =
                            x.TeacherAssignment
                                .Subject.Name,

                        MarksObtained =
                            x.MarksObtained,

                        MaximumMarks =
                            x.Exam.MaximumMarks
                    })
                .ToListAsync();


        // ========================================================
        // BUILD FINAL REPORT DTO
        // ========================================================

        var report =
            new StudentAcademicProfileReportDto
            {
                StudentId =
                    student.Id,

                IndexNumber =
                    student.IndexNumber,

                FullName =
                    student.FullName,

                DateOfBirth =
                    student.DateOfBirth,

                IsActive =
                    student.IsActive,

                IsGraduated =
                    student.IsGraduated,

                GraduationDate =
                    student.GraduationDate,

                GraduationAcademicYearName =
                    student.GraduationAcademicYear != null
                        ? student
                            .GraduationAcademicYear
                            .Name
                        : null,

                CurrentEnrollment =
                    currentEnrollment,

                EnrollmentHistory =
                    enrollmentHistory,

                PromotionHistory =
                    promotionHistory,

                Subjects =
                    subjects,

                Attendance =
                    new AttendanceSummaryReportDto
                    {
                        TotalDays =
                            totalDays,

                        PresentDays =
                            presentDays,

                        AbsentDays =
                            absentDays,

                        AttendancePercentage =
                            attendancePercentage
                    },

                PublishedResults =
                    publishedResults
            };

        return report;
    }
}