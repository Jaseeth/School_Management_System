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

    // ============================================================
    // CLASS STUDENT REPORT
    // ============================================================

    [HttpGet("classes/{schoolClassId:int}/students")]
    public async Task<IActionResult> GetClassStudentReport(
        int schoolClassId,
        int academicYearId)
    {
        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicYearId);

        if (academicYear == null)
        {
            return NotFound(new
            {
                message = "Academic year not found."
            });
        }

        var schoolClass =
            await _context.SchoolClasses
                .AsNoTracking()
                .Include(x => x.Grade)
                    .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == schoolClassId);

        if (schoolClass == null)
        {
            return NotFound(new
            {
                message = "School class not found."
            });
        }

        var enrollments =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.AcademicYearId == academicYearId &&
                    x.SchoolClassId == schoolClassId &&
                    x.IsActive)
                .Select(x => new
                {
                    x.StudentId,

                    x.Student.IndexNumber,

                    x.Student.FullName,

                    x.Student.IsActive,

                    IsCompleted =
                        x.Student.IsGraduated
                })
                .OrderBy(x =>
                    x.FullName)
                .ToListAsync();

        var studentIds =
            enrollments
                .Select(x => x.StudentId)
                .ToList();

        var attendanceData =
            await _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    studentIds.Contains(x.StudentId) &&
                    x.AcademicYearId == academicYearId &&
                    x.SchoolClassId == schoolClassId)
                .GroupBy(x =>
                    x.StudentId)
                .Select(group => new
                {
                    StudentId =
                        group.Key,

                    TotalDays =
                        group.Count(),

                    PresentDays =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Present),

                    AbsentDays =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Absent)
                })
                .ToListAsync();

        var subjectCounts =
            await _context.StudentSubjectEnrollments
                .AsNoTracking()
                .Where(x =>
                    studentIds.Contains(x.StudentId) &&
                    x.AcademicYearId == academicYearId &&
                    x.IsActive)
                .GroupBy(x =>
                    x.StudentId)
                .Select(group => new
                {
                    StudentId =
                        group.Key,

                    SubjectCount =
                        group.Count()
                })
                .ToListAsync();

        var students =
            enrollments
                .Select(enrollment =>
                {
                    var attendance =
                        attendanceData
                            .FirstOrDefault(x =>
                                x.StudentId ==
                                    enrollment.StudentId);

                    var subject =
                        subjectCounts
                            .FirstOrDefault(x =>
                                x.StudentId ==
                                    enrollment.StudentId);

                    var totalDays =
                        attendance?.TotalDays ?? 0;

                    var presentDays =
                        attendance?.PresentDays ?? 0;

                    var absentDays =
                        attendance?.AbsentDays ?? 0;

                    var attendancePercentage =
                        totalDays == 0
                            ? 0
                            : Math.Round(
                                (decimal)presentDays /
                                totalDays * 100,
                                2);

                    return new ClassStudentReportItemDto
                    {
                        StudentId =
                            enrollment.StudentId,

                        IndexNumber =
                            enrollment.IndexNumber,

                        FullName =
                            enrollment.FullName,

                        IsActive =
                            enrollment.IsActive,

                        IsCompleted =
                            enrollment.IsCompleted,

                        SubjectCount =
                            subject?.SubjectCount ?? 0,

                        TotalAttendanceDays =
                            totalDays,

                        PresentDays =
                            presentDays,

                        AbsentDays =
                            absentDays,

                        AttendancePercentage =
                            attendancePercentage
                    };
                })
                .ToList();

        var report =
            new ClassStudentReportDto
            {
                AcademicYearId =
                    academicYear.Id,

                AcademicYearName =
                    academicYear.Name,

                SchoolClassId =
                    schoolClass.Id,

                ClassName =
                    schoolClass.Name,

                GradeName =
                    schoolClass.Grade.Name,

                SectionName =
                    schoolClass.Grade.Section.Name,

                TotalStudents =
                    students.Count,

                Students =
                    students
            };

        return Ok(report);
    }

    // ============================================================
    // CLASS STUDENT PDF REPORT
    // ============================================================

    [HttpGet("classes/{schoolClassId:int}/students/pdf")]
    public async Task<IActionResult> GetClassStudentReportPdf(
        int schoolClassId,
        int academicYearId)
    {
        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicYearId);

        if (academicYear == null)
        {
            return NotFound(new
            {
                message = "Academic year not found."
            });
        }

        var schoolClass =
            await _context.SchoolClasses
                .AsNoTracking()
                .Include(x => x.Grade)
                    .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == schoolClassId);

        if (schoolClass == null)
        {
            return NotFound(new
            {
                message = "School class not found."
            });
        }

        var enrollments =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.AcademicYearId == academicYearId &&
                    x.SchoolClassId == schoolClassId &&
                    x.IsActive)
                .Select(x => new
                {
                    x.StudentId,

                    x.Student.IndexNumber,

                    x.Student.FullName,

                    x.Student.IsActive,

                    IsCompleted =
                        x.Student.IsGraduated
                })
                .OrderBy(x =>
                    x.FullName)
                .ToListAsync();

        var studentIds =
            enrollments
                .Select(x => x.StudentId)
                .ToList();

        var attendanceData =
            await _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    studentIds.Contains(x.StudentId) &&
                    x.AcademicYearId == academicYearId &&
                    x.SchoolClassId == schoolClassId)
                .GroupBy(x =>
                    x.StudentId)
                .Select(group => new
                {
                    StudentId =
                        group.Key,

                    TotalDays =
                        group.Count(),

                    PresentDays =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Present),

                    AbsentDays =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Absent)
                })
                .ToListAsync();

        var subjectCounts =
            await _context.StudentSubjectEnrollments
                .AsNoTracking()
                .Where(x =>
                    studentIds.Contains(x.StudentId) &&
                    x.AcademicYearId == academicYearId &&
                    x.IsActive)
                .GroupBy(x =>
                    x.StudentId)
                .Select(group => new
                {
                    StudentId =
                        group.Key,

                    SubjectCount =
                        group.Count()
                })
                .ToListAsync();

        var students =
            enrollments
                .Select(enrollment =>
                {
                    var attendance =
                        attendanceData
                            .FirstOrDefault(x =>
                                x.StudentId ==
                                    enrollment.StudentId);

                    var subject =
                        subjectCounts
                            .FirstOrDefault(x =>
                                x.StudentId ==
                                    enrollment.StudentId);

                    var totalDays =
                        attendance?.TotalDays ?? 0;

                    var presentDays =
                        attendance?.PresentDays ?? 0;

                    var absentDays =
                        attendance?.AbsentDays ?? 0;

                    var attendancePercentage =
                        totalDays == 0
                            ? 0
                            : Math.Round(
                                (decimal)presentDays /
                                totalDays * 100,
                                2);

                    return new ClassStudentReportItemDto
                    {
                        StudentId =
                            enrollment.StudentId,

                        IndexNumber =
                            enrollment.IndexNumber,

                        FullName =
                            enrollment.FullName,

                        IsActive =
                            enrollment.IsActive,

                        IsCompleted =
                            enrollment.IsCompleted,

                        SubjectCount =
                            subject?.SubjectCount ?? 0,

                        TotalAttendanceDays =
                            totalDays,

                        PresentDays =
                            presentDays,

                        AbsentDays =
                            absentDays,

                        AttendancePercentage =
                            attendancePercentage
                    };
                })
                .ToList();

        var report =
            new ClassStudentReportDto
            {
                AcademicYearId =
                    academicYear.Id,

                AcademicYearName =
                    academicYear.Name,

                SchoolClassId =
                    schoolClass.Id,

                ClassName =
                    schoolClass.Name,

                GradeName =
                    schoolClass.Grade.Name,

                SectionName =
                    schoolClass.Grade.Section.Name,

                TotalStudents =
                    students.Count,

                Students =
                    students
            };

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

        var document =
            new ClassStudentReportPdfDocument(
                report,
                logoPath);

        var pdfBytes =
            document.GeneratePdf();

        var fileName =
            $"Class-Student-Report-" +
            $"{academicYear.Name.Replace("/", "-")}-" +
            $"Grade-{schoolClass.Grade.Name}-" +
            $"Class-{schoolClass.Name}.pdf";

        return File(
            pdfBytes,
            "application/pdf",
            fileName);
    }

    // ============================================================
    // CLASS ATTENDANCE REPORT
    // ============================================================

    [HttpGet("classes/{schoolClassId:int}/attendance")]
    public async Task<IActionResult> GetClassAttendanceReport(
        int schoolClassId,
        int academicYearId,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value > toDate.Value)
        {
            return BadRequest(new
            {
                message =
                    "From date cannot be later than to date."
            });
        }

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicYearId);

        if (academicYear == null)
        {
            return NotFound(new
            {
                message =
                    "Academic year not found."
            });
        }

        var schoolClass =
            await _context.SchoolClasses
                .AsNoTracking()
                .Include(x =>
                    x.Grade)
                    .ThenInclude(x =>
                        x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == schoolClassId);

        if (schoolClass == null)
        {
            return NotFound(new
            {
                message =
                    "School class not found."
            });
        }

        // ========================================================
        // STUDENTS ENROLLED IN THIS CLASS / YEAR
        // ========================================================

        var students =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.AcademicYearId == academicYearId &&
                    x.SchoolClassId == schoolClassId &&
                    x.IsActive)
                .Select(x =>
                    new
                    {
                        x.StudentId,
                        x.Student.IndexNumber,
                        x.Student.FullName
                    })
                .OrderBy(x =>
                    x.FullName)
                .ToListAsync();

        var studentIds =
            students
                .Select(x =>
                    x.StudentId)
                .ToList();

        // ========================================================
        // ATTENDANCE QUERY
        // ========================================================

        var attendanceQuery =
            _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    studentIds.Contains(x.StudentId) &&
                    x.AcademicYearId == academicYearId &&
                    x.SchoolClassId == schoolClassId);

        if (fromDate.HasValue)
        {
            var fromDateTime =
                fromDate.Value.ToDateTime(
                    TimeOnly.MinValue);

            attendanceQuery =
                attendanceQuery.Where(x =>
                    x.AttendanceDate >=
                        fromDateTime);
        }

        if (toDate.HasValue)
        {
            var toDateTime =
                toDate.Value.ToDateTime(
                    TimeOnly.MaxValue);

            attendanceQuery =
                attendanceQuery.Where(x =>
                    x.AttendanceDate <=
                        toDateTime);
        }

        var attendanceData =
            await attendanceQuery
                .GroupBy(x =>
                    x.StudentId)
                .Select(group =>
                    new
                    {
                        StudentId =
                            group.Key,

                        TotalDays =
                            group.Count(),

                        PresentDays =
                            group.Count(x =>
                                x.Status ==
                                    AttendanceStatus.Present),

                        AbsentDays =
                            group.Count(x =>
                                x.Status ==
                                    AttendanceStatus.Absent)
                    })
                .ToListAsync();

        // ========================================================
        // BUILD STUDENT ROWS
        // ========================================================

        var reportStudents =
            students
                .Select(student =>
                {
                    var attendance =
                        attendanceData
                            .FirstOrDefault(x =>
                                x.StudentId ==
                                    student.StudentId);

                    var totalDays =
                        attendance?.TotalDays ?? 0;

                    var presentDays =
                        attendance?.PresentDays ?? 0;

                    var absentDays =
                        attendance?.AbsentDays ?? 0;

                    var percentage =
                        totalDays == 0
                            ? 0
                            : Math.Round(
                                (decimal)presentDays /
                                totalDays * 100,
                                2);

                    return new ClassAttendanceStudentDto
                    {
                        StudentId =
                            student.StudentId,

                        IndexNumber =
                            student.IndexNumber,

                        FullName =
                            student.FullName,

                        TotalDays =
                            totalDays,

                        PresentDays =
                            presentDays,

                        AbsentDays =
                            absentDays,

                        AttendancePercentage =
                            percentage
                    };
                })
                .ToList();

        // ========================================================
        // FINAL REPORT
        // ========================================================

        var report =
            new ClassAttendanceReportDto
            {
                AcademicYearId =
                    academicYear.Id,

                AcademicYearName =
                    academicYear.Name,

                SchoolClassId =
                    schoolClass.Id,

                ClassName =
                    schoolClass.Name,

                GradeName =
                    schoolClass.Grade.Name,

                SectionName =
                    schoolClass.Grade.Section.Name,

                FromDate =
                    fromDate,

                ToDate =
                    toDate,

                TotalStudents =
                    reportStudents.Count,

                Students =
                    reportStudents
            };

        return Ok(report);
    }

    // ============================================================
    // CLASS ATTENDANCE PDF REPORT
    // ============================================================

    [HttpGet("classes/{schoolClassId:int}/attendance/pdf")]
    public async Task<IActionResult> GetClassAttendanceReportPdf(
        int schoolClassId,
        int academicYearId,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value > toDate.Value)
        {
            return BadRequest(new
            {
                message =
                    "From date cannot be later than to date."
            });
        }

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicYearId);

        if (academicYear == null)
        {
            return NotFound(new
            {
                message =
                    "Academic year not found."
            });
        }

        var schoolClass =
            await _context.SchoolClasses
                .AsNoTracking()
                .Include(x =>
                    x.Grade)
                    .ThenInclude(x =>
                        x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == schoolClassId);

        if (schoolClass == null)
        {
            return NotFound(new
            {
                message =
                    "School class not found."
            });
        }

        var students =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.AcademicYearId == academicYearId &&
                    x.SchoolClassId == schoolClassId &&
                    x.IsActive)
                .Select(x =>
                    new
                    {
                        x.StudentId,
                        x.Student.IndexNumber,
                        x.Student.FullName
                    })
                .OrderBy(x =>
                    x.FullName)
                .ToListAsync();

        var studentIds =
            students
                .Select(x =>
                    x.StudentId)
                .ToList();

        var attendanceQuery =
            _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    studentIds.Contains(x.StudentId) &&
                    x.AcademicYearId == academicYearId &&
                    x.SchoolClassId == schoolClassId);

        if (fromDate.HasValue)
        {
            var fromDateTime =
                fromDate.Value.ToDateTime(
                    TimeOnly.MinValue);

            attendanceQuery =
                attendanceQuery.Where(x =>
                    x.AttendanceDate >=
                        fromDateTime);
        }

        if (toDate.HasValue)
        {
            var toDateTime =
                toDate.Value.ToDateTime(
                    TimeOnly.MaxValue);

            attendanceQuery =
                attendanceQuery.Where(x =>
                    x.AttendanceDate <=
                        toDateTime);
        }

        var attendanceData =
            await attendanceQuery
                .GroupBy(x =>
                    x.StudentId)
                .Select(group =>
                    new
                    {
                        StudentId =
                            group.Key,

                        TotalDays =
                            group.Count(),

                        PresentDays =
                            group.Count(x =>
                                x.Status ==
                                    AttendanceStatus.Present),

                        AbsentDays =
                            group.Count(x =>
                                x.Status ==
                                    AttendanceStatus.Absent)
                    })
                .ToListAsync();

        var reportStudents =
            students
                .Select(student =>
                {
                    var attendance =
                        attendanceData
                            .FirstOrDefault(x =>
                                x.StudentId ==
                                    student.StudentId);

                    var totalDays =
                        attendance?.TotalDays ?? 0;

                    var presentDays =
                        attendance?.PresentDays ?? 0;

                    var absentDays =
                        attendance?.AbsentDays ?? 0;

                    var percentage =
                        totalDays == 0
                            ? 0
                            : Math.Round(
                                (decimal)presentDays /
                                totalDays * 100,
                                2);

                    return new ClassAttendanceStudentDto
                    {
                        StudentId =
                            student.StudentId,

                        IndexNumber =
                            student.IndexNumber,

                        FullName =
                            student.FullName,

                        TotalDays =
                            totalDays,

                        PresentDays =
                            presentDays,

                        AbsentDays =
                            absentDays,

                        AttendancePercentage =
                            percentage
                    };
                })
                .ToList();

        var report =
            new ClassAttendanceReportDto
            {
                AcademicYearId =
                    academicYear.Id,

                AcademicYearName =
                    academicYear.Name,

                SchoolClassId =
                    schoolClass.Id,

                ClassName =
                    schoolClass.Name,

                GradeName =
                    schoolClass.Grade.Name,

                SectionName =
                    schoolClass.Grade.Section.Name,

                FromDate =
                    fromDate,

                ToDate =
                    toDate,

                TotalStudents =
                    reportStudents.Count,

                Students =
                    reportStudents
            };

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

        var document =
            new ClassAttendanceReportPdfDocument(
                report,
                logoPath);

        var pdfBytes =
            document.GeneratePdf();

        var fileName =
            $"Class-Attendance-Report-" +
            $"{academicYear.Name.Replace("/", "-")}-" +
            $"Grade-{schoolClass.Grade.Name}-" +
            $"Class-{schoolClass.Name}.pdf";

        return File(
            pdfBytes,
            "application/pdf",
            fileName);
    }

    // ============================================================
    // STUDENT RESULT REPORT
    // ============================================================

    [HttpGet("students/{studentId:int}/results")]
    public async Task<IActionResult> GetStudentResultReport(
        int studentId,
        int academicYearId,
        int academicTermId)
    {
        var student =
            await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == studentId);

        if (student == null)
        {
            return NotFound(new
            {
                message = "Student not found."
            });
        }

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicYearId);

        if (academicYear == null)
        {
            return NotFound(new
            {
                message = "Academic year not found."
            });
        }

        var academicTerm =
            await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicTermId &&
                    x.AcademicYearId == academicYearId);

        if (academicTerm == null)
        {
            return NotFound(new
            {
                message =
                    "Academic term not found for the selected academic year."
            });
        }

        // Only published results should appear
        var marks =
            await _context.StudentMarks
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsPublished &&
                    x.Exam.AcademicTermId == academicTermId &&
                    x.TeacherAssignment.AcademicYearId == academicYearId)
                .OrderBy(x =>
                    x.Exam.Name)
                .ThenBy(x =>
                    x.TeacherAssignment.Subject.Name)
                .Select(x =>
                    new
                    {
                        x.ExamId,

                        ExamName =
                            x.Exam.Name,

                        SubjectId =
                            x.TeacherAssignment.SubjectId,

                        SubjectName =
                            x.TeacherAssignment.Subject.Name,

                        x.MarksObtained,

                        MaximumMarks =
                            x.Exam.MaximumMarks,

                        x.IsPublished
                    })
                .ToListAsync();

        var results =
            marks
                .Select(x =>
                    new StudentResultReportItemDto
                    {
                        ExamId =
                            x.ExamId,

                        ExamName =
                            x.ExamName,

                        SubjectId =
                            x.SubjectId,

                        SubjectName =
                            x.SubjectName,

                        MarksObtained =
                            x.MarksObtained,

                        MaximumMarks =
                            x.MaximumMarks,

                        Percentage =
                            x.MaximumMarks <= 0
                                ? 0
                                : Math.Round(
                                    x.MarksObtained /
                                    x.MaximumMarks * 100,
                                    2),

                        IsPublished =
                            x.IsPublished
                    })
                .ToList();

        var report =
            new StudentResultReportDto
            {
                StudentId =
                    student.Id,

                IndexNumber =
                    student.IndexNumber,

                FullName =
                    student.FullName,

                AcademicYearId =
                    academicYear.Id,

                AcademicYearName =
                    academicYear.Name,

                AcademicTermId =
                    academicTerm.Id,

                AcademicTermName =
                    academicTerm.Name,

                TotalResults =
                    results.Count,

                Results =
                    results
            };

        return Ok(report);
    }

    // ============================================================
    // STUDENT RESULT PDF REPORT
    // ============================================================

    [HttpGet("students/{studentId:int}/results/pdf")]
    public async Task<IActionResult> GetStudentResultReportPdf(
        int studentId,
        int academicYearId,
        int academicTermId)
    {
        var student =
            await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == studentId);

        if (student == null)
        {
            return NotFound(new
            {
                message = "Student not found."
            });
        }

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicYearId);

        if (academicYear == null)
        {
            return NotFound(new
            {
                message = "Academic year not found."
            });
        }

        var academicTerm =
            await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicTermId &&
                    x.AcademicYearId ==
                        academicYearId);

        if (academicTerm == null)
        {
            return NotFound(new
            {
                message =
                    "Academic term not found for the selected academic year."
            });
        }

        var marks =
            await _context.StudentMarks
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsPublished &&
                    x.Exam.AcademicTermId ==
                        academicTermId &&
                    x.TeacherAssignment
                        .AcademicYearId ==
                        academicYearId)
                .OrderBy(x =>
                    x.Exam.Name)
                .ThenBy(x =>
                    x.TeacherAssignment
                        .Subject.Name)
                .Select(x =>
                    new
                    {
                        x.ExamId,

                        ExamName =
                            x.Exam.Name,

                        SubjectId =
                            x.TeacherAssignment
                                .SubjectId,

                        SubjectName =
                            x.TeacherAssignment
                                .Subject.Name,

                        x.MarksObtained,

                        MaximumMarks =
                            x.Exam.MaximumMarks,

                        x.IsPublished
                    })
                .ToListAsync();

        var results =
            marks
                .Select(x =>
                    new StudentResultReportItemDto
                    {
                        ExamId =
                            x.ExamId,

                        ExamName =
                            x.ExamName,

                        SubjectId =
                            x.SubjectId,

                        SubjectName =
                            x.SubjectName,

                        MarksObtained =
                            x.MarksObtained,

                        MaximumMarks =
                            x.MaximumMarks,

                        Percentage =
                            x.MaximumMarks <= 0
                                ? 0
                                : Math.Round(
                                    x.MarksObtained /
                                    x.MaximumMarks * 100,
                                    2),

                        IsPublished =
                            x.IsPublished
                    })
                .ToList();

        var report =
            new StudentResultReportDto
            {
                StudentId =
                    student.Id,

                IndexNumber =
                    student.IndexNumber,

                FullName =
                    student.FullName,

                AcademicYearId =
                    academicYear.Id,

                AcademicYearName =
                    academicYear.Name,

                AcademicTermId =
                    academicTerm.Id,

                AcademicTermName =
                    academicTerm.Name,

                TotalResults =
                    results.Count,

                Results =
                    results
            };

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

        var document =
            new StudentResultReportPdfDocument(
                report,
                logoPath);

        var pdfBytes =
            document.GeneratePdf();

        var fileName =
            $"Student-Result-" +
            $"{student.IndexNumber}-" +
            $"{academicYear.Name.Replace("/", "-")}-" +
            $"{academicTerm.Name.Replace(" ", "-")}.pdf";

        return File(
            pdfBytes,
            "application/pdf",
            fileName);
    }

    // ============================================================
    // CLASS RESULT REPORT
    // ============================================================

    [HttpGet("classes/{schoolClassId:int}/results")]
    public async Task<IActionResult> GetClassResultReport(
        int schoolClassId,
        int academicYearId,
        int academicTermId,
        int examId)
    {
        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicYearId);

        if (academicYear == null)
        {
            return NotFound(new
            {
                message = "Academic year not found."
            });
        }

        var academicTerm =
            await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicTermId &&
                    x.AcademicYearId == academicYearId);

        if (academicTerm == null)
        {
            return NotFound(new
            {
                message =
                    "Academic term not found for the selected academic year."
            });
        }

        var exam =
            await _context.Exams
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == examId &&
                    x.AcademicTermId == academicTermId);

        if (exam == null)
        {
            return NotFound(new
            {
                message =
                    "Exam not found for the selected academic term."
            });
        }

        var schoolClass =
            await _context.SchoolClasses
                .AsNoTracking()
                .Include(x => x.Grade)
                    .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == schoolClassId);

        if (schoolClass == null)
        {
            return NotFound(new
            {
                message = "School class not found."
            });
        }

        // ========================================================
        // STUDENTS IN CLASS
        // ========================================================

        var students =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.AcademicYearId == academicYearId &&
                    x.SchoolClassId == schoolClassId &&
                    x.IsActive)
                .Select(x => new
                {
                    x.StudentId,
                    x.Student.IndexNumber,
                    x.Student.FullName
                })
                .OrderBy(x =>
                    x.FullName)
                .ToListAsync();

        var studentIds =
            students
                .Select(x =>
                    x.StudentId)
                .ToList();

        // ========================================================
        // PUBLISHED MARKS
        // ========================================================

        var marks =
            await _context.StudentMarks
                .AsNoTracking()
                .Where(x =>
                    studentIds.Contains(x.StudentId) &&
                    x.ExamId == examId &&
                    x.IsPublished &&
                    x.TeacherAssignment.AcademicYearId ==
                        academicYearId &&
                    x.TeacherAssignment.SchoolClassId ==
                        schoolClassId)
                .Select(x => new
                {
                    x.StudentId,

                    SubjectId =
                        x.TeacherAssignment.SubjectId,

                    SubjectName =
                        x.TeacherAssignment.Subject.Name,

                    x.MarksObtained,

                    MaximumMarks =
                        x.Exam.MaximumMarks
                })
                .OrderBy(x =>
                    x.SubjectName)
                .ToListAsync();

        // ========================================================
        // BUILD STUDENT RESULT ROWS
        // ========================================================

        var reportStudents =
            students
                .Select(student =>
                {
                    var studentMarks =
                        marks
                            .Where(x =>
                                x.StudentId ==
                                    student.StudentId)
                            .ToList();

                    var subjectResults =
                        studentMarks
                            .Select(mark =>
                                new ClassResultSubjectDto
                                {
                                    SubjectId =
                                        mark.SubjectId,

                                    SubjectName =
                                        mark.SubjectName,

                                    MarksObtained =
                                        mark.MarksObtained,

                                    MaximumMarks =
                                        mark.MaximumMarks,

                                    Percentage =
                                        mark.MaximumMarks <= 0
                                            ? 0
                                            : Math.Round(
                                                mark.MarksObtained /
                                                mark.MaximumMarks * 100,
                                                2)
                                })
                            .ToList();

                    var totalMarksObtained =
                        subjectResults
                            .Sum(x =>
                                x.MarksObtained);

                    var totalMaximumMarks =
                        subjectResults
                            .Sum(x =>
                                x.MaximumMarks);

                    var overallPercentage =
                        totalMaximumMarks <= 0
                            ? 0
                            : Math.Round(
                                totalMarksObtained /
                                totalMaximumMarks * 100,
                                2);

                    return new ClassResultStudentDto
                    {
                        StudentId =
                            student.StudentId,

                        IndexNumber =
                            student.IndexNumber,

                        FullName =
                            student.FullName,

                        PublishedSubjectCount =
                            subjectResults.Count,

                        TotalMarksObtained =
                            totalMarksObtained,

                        TotalMaximumMarks =
                            totalMaximumMarks,

                        OverallPercentage =
                            overallPercentage,

                        Subjects =
                            subjectResults
                    };
                })
                .ToList();

        // ========================================================
        // FINAL REPORT
        // ========================================================

        var report =
            new ClassResultReportDto
            {
                AcademicYearId =
                    academicYear.Id,

                AcademicYearName =
                    academicYear.Name,

                AcademicTermId =
                    academicTerm.Id,

                AcademicTermName =
                    academicTerm.Name,

                ExamId =
                    exam.Id,

                ExamName =
                    exam.Name,

                MaximumMarks =
                    exam.MaximumMarks,

                SchoolClassId =
                    schoolClass.Id,

                ClassName =
                    schoolClass.Name,

                GradeName =
                    schoolClass.Grade.Name,

                SectionName =
                    schoolClass.Grade.Section.Name,

                TotalStudents =
                    reportStudents.Count,

                Students =
                    reportStudents
            };

        return Ok(report);
    }

    // ============================================================
    // CLASS RESULT PDF REPORT
    // ============================================================

    [HttpGet("classes/{schoolClassId:int}/results/pdf")]
    public async Task<IActionResult> GetClassResultReportPdf(
        int schoolClassId,
        int academicYearId,
        int academicTermId,
        int examId)
    {
        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicYearId);

        if (academicYear == null)
        {
            return NotFound(new
            {
                message = "Academic year not found."
            });
        }

        var academicTerm =
            await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicTermId &&
                    x.AcademicYearId == academicYearId);

        if (academicTerm == null)
        {
            return NotFound(new
            {
                message =
                    "Academic term not found for the selected academic year."
            });
        }

        var exam =
            await _context.Exams
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == examId &&
                    x.AcademicTermId == academicTermId);

        if (exam == null)
        {
            return NotFound(new
            {
                message =
                    "Exam not found for the selected academic term."
            });
        }

        var schoolClass =
            await _context.SchoolClasses
                .AsNoTracking()
                .Include(x => x.Grade)
                    .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == schoolClassId);

        if (schoolClass == null)
        {
            return NotFound(new
            {
                message = "School class not found."
            });
        }

        var students =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.AcademicYearId == academicYearId &&
                    x.SchoolClassId == schoolClassId &&
                    x.IsActive)
                .Select(x => new
                {
                    x.StudentId,
                    x.Student.IndexNumber,
                    x.Student.FullName
                })
                .OrderBy(x =>
                    x.FullName)
                .ToListAsync();

        var studentIds =
            students
                .Select(x =>
                    x.StudentId)
                .ToList();

        var marks =
            await _context.StudentMarks
                .AsNoTracking()
                .Where(x =>
                    studentIds.Contains(x.StudentId) &&
                    x.ExamId == examId &&
                    x.IsPublished &&
                    x.TeacherAssignment.AcademicYearId ==
                        academicYearId &&
                    x.TeacherAssignment.SchoolClassId ==
                        schoolClassId)
                .Select(x => new
                {
                    x.StudentId,

                    SubjectId =
                        x.TeacherAssignment.SubjectId,

                    SubjectName =
                        x.TeacherAssignment.Subject.Name,

                    x.MarksObtained,

                    MaximumMarks =
                        x.Exam.MaximumMarks
                })
                .OrderBy(x =>
                    x.SubjectName)
                .ToListAsync();

        var reportStudents =
            students
                .Select(student =>
                {
                    var studentMarks =
                        marks
                            .Where(x =>
                                x.StudentId ==
                                    student.StudentId)
                            .ToList();

                    var subjectResults =
                        studentMarks
                            .Select(mark =>
                                new ClassResultSubjectDto
                                {
                                    SubjectId =
                                        mark.SubjectId,

                                    SubjectName =
                                        mark.SubjectName,

                                    MarksObtained =
                                        mark.MarksObtained,

                                    MaximumMarks =
                                        mark.MaximumMarks,

                                    Percentage =
                                        mark.MaximumMarks <= 0
                                            ? 0
                                            : Math.Round(
                                                mark.MarksObtained /
                                                mark.MaximumMarks * 100,
                                                2)
                                })
                            .ToList();

                    var totalMarksObtained =
                        subjectResults
                            .Sum(x =>
                                x.MarksObtained);

                    var totalMaximumMarks =
                        subjectResults
                            .Sum(x =>
                                x.MaximumMarks);

                    var overallPercentage =
                        totalMaximumMarks <= 0
                            ? 0
                            : Math.Round(
                                totalMarksObtained /
                                totalMaximumMarks * 100,
                                2);

                    return new ClassResultStudentDto
                    {
                        StudentId =
                            student.StudentId,

                        IndexNumber =
                            student.IndexNumber,

                        FullName =
                            student.FullName,

                        PublishedSubjectCount =
                            subjectResults.Count,

                        TotalMarksObtained =
                            totalMarksObtained,

                        TotalMaximumMarks =
                            totalMaximumMarks,

                        OverallPercentage =
                            overallPercentage,

                        Subjects =
                            subjectResults
                    };
                })
                .ToList();

        var report =
            new ClassResultReportDto
            {
                AcademicYearId =
                    academicYear.Id,

                AcademicYearName =
                    academicYear.Name,

                AcademicTermId =
                    academicTerm.Id,

                AcademicTermName =
                    academicTerm.Name,

                ExamId =
                    exam.Id,

                ExamName =
                    exam.Name,

                MaximumMarks =
                    exam.MaximumMarks,

                SchoolClassId =
                    schoolClass.Id,

                ClassName =
                    schoolClass.Name,

                GradeName =
                    schoolClass.Grade.Name,

                SectionName =
                    schoolClass.Grade.Section.Name,

                TotalStudents =
                    reportStudents.Count,

                Students =
                    reportStudents
            };

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

        var document =
            new ClassResultReportPdfDocument(
                report,
                logoPath);

        var pdfBytes =
            document.GeneratePdf();

        var fileName =
            $"Class-Result-" +
            $"{academicYear.Name.Replace("/", "-")}-" +
            $"{academicTerm.Name.Replace(" ", "-")}-" +
            $"{exam.Name.Replace(" ", "-")}-" +
            $"Grade-{schoolClass.Grade.Name}-" +
            $"Class-{schoolClass.Name}.pdf";

        return File(
            pdfBytes,
            "application/pdf",
            fileName);
    }
}