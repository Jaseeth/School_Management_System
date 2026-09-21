using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Dashboard.DTOs;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using SchoolManagement.Infrastructure.Identity;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }


    // ============================================================
    // ADMIN DASHBOARD SUMMARY
    // ============================================================

    [HttpGet("admin-summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAdminSummary()
    {
        var today =
            DateOnly.FromDateTime(
                DateTime.Now);


        // ========================================================
        // ACTIVE ACADEMIC YEAR
        // ========================================================

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .Where(x =>
                    x.IsActive)
                .OrderByDescending(x =>
                    x.Id)
                .FirstOrDefaultAsync();

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "No active academic year found."
            });
        }


        // ========================================================
        // ACTIVE ACADEMIC TERM
        // ========================================================

        var academicTerm =
            await _context.AcademicTerms
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        academicYear.Id)
                .OrderBy(x =>
                    x.Id)
                .FirstOrDefaultAsync();


        // ========================================================
        // BASIC COUNTS
        // ========================================================

        var totalStudents =
            await _context.Students
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        var totalStaff =
            await _context.Staff
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        var totalSections =
            await _context.Sections
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        var totalGrades =
            await _context.Grades
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        var totalClasses =
            await _context.SchoolClasses
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        var totalSubjects =
            await _context.Subjects
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        // ========================================================
        // TODAY'S NORMAL TIMETABLE
        // ========================================================

        var todaySchoolDay =
            (SchoolDay)(int)today.DayOfWeek;


        var todayTimetableCount =
            0;


        if (academicTerm != null)
        {
            todayTimetableCount =
                await _context.TimetableEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.IsActive &&
                        x.AcademicYearId ==
                            academicYear.Id &&
                        x.AcademicTermId ==
                            academicTerm.Id &&
                        x.Day ==
                            todaySchoolDay);
        }


        // ========================================================
        // TODAY'S SPECIAL CLASSES
        // ========================================================

        var todaySpecialClassCount =
            await _context.SpecialClassSessions
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.ClassDate ==
                        today);


        // ========================================================
        // UPCOMING SPECIAL CLASSES
        // ========================================================

        var upcomingSpecialClassCount =
            await _context.SpecialClassSessions
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.ClassDate >
                        today);


        // ========================================================
        // PENDING LEAVE REQUESTS
        // ========================================================

        var pendingLeaveRequestCount =
            await _context.StaffLeaveRequests
                .AsNoTracking()
                .CountAsync(x =>
                    x.Status ==
                        StaffLeaveStatus.Pending);


        // ========================================================
        // RESPONSE
        // ========================================================

        var response =
            new AdminDashboardSummaryDto
            {
                TotalStudents =
                    totalStudents,

                TotalStaff =
                    totalStaff,

                TotalSections =
                    totalSections,

                TotalGrades =
                    totalGrades,

                TotalClasses =
                    totalClasses,

                TotalSubjects =
                    totalSubjects,

                TodayTimetableCount =
                    todayTimetableCount,

                TodaySpecialClassCount =
                    todaySpecialClassCount,

                UpcomingSpecialClassCount =
                    upcomingSpecialClassCount,

                PendingLeaveRequestCount =
                    pendingLeaveRequestCount,

                ActiveAcademicYearId =
                    academicYear.Id,

                ActiveAcademicYearName =
                    academicYear.Name,

                ActiveAcademicTermId =
                    academicTerm?.Id,

                ActiveAcademicTermName =
                    academicTerm?.Name
            };


        return Ok(response);
    }

    // ============================================================
    // TEACHER DASHBOARD SUMMARY
    // ============================================================

    [HttpGet("teacher-summary")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> GetTeacherSummary()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }


        var staff =
            await _context.Staff
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (staff == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active staff record."
            });
        }


        var today =
            DateOnly.FromDateTime(
                DateTime.Now);


        // ========================================================
        // ACTIVE ACADEMIC YEAR
        // ========================================================

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .Where(x =>
                    x.IsActive)
                .OrderByDescending(x =>
                    x.Id)
                .FirstOrDefaultAsync();

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "No active academic year found."
            });
        }


        // ========================================================
        // ACTIVE ACADEMIC TERM
        // ========================================================

        var academicTerm =
            await _context.AcademicTerms
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        academicYear.Id)
                .OrderBy(x =>
                    x.Id)
                .FirstOrDefaultAsync();


        // ========================================================
        // ASSIGNED CLASSES
        // ========================================================

        var assignedClassCount =
            await _context.TeacherAssignments
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.StaffId ==
                        staff.Id &&
                    x.AcademicYearId ==
                        academicYear.Id)
                .Select(x =>
                    x.SchoolClassId)
                .Distinct()
                .CountAsync();


        // ========================================================
        // ASSIGNED SUBJECTS
        // ========================================================

        var assignedSubjectCount =
            await _context.TeacherAssignments
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.StaffId ==
                        staff.Id &&
                    x.AcademicYearId ==
                        academicYear.Id)
                .Select(x =>
                    x.SubjectId)
                .Distinct()
                .CountAsync();


        // ========================================================
        // TODAY TIMETABLE
        // ========================================================

        var todayTimetableCount = 0;

        if (academicTerm != null)
        {
            var schoolDay =
                (SchoolDay)(int)today.DayOfWeek;

            todayTimetableCount =
                await _context.TimetableEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.IsActive &&
                        x.AcademicYearId ==
                            academicYear.Id &&
                        x.AcademicTermId ==
                            academicTerm.Id &&
                        x.StaffId ==
                            staff.Id &&
                        x.Day ==
                            schoolDay);
        }


        // ========================================================
        // TODAY SPECIAL CLASSES
        // ========================================================

        var todaySpecialClassCount =
            await _context.SpecialClassSessions
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.StaffId ==
                        staff.Id &&
                    x.ClassDate ==
                        today);


        // ========================================================
        // UPCOMING SPECIAL CLASSES
        // ========================================================

        var upcomingSpecialClassCount =
            await _context.SpecialClassSessions
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.StaffId ==
                        staff.Id &&
                    x.ClassDate >
                        today);


        // ========================================================
        // PENDING LEAVE REQUESTS
        // ========================================================

        var pendingLeaveRequestCount =
            await _context.StaffLeaveRequests
                .AsNoTracking()
                .CountAsync(x =>
                    x.StaffId ==
                        staff.Id &&
                    x.Status ==
                        StaffLeaveStatus.Pending);


        // ========================================================
        // UNREAD NOTIFICATIONS
        // ========================================================

        var unreadNotificationCount =
            await _context.Notifications
                .AsNoTracking()
                .CountAsync(x =>
                    x.RecipientStaffId ==
                        staff.Id &&
                    !x.IsRead);


        // ========================================================
        // RESPONSE
        // ========================================================

        var response =
            new TeacherDashboardSummaryDto
            {
                StaffId =
                    staff.Id,

                StaffNumber =
                    staff.StaffNumber,

                TeacherName =
                    staff.FullName,

                AssignedClassCount =
                    assignedClassCount,

                AssignedSubjectCount =
                    assignedSubjectCount,

                TodayTimetableCount =
                    todayTimetableCount,

                TodaySpecialClassCount =
                    todaySpecialClassCount,

                UpcomingSpecialClassCount =
                    upcomingSpecialClassCount,

                PendingLeaveRequestCount =
                    pendingLeaveRequestCount,

                UnreadNotificationCount =
                    unreadNotificationCount,

                ActiveAcademicYearId =
                    academicYear.Id,

                ActiveAcademicYearName =
                    academicYear.Name,

                ActiveAcademicTermId =
                    academicTerm?.Id,

                ActiveAcademicTermName =
                    academicTerm?.Name
            };


        return Ok(response);
    }

    // ============================================================
    // SECTION HEAD DASHBOARD SUMMARY
    // ============================================================

    [HttpGet("section-head-summary")]
    [Authorize(Roles = "Section Head")]
    public async Task<IActionResult> GetSectionHeadSummary()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }


        var staff =
            await _context.Staff
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (staff == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active staff record."
            });
        }


        // ========================================================
        // ACTIVE ACADEMIC YEAR
        // ========================================================

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .Where(x =>
                    x.IsActive)
                .OrderByDescending(x =>
                    x.Id)
                .FirstOrDefaultAsync();

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "No active academic year found."
            });
        }


        // ========================================================
        // ACTIVE ACADEMIC TERM
        // ========================================================

        var academicTerm =
            await _context.AcademicTerms
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        academicYear.Id)
                .OrderBy(x =>
                    x.Id)
                .FirstOrDefaultAsync();


        // ========================================================
        // SECTION HEAD ASSIGNMENT
        // ========================================================

        var sectionHeadAssignment =
            await _context.SectionHeadAssignments
                .AsNoTracking()
                .Where(x =>
                    x.StaffId ==
                        staff.Id &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.IsActive)
                .Select(x => new
                {
                    x.SectionId,
                    SectionName =
                        x.Section.Name
                })
                .FirstOrDefaultAsync();

        if (sectionHeadAssignment == null)
        {
            return BadRequest(new
            {
                message =
                    "No active section-head assignment found for this academic year."
            });
        }


        var sectionId =
            sectionHeadAssignment.SectionId;


        var today =
            DateOnly.FromDateTime(
                DateTime.Now);

        var schoolDay =
            (SchoolDay)(int)today.DayOfWeek;


        // ========================================================
        // TOTAL STUDENTS IN SECTION
        // ========================================================

        var totalStudents =
            await _context.Students
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                    sectionId);


        // ========================================================
        // TOTAL CLASSES IN SECTION
        // ========================================================

        var totalClasses =
            await _context.SchoolClasses
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.Grade.SectionId ==
                        sectionId);


        // ========================================================
        // TOTAL TEACHERS IN SECTION
        // ========================================================

        var totalTeachers =
            await _context.TeacherAssignments
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                        sectionId)
                .Select(x =>
                    x.StaffId)
                .Distinct()
                .CountAsync();


        // ========================================================
        // TODAY TIMETABLE
        // ========================================================

        var todayTimetableCount = 0;

        if (academicTerm != null)
        {
            todayTimetableCount =
                await _context.TimetableEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.IsActive &&
                        x.AcademicYearId ==
                            academicYear.Id &&
                        x.AcademicTermId ==
                            academicTerm.Id &&
                        x.Day ==
                            schoolDay &&
                        x.SchoolClass
                            .Grade
                            .SectionId ==
                            sectionId);
        }


        // ========================================================
        // TODAY SPECIAL CLASSES
        // ========================================================

        var todaySpecialClassCount =
            await _context.SpecialClassSessions
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.ClassDate ==
                        today &&
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                        sectionId);


        // ========================================================
        // UPCOMING SPECIAL CLASSES
        // ========================================================

        var upcomingSpecialClassCount =
            await _context.SpecialClassSessions
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.ClassDate >
                        today &&
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                        sectionId);


        // ========================================================
        // PENDING LEAVE REQUESTS IN SECTION
        //
        // Count staff assigned to classes inside this section
        // ========================================================

        var sectionStaffIds =
            _context.TeacherAssignments
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                        sectionId)
                .Select(x =>
                    x.StaffId)
                .Distinct();


        var pendingLeaveRequestCount =
            await _context.StaffLeaveRequests
                .AsNoTracking()
                .CountAsync(x =>
                    sectionStaffIds.Contains(
                        x.StaffId) &&
                    x.Status ==
                        StaffLeaveStatus.Pending);


        // ========================================================
        // UNREAD NOTIFICATIONS
        // ========================================================

        var unreadNotificationCount =
            await _context.Notifications
                .AsNoTracking()
                .CountAsync(x =>
                    x.RecipientStaffId ==
                        staff.Id &&
                    !x.IsRead);


        // ========================================================
        // RESPONSE
        // ========================================================

        var response =
            new SectionHeadDashboardSummaryDto
            {
                StaffId =
                    staff.Id,

                StaffNumber =
                    staff.StaffNumber,

                SectionHeadName =
                    staff.FullName,

                SectionId =
                    sectionId,

                SectionName =
                    sectionHeadAssignment.SectionName,

                TotalStudents =
                    totalStudents,

                TotalClasses =
                    totalClasses,

                TotalTeachers =
                    totalTeachers,

                TodayTimetableCount =
                    todayTimetableCount,

                TodaySpecialClassCount =
                    todaySpecialClassCount,

                UpcomingSpecialClassCount =
                    upcomingSpecialClassCount,

                PendingLeaveRequestCount =
                    pendingLeaveRequestCount,

                UnreadNotificationCount =
                    unreadNotificationCount,

                ActiveAcademicYearId =
                    academicYear.Id,

                ActiveAcademicYearName =
                    academicYear.Name,

                ActiveAcademicTermId =
                    academicTerm?.Id,

                ActiveAcademicTermName =
                    academicTerm?.Name
            };


        return Ok(response);
    }

    // ============================================================
    // STUDENT DASHBOARD SUMMARY
    // ============================================================

    [HttpGet("student-summary")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetStudentSummary()
    {
        // ========================================================
        // CURRENT USER
        // ========================================================

        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }


        // ========================================================
        // STUDENT
        // ========================================================

        var student =
            await _context.Students
                .AsNoTracking()
                .Where(x =>
                    x.ApplicationUserId ==
                        userId &&
                    x.IsActive)
                .Select(x => new
                {
                    x.Id,
                    x.IndexNumber,
                    x.FullName,
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

        if (student == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active student."
            });
        }


        // ========================================================
        // ACTIVE ACADEMIC YEAR
        // ========================================================

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .Where(x =>
                    x.IsActive)
                .OrderByDescending(x =>
                    x.Id)
                .FirstOrDefaultAsync();

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "No active academic year found."
            });
        }


        // ========================================================
        // ACTIVE ACADEMIC TERM
        // ========================================================

        var academicTerm =
            await _context.AcademicTerms
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        academicYear.Id)
                .OrderBy(x =>
                    x.Id)
                .FirstOrDefaultAsync();


        // ========================================================
        // TODAY
        // ========================================================

        var today =
            DateOnly.FromDateTime(
                DateTime.Now);

        var schoolDay =
            (SchoolDay)(int)today.DayOfWeek;


        // ========================================================
        // TODAY TIMETABLE
        // ========================================================

        var todayTimetableCount =
            0;

        if (academicTerm != null)
        {
            todayTimetableCount =
                await _context.TimetableEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.IsActive &&
                        x.AcademicYearId ==
                            academicYear.Id &&
                        x.AcademicTermId ==
                            academicTerm.Id &&
                        x.SchoolClassId ==
                            student.SchoolClassId &&
                        x.Day ==
                            schoolDay);
        }


        // ========================================================
        // TODAY SPECIAL CLASSES
        // ========================================================

        var todaySpecialClassCount =
            await _context.SpecialClassSessions
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.SchoolClassId ==
                        student.SchoolClassId &&
                    x.ClassDate ==
                        today);


        // ========================================================
        // UPCOMING SPECIAL CLASSES
        // ========================================================

        var upcomingSpecialClassCount =
            await _context.SpecialClassSessions
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.SchoolClassId ==
                        student.SchoolClassId &&
                    x.ClassDate >
                        today);


        // ========================================================
        // UNREAD NOTIFICATIONS
        // ========================================================

        var unreadNotificationCount =
            await _context.Notifications
                .AsNoTracking()
                .CountAsync(x =>
                    x.RecipientStudentId ==
                        student.Id &&
                    !x.IsRead);


        // ========================================================
        // PUBLISHED RESULTS
        // ========================================================

        var publishedResultsQuery =
            _context.StudentMarks
                .AsNoTracking()
                .Where(x =>
                    x.StudentId ==
                        student.Id &&
                    x.IsPublished &&
                    x.TeacherAssignment
                        .AcademicYearId ==
                        academicYear.Id);


        // If there is an active term,
        // only count results belonging to that term.
        if (academicTerm != null)
        {
            publishedResultsQuery =
                publishedResultsQuery.Where(x =>
                    x.Exam.AcademicTermId ==
                        academicTerm.Id);
        }


        var publishedResultCount =
            await publishedResultsQuery
                .CountAsync();


        // ========================================================
        // ATTENDANCE
        // ========================================================

        var attendanceQuery =
            _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    x.StudentId ==
                        student.Id &&
                    x.AcademicYearId ==
                        academicYear.Id);


        var totalAttendanceDays =
            await attendanceQuery
                .CountAsync();


        var presentDays =
            await attendanceQuery
                .CountAsync(x =>
                    x.Status ==
                        AttendanceStatus.Present);


        var absentDays =
            await attendanceQuery
                .CountAsync(x =>
                    x.Status ==
                        AttendanceStatus.Absent);


        var attendancePercentage =
            totalAttendanceDays == 0
                ? 0
                : Math.Round(
                    (double)presentDays /
                    totalAttendanceDays *
                    100,
                    2);


        // ========================================================
        // RESPONSE
        // ========================================================

        var response =
            new StudentDashboardSummaryDto
            {
                StudentId =
                    student.Id,

                IndexNumber =
                    student.IndexNumber,

                StudentName =
                    student.FullName,

                SchoolClassId =
                    student.SchoolClassId,

                ClassName =
                    student.ClassName,

                GradeName =
                    student.GradeName,

                SectionName =
                    student.SectionName,

                TodayTimetableCount =
                    todayTimetableCount,

                TodaySpecialClassCount =
                    todaySpecialClassCount,

                UpcomingSpecialClassCount =
                    upcomingSpecialClassCount,

                UnreadNotificationCount =
                    unreadNotificationCount,

                PublishedResultCount =
                    publishedResultCount,

                TotalAttendanceDays =
                    totalAttendanceDays,

                PresentDays =
                    presentDays,

                AbsentDays =
                    absentDays,

                AttendancePercentage =
                    attendancePercentage,

                ActiveAcademicYearId =
                    academicYear.Id,

                ActiveAcademicYearName =
                    academicYear.Name,

                ActiveAcademicTermId =
                    academicTerm?.Id,

                ActiveAcademicTermName =
                    academicTerm?.Name
            };


        return Ok(response);
    }

    // ============================================================
    // PRINCIPAL / DEPUTY DASHBOARD SUMMARY
    // ============================================================

    [HttpGet("management-summary")]
    [Authorize(Roles = "Principal,Deputy Principal")]
    public async Task<IActionResult> GetManagementSummary()
    {
        var today =
            DateOnly.FromDateTime(
                DateTime.Now);


        // ========================================================
        // ACTIVE ACADEMIC YEAR
        // ========================================================

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .Where(x =>
                    x.IsActive)
                .OrderByDescending(x =>
                    x.Id)
                .FirstOrDefaultAsync();

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "No active academic year found."
            });
        }


        // ========================================================
        // ACTIVE ACADEMIC TERM
        // ========================================================

        var academicTerm =
            await _context.AcademicTerms
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        academicYear.Id)
                .OrderBy(x =>
                    x.Id)
                .FirstOrDefaultAsync();


        // ========================================================
        // SCHOOL COUNTS
        // ========================================================

        var totalStudents =
            await _context.Students
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        var totalStaff =
            await _context.Staff
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        var totalSections =
            await _context.Sections
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        var totalGrades =
            await _context.Grades
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        var totalClasses =
            await _context.SchoolClasses
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        var totalSubjects =
            await _context.Subjects
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);


        // ========================================================
        // TODAY TIMETABLE
        // ========================================================

        var todayTimetableCount =
            0;

        if (academicTerm != null)
        {
            var schoolDay =
                (SchoolDay)(int)today.DayOfWeek;

            todayTimetableCount =
                await _context.TimetableEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.IsActive &&
                        x.AcademicYearId ==
                            academicYear.Id &&
                        x.AcademicTermId ==
                            academicTerm.Id &&
                        x.Day ==
                            schoolDay);
        }


        // ========================================================
        // TODAY SPECIAL CLASSES
        // ========================================================

        var todaySpecialClassCount =
            await _context.SpecialClassSessions
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.ClassDate ==
                        today);


        // ========================================================
        // UPCOMING SPECIAL CLASSES
        // ========================================================

        var upcomingSpecialClassCount =
            await _context.SpecialClassSessions
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYear.Id &&
                    x.ClassDate >
                        today);


        // ========================================================
        // PENDING LEAVE REQUESTS
        // ========================================================

        var pendingLeaveRequestCount =
            await _context.StaffLeaveRequests
                .AsNoTracking()
                .CountAsync(x =>
                    x.Status ==
                        StaffLeaveStatus.Pending);


        // ========================================================
        // PENDING MARK SUBMISSIONS
        // ========================================================

        var pendingMarksReviewCount =
            await _context.MarksSubmissions
                .AsNoTracking()
                .CountAsync(x =>
                    x.Status ==
                        MarksSubmissionStatus.Submitted);


        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(new
        {
            totalStudents,

            totalStaff,

            totalSections,

            totalGrades,

            totalClasses,

            totalSubjects,

            todayTimetableCount,

            todaySpecialClassCount,

            upcomingSpecialClassCount,

            pendingLeaveRequestCount,

            pendingMarksReviewCount,

            activeAcademicYearId =
                academicYear.Id,

            activeAcademicYearName =
                academicYear.Name,

            activeAcademicTermId =
                academicTerm?.Id,

            activeAcademicTermName =
                academicTerm?.Name
        });
    }
}