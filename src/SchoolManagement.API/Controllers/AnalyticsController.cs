using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AnalyticsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }


    // ============================================================
    // ANALYTICS OVERVIEW
    //
    // Optional filters:
    //
    // academicYearId
    // termId
    // examId
    // sectionId
    // gradeId
    // classId
    // subjectId
    //
    // Role Scope:
    //
    // Admin / Principal / Deputy Principal
    //      → Whole School
    //
    // Section Head
    //      → Assigned Sections
    //
    // Teacher
    //      → Own Teacher Assignments
    //
    // Only Published Results are included.
    // ============================================================

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] int? academicYearId,
        [FromQuery] int? termId,
        [FromQuery] int? examId,
        [FromQuery] int? sectionId,
        [FromQuery] int? gradeId,
        [FromQuery] int? classId,
        [FromQuery] int? subjectId)
    {
        // --------------------------------------------------------
        // Get logged-in user
        // --------------------------------------------------------

        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }


        // --------------------------------------------------------
        // Find Identity User
        // --------------------------------------------------------

        var applicationUser =
            await _userManager.FindByIdAsync(
                userId);

        if (applicationUser == null ||
            !applicationUser.IsActive)
        {
            return Unauthorized();
        }


        // --------------------------------------------------------
        // Get Roles
        // --------------------------------------------------------

        var roles =
            await _userManager
                .GetRolesAsync(
                    applicationUser);


        var isGlobalUser =
            roles.Contains(
                "Admin",
                StringComparer.OrdinalIgnoreCase)
            ||
            roles.Contains(
                "Principal",
                StringComparer.OrdinalIgnoreCase)
            ||
            roles.Contains(
                "Deputy Principal",
                StringComparer.OrdinalIgnoreCase);


        var isSectionHead =
            roles.Contains(
                "Section Head",
                StringComparer.OrdinalIgnoreCase);


        var isTeacher =
            roles.Contains(
                "Teacher",
                StringComparer.OrdinalIgnoreCase);


        // --------------------------------------------------------
        // Find Staff Profile
        //
        // Global users may not necessarily require Staff profile.
        // Section Head / Teacher must have one.
        // --------------------------------------------------------

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);


        if (!isGlobalUser &&
            staff == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active staff profile is linked to this account."
                });
        }


        // --------------------------------------------------------
        // Determine Section Head Scope
        // --------------------------------------------------------

        var allowedSectionIds =
            new List<int>();


        if (!isGlobalUser &&
            isSectionHead &&
            staff != null)
        {
            var sectionAssignments =
                _context.SectionHeadAssignments
                    .Where(x =>
                        x.StaffId == staff.Id &&
                        x.IsActive);


            // If Academic Year filter was supplied,
            // Section Head assignment must match it.
            if (academicYearId.HasValue)
            {
                sectionAssignments =
                    sectionAssignments.Where(x =>
                        x.AcademicYearId ==
                            academicYearId.Value);
            }


            allowedSectionIds =
                await sectionAssignments
                    .Select(x =>
                        x.SectionId)
                    .Distinct()
                    .ToListAsync();
        }


        // --------------------------------------------------------
        // Determine Teacher Scope
        // --------------------------------------------------------

        var allowedTeacherAssignmentIds =
            new List<int>();


        if (!isGlobalUser &&
            !isSectionHead &&
            isTeacher &&
            staff != null)
        {
            var teacherAssignments =
                _context.TeacherAssignments
                    .Where(x =>
                        x.StaffId == staff.Id &&
                        x.IsActive);


            if (academicYearId.HasValue)
            {
                teacherAssignments =
                    teacherAssignments.Where(x =>
                        x.AcademicYearId ==
                            academicYearId.Value);
            }


            allowedTeacherAssignmentIds =
                await teacherAssignments
                    .Select(x =>
                        x.Id)
                    .Distinct()
                    .ToListAsync();
        }


        // --------------------------------------------------------
        // User has no supported analytics role
        // --------------------------------------------------------

        if (!isGlobalUser &&
            !isSectionHead &&
            !isTeacher)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have access to academic analytics."
                });
        }


        // --------------------------------------------------------
        // Section Head has no assigned sections
        // --------------------------------------------------------

        if (!isGlobalUser &&
            isSectionHead &&
            allowedSectionIds.Count == 0)
        {
            return Ok(new
            {
                scope = new
                {
                    type =
                        "SectionHead",

                    sections =
                        Array.Empty<int>()
                },

                filters = new
                {
                    academicYearId,
                    termId,
                    examId,
                    sectionId,
                    gradeId,
                    classId,
                    subjectId
                },

                overview = new
                {
                    totalStudents = 0,

                    publishedResultCount = 0,

                    averagePercentage = 0,

                    highestPercentage = 0,

                    lowestPercentage = 0
                }
            });
        }


        // --------------------------------------------------------
        // Teacher has no assignments
        // --------------------------------------------------------

        if (!isGlobalUser &&
            !isSectionHead &&
            isTeacher &&
            allowedTeacherAssignmentIds.Count == 0)
        {
            return Ok(new
            {
                scope = new
                {
                    type =
                        "Teacher",

                    teacherAssignments =
                        Array.Empty<int>()
                },

                filters = new
                {
                    academicYearId,
                    termId,
                    examId,
                    sectionId,
                    gradeId,
                    classId,
                    subjectId
                },

                overview = new
                {
                    totalStudents = 0,

                    publishedResultCount = 0,

                    averagePercentage = 0,

                    highestPercentage = 0,

                    lowestPercentage = 0
                }
            });
        }


        // --------------------------------------------------------
        // Base Published Marks Query
        //
        // We join StudentMark → MarksSubmission
        // so analytics only includes Published submissions.
        // --------------------------------------------------------

        var query =
            from mark in _context.StudentMarks

            join submission
                in _context.MarksSubmissions

                on new
                {
                    mark.ExamId,
                    mark.TeacherAssignmentId
                }

                equals new
                {
                    submission.ExamId,
                    submission.TeacherAssignmentId
                }

            where
                mark.IsPublished &&

                submission.Status ==
                    MarksSubmissionStatus.Published &&

                mark.Student.IsActive &&

                mark.TeacherAssignment.IsActive

            select new
            {
                StudentId =
                    mark.StudentId,

                MarkId =
                    mark.Id,

                MarksObtained =
                    mark.MarksObtained,

                MaximumMarks =
                    mark.Exam.MaximumMarks,

                AcademicYearId =
                    mark.TeacherAssignment
                        .AcademicYearId,

                TermId =
                    mark.Exam
                        .AcademicTermId,

                ExamId =
                    mark.ExamId,

                SectionId =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .SectionId,

                GradeId =
                    mark.TeacherAssignment
                        .SchoolClass
                        .GradeId,

                ClassId =
                    mark.TeacherAssignment
                        .SchoolClassId,

                SubjectId =
                    mark.TeacherAssignment
                        .SubjectId,

                TeacherAssignmentId =
                    mark.TeacherAssignmentId,

                StaffId =
                    mark.TeacherAssignment
                        .StaffId
            };


        // ========================================================
        // ROLE SCOPE
        // ========================================================


        // --------------------------------------------------------
        // Section Head Scope
        // --------------------------------------------------------

        if (!isGlobalUser &&
            isSectionHead)
        {
            query =
                query.Where(x =>
                    allowedSectionIds
                        .Contains(
                            x.SectionId));
        }


        // --------------------------------------------------------
        // Teacher Scope
        //
        // Only own assignments.
        // --------------------------------------------------------

        if (!isGlobalUser &&
            !isSectionHead &&
            isTeacher)
        {
            query =
                query.Where(x =>
                    allowedTeacherAssignmentIds
                        .Contains(
                            x.TeacherAssignmentId));
        }


        // ========================================================
        // USER FILTERS
        // ========================================================


        // --------------------------------------------------------
        // Academic Year
        // --------------------------------------------------------

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                        academicYearId.Value);
        }


        // --------------------------------------------------------
        // Term
        // --------------------------------------------------------

        if (termId.HasValue)
        {
            query =
                query.Where(x =>
                    x.TermId ==
                        termId.Value);
        }


        // --------------------------------------------------------
        // Exam
        // --------------------------------------------------------

        if (examId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ExamId ==
                        examId.Value);
        }


        // --------------------------------------------------------
        // Section
        // --------------------------------------------------------

        if (sectionId.HasValue)
        {
            // Section Head cannot request another section.
            if (!isGlobalUser &&
                isSectionHead &&
                !allowedSectionIds.Contains(
                    sectionId.Value))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message =
                            "You do not have access to this section."
                    });
            }


            query =
                query.Where(x =>
                    x.SectionId ==
                        sectionId.Value);
        }


        // --------------------------------------------------------
        // Grade
        // --------------------------------------------------------

        if (gradeId.HasValue)
        {
            query =
                query.Where(x =>
                    x.GradeId ==
                        gradeId.Value);
        }


        // --------------------------------------------------------
        // Class
        // --------------------------------------------------------

        if (classId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ClassId ==
                        classId.Value);
        }


        // --------------------------------------------------------
        // Subject
        // --------------------------------------------------------

        if (subjectId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SubjectId ==
                        subjectId.Value);
        }


        // --------------------------------------------------------
        // Execute Query
        // --------------------------------------------------------

        var data =
            await query
                .ToListAsync();


        // --------------------------------------------------------
        // Published Result Count
        //
        // One StudentMark = one subject result.
        // --------------------------------------------------------

        var publishedResultCount =
            data.Count;


        // --------------------------------------------------------
        // Number of Students represented
        // in the current filtered analytics.
        // --------------------------------------------------------

        var totalStudents =
            data
                .Select(x =>
                    x.StudentId)
                .Distinct()
                .Count();


        // --------------------------------------------------------
        // Calculate Percentage For Each Published Mark
        // --------------------------------------------------------

        var percentages =
            data
                .Where(x =>
                    x.MaximumMarks > 0)

                .Select(x =>
                    Math.Round(
                        x.MarksObtained /
                        x.MaximumMarks *
                        100,
                        2))

                .ToList();


        // --------------------------------------------------------
        // Average Percentage
        // --------------------------------------------------------

        var averagePercentage =
            percentages.Count > 0
                ? Math.Round(
                    percentages.Average(),
                    2)
                : 0;


        // --------------------------------------------------------
        // Highest Percentage
        // --------------------------------------------------------

        var highestPercentage =
            percentages.Count > 0
                ? percentages.Max()
                : 0;


        // --------------------------------------------------------
        // Lowest Percentage
        // --------------------------------------------------------

        var lowestPercentage =
            percentages.Count > 0
                ? percentages.Min()
                : 0;


        // --------------------------------------------------------
        // Determine Scope Name
        // --------------------------------------------------------

        string scopeType;


        if (isGlobalUser)
        {
            scopeType =
                "WholeSchool";
        }
        else if (isSectionHead)
        {
            scopeType =
                "SectionHead";
        }
        else
        {
            scopeType =
                "Teacher";
        }


        // --------------------------------------------------------
        // Response
        // --------------------------------------------------------

        return Ok(new
        {
            scope = new
            {
                type =
                    scopeType,

                staffId =
                    staff?.Id,

                sections =
                    isSectionHead
                        ? allowedSectionIds
                        : null,

                teacherAssignments =
                    isTeacher &&
                    !isSectionHead
                        ? allowedTeacherAssignmentIds
                        : null
            },

            filters = new
            {
                academicYearId,
                termId,
                examId,
                sectionId,
                gradeId,
                classId,
                subjectId
            },

            overview = new
            {
                totalStudents,

                publishedResultCount,

                averagePercentage,

                highestPercentage,

                lowestPercentage
            }
        });
    }
}