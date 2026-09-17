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

    // ============================================================
    // TOP STUDENTS
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
    // limit
    //
    // Role scope:
    //
    // Admin / Principal / Deputy Principal
    //      → Whole School
    //
    // Section Head
    //      → Assigned Sections Only
    //
    // Teacher
    //      → Own Teacher Assignments Only
    //
    // Only Published Results are included.
    // ============================================================

    [HttpGet("top-students")]
    public async Task<IActionResult> GetTopStudents(
        [FromQuery] int? academicYearId,
        [FromQuery] int? termId,
        [FromQuery] int? examId,
        [FromQuery] int? sectionId,
        [FromQuery] int? gradeId,
        [FromQuery] int? classId,
        [FromQuery] int? subjectId,
        [FromQuery] int limit = 10)
    {
        // --------------------------------------------------------
        // Validate limit
        // --------------------------------------------------------

        if (limit <= 0)
        {
            limit = 10;
        }

        if (limit > 100)
        {
            limit = 100;
        }

        // --------------------------------------------------------
        // Logged-in user
        // --------------------------------------------------------

        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var applicationUser =
            await _userManager.FindByIdAsync(
                userId);

        if (applicationUser == null ||
            !applicationUser.IsActive)
        {
            return Unauthorized();
        }

        // --------------------------------------------------------
        // Roles
        // --------------------------------------------------------

        var roles =
            await _userManager
                .GetRolesAsync(applicationUser);

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
        // Staff Profile
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
        // Section Head Scope
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
        // Teacher Scope
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
        // Section Head has no section
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
                    subjectId,
                    limit
                },

                totalRankedStudents = 0,

                students =
                    Array.Empty<object>()
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
                    subjectId,
                    limit
                },

                totalRankedStudents = 0,

                students =
                    Array.Empty<object>()
            });
        }

        // --------------------------------------------------------
        // Published Marks Base Query
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

                StudentIndexNumber =
                    mark.Student.IndexNumber,

                StudentName =
                    mark.Student.FullName,

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

                SectionName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .Section.Name,

                GradeId =
                    mark.TeacherAssignment
                        .SchoolClass
                        .GradeId,

                GradeName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade.Name,

                ClassId =
                    mark.TeacherAssignment
                        .SchoolClassId,

                ClassName =
                    mark.TeacherAssignment
                        .SchoolClass.Name,

                SubjectId =
                    mark.TeacherAssignment
                        .SubjectId,

                TeacherAssignmentId =
                    mark.TeacherAssignmentId
            };

        // ========================================================
        // ROLE SCOPE
        // ========================================================

        if (!isGlobalUser &&
            isSectionHead)
        {
            query =
                query.Where(x =>
                    allowedSectionIds
                        .Contains(
                            x.SectionId));
        }

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
        // FILTERS
        // ========================================================

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                        academicYearId.Value);
        }

        if (termId.HasValue)
        {
            query =
                query.Where(x =>
                    x.TermId ==
                        termId.Value);
        }

        if (examId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ExamId ==
                        examId.Value);
        }

        if (sectionId.HasValue)
        {
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

        if (gradeId.HasValue)
        {
            query =
                query.Where(x =>
                    x.GradeId ==
                        gradeId.Value);
        }

        if (classId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ClassId ==
                        classId.Value);
        }

        if (subjectId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SubjectId ==
                        subjectId.Value);
        }

        // --------------------------------------------------------
        // Load Data
        // --------------------------------------------------------

        var data =
            await query
                .ToListAsync();

        // --------------------------------------------------------
        // Group Results by Student
        //
        // Percentage:
        //
        // Total marks obtained
        // --------------------- × 100
        // Maximum total
        // --------------------------------------------------------

        var rankedStudents =
            data
                .GroupBy(x => new
                {
                    x.StudentId,
                    x.StudentIndexNumber,
                    x.StudentName
                })
                .Select(group =>
                {
                    var totalMarks =
                        group.Sum(x =>
                            x.MarksObtained);

                    var maximumTotal =
                        group.Sum(x =>
                            x.MaximumMarks);

                    var percentage =
                        maximumTotal > 0
                            ? Math.Round(
                                totalMarks /
                                maximumTotal *
                                100,
                                2)
                            : 0;

                    return new
                    {
                        StudentId =
                            group.Key.StudentId,

                        IndexNumber =
                            group.Key.StudentIndexNumber,

                        FullName =
                            group.Key.StudentName,

                        ClassId =
                            group.First().ClassId,

                        ClassName =
                            group.First().ClassName,

                        GradeId =
                            group.First().GradeId,

                        GradeName =
                            group.First().GradeName,

                        SectionId =
                            group.First().SectionId,

                        SectionName =
                            group.First().SectionName,

                        ResultCount =
                            group.Count(),

                        TotalMarks =
                            totalMarks,

                        MaximumTotal =
                            maximumTotal,

                        Percentage =
                            percentage
                    };
                })
                .OrderByDescending(x =>
                    x.Percentage)
                .ThenByDescending(x =>
                    x.TotalMarks)
                .ThenBy(x =>
                    x.FullName)
                .Take(limit)
                .ToList();

        // --------------------------------------------------------
        // Add Rank
        // --------------------------------------------------------

        var topStudents =
            rankedStudents
                .Select((x, index) => new
                {
                    rank =
                        index + 1,

                    student = new
                    {
                        id =
                            x.StudentId,

                        indexNumber =
                            x.IndexNumber,

                        fullName =
                            x.FullName
                    },

                    schoolClass = new
                    {
                        id =
                            x.ClassId,

                        name =
                            x.ClassName,

                        grade = new
                        {
                            id =
                                x.GradeId,

                            name =
                                x.GradeName
                        },

                        section = new
                        {
                            id =
                                x.SectionId,

                            name =
                                x.SectionName
                        }
                    },

                    resultCount =
                        x.ResultCount,

                    totalMarks =
                        x.TotalMarks,

                    maximumTotal =
                        x.MaximumTotal,

                    percentage =
                        x.Percentage
                })
                .ToList();

        // --------------------------------------------------------
        // Scope Type
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
                subjectId,
                limit
            },

            totalRankedStudents =
                topStudents.Count,

            students =
                topStudents
        });
    }

    [HttpGet("class-comparison")]
    public async Task<IActionResult> GetClassComparison(
    int? academicYearId,
    int? termId,
    int? examId,
    int? sectionId,
    int? gradeId,
    int? subjectId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager
                .FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager
                .GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        var isTeacher =
            roles.Contains("Teacher");

        int? staffId = null;

        List<int>? allowedSectionIds = null;
        List<int>? allowedTeacherAssignmentIds = null;

        if (!isWholeSchool)
        {
            var staff =
                await _context.Staff
                    .FirstOrDefaultAsync(x =>
                        x.ApplicationUserId == userId &&
                        x.IsActive);

            if (staff == null)
            {
                return Forbid();
            }

            staffId = staff.Id;
        }
        else
        {
            var staff =
                await _context.Staff
                    .FirstOrDefaultAsync(x =>
                        x.ApplicationUserId == userId &&
                        x.IsActive);

            staffId = staff?.Id;
        }

        // ============================================================
        // SECTION HEAD SCOPE
        // ============================================================

        if (isSectionHead && !isWholeSchool)
        {
            var sectionQuery =
                _context.SectionHeadAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                sectionQuery =
                    sectionQuery.Where(x =>
                        x.AcademicYearId ==
                        academicYearId.Value);
            }

            allowedSectionIds =
                await sectionQuery
                    .Select(x => x.SectionId)
                    .Distinct()
                    .ToListAsync();

            if (allowedSectionIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "SectionHead",
                        staffId,
                        sections =
                            allowedSectionIds,
                        teacherAssignments =
                            (object?)null
                    },

                    filters = new
                    {
                        academicYearId,
                        termId,
                        examId,
                        sectionId,
                        gradeId,
                        subjectId
                    },

                    classes =
                        Array.Empty<object>()
                });
            }

            if (sectionId.HasValue &&
                !allowedSectionIds.Contains(
                    sectionId.Value))
            {
                return Forbid();
            }
        }

        // ============================================================
        // TEACHER SCOPE
        // ============================================================

        if (isTeacher && !isWholeSchool && !isSectionHead)
        {
            var assignmentQuery =
                _context.TeacherAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                assignmentQuery =
                    assignmentQuery.Where(x =>
                        x.AcademicYearId ==
                        academicYearId.Value);
            }

            allowedTeacherAssignmentIds =
                await assignmentQuery
                    .Select(x => x.Id)
                    .Distinct()
                    .ToListAsync();

            if (allowedTeacherAssignmentIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "Teacher",
                        staffId,
                        sections =
                            (object?)null,
                        teacherAssignments =
                            allowedTeacherAssignmentIds
                    },

                    filters = new
                    {
                        academicYearId,
                        termId,
                        examId,
                        sectionId,
                        gradeId,
                        subjectId
                    },

                    classes =
                        Array.Empty<object>()
                });
            }
        }

        if (!isWholeSchool &&
            !isSectionHead &&
            !isTeacher)
        {
            return Forbid();
        }

        // ============================================================
        // PUBLISHED RESULT BASE QUERY
        // ============================================================

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
                mark.StudentId,

                mark.MarksObtained,

                MaximumMarks =
                    mark.Exam.MaximumMarks,

                AcademicYearId =
                    mark.TeacherAssignment
                        .AcademicYearId,

                TermId =
                    mark.Exam.AcademicTermId,

                mark.ExamId,

                SectionId =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .SectionId,

                SectionName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .Section
                        .Name,

                GradeId =
                    mark.TeacherAssignment
                        .SchoolClass
                        .GradeId,

                GradeName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .Name,

                ClassId =
                    mark.TeacherAssignment
                        .SchoolClassId,

                ClassName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Name,

                SubjectId =
                    mark.TeacherAssignment
                        .SubjectId,

                mark.TeacherAssignmentId
            };

        // ============================================================
        // ROLE SCOPE
        // ============================================================

        if (isSectionHead &&
            !isWholeSchool &&
            allowedSectionIds != null)
        {
            query =
                query.Where(x =>
                    allowedSectionIds
                        .Contains(x.SectionId));
        }

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead &&
            allowedTeacherAssignmentIds != null)
        {
            query =
                query.Where(x =>
                    allowedTeacherAssignmentIds
                        .Contains(
                            x.TeacherAssignmentId));
        }

        // ============================================================
        // FILTERS
        // ============================================================

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                    academicYearId.Value);
        }

        if (termId.HasValue)
        {
            query =
                query.Where(x =>
                    x.TermId ==
                    termId.Value);
        }

        if (examId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ExamId ==
                    examId.Value);
        }

        if (sectionId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SectionId ==
                    sectionId.Value);
        }

        if (gradeId.HasValue)
        {
            query =
                query.Where(x =>
                    x.GradeId ==
                    gradeId.Value);
        }

        if (subjectId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SubjectId ==
                    subjectId.Value);
        }

        // ============================================================
        // LOAD DATA
        // ============================================================

        var resultRows =
            await query
                .AsNoTracking()
                .ToListAsync();

        // ============================================================
        // CLASS COMPARISON
        // ============================================================

        var classComparison =
            resultRows
                .GroupBy(x => new
                {
                    x.ClassId,
                    x.ClassName,
                    x.GradeId,
                    x.GradeName,
                    x.SectionId,
                    x.SectionName
                })
                .Select(group =>
                {
                    var totalMarks =
                        group.Sum(x =>
                            x.MarksObtained);

                    var maximumTotal =
                        group.Sum(x =>
                            x.MaximumMarks);

                    var percentage =
                        maximumTotal > 0
                            ? Math.Round(
                                totalMarks / maximumTotal * 100m,
                                2)
                            : 0m;

                    return new
                    {
                        schoolClass = new
                        {
                            id =
                                group.Key.ClassId,

                            name =
                                group.Key.ClassName,

                            grade = new
                            {
                                id =
                                    group.Key.GradeId,

                                name =
                                    group.Key.GradeName
                            },

                            section = new
                            {
                                id =
                                    group.Key.SectionId,

                                name =
                                    group.Key.SectionName
                            }
                        },

                        studentCount =
                            group
                                .Select(x =>
                                    x.StudentId)
                                .Distinct()
                                .Count(),

                        resultCount =
                            group.Count(),

                        totalMarks,

                        maximumTotal,

                        percentage
                    };
                })
                .OrderByDescending(x =>
                    x.percentage)
                .ThenBy(x =>
                    x.schoolClass.grade.name)
                .ThenBy(x =>
                    x.schoolClass.name)
                .ToList();

        // ============================================================
        // RESPONSE
        // ============================================================

        string scopeType;

        if (isWholeSchool)
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

        return Ok(new
        {
            scope = new
            {
                type =
                    scopeType,

                staffId,

                sections =
                    allowedSectionIds,

                teacherAssignments =
                    allowedTeacherAssignmentIds
            },

            filters = new
            {
                academicYearId,
                termId,
                examId,
                sectionId,
                gradeId,
                subjectId
            },

            classCount =
                classComparison.Count,

            classes =
                classComparison
        });
    }

    [HttpGet("grade-comparison")]
    public async Task<IActionResult> GetGradeComparison(
    int? academicYearId,
    int? termId,
    int? examId,
    int? sectionId,
    int? classId,
    int? subjectId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager
                .FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager
                .GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        var isTeacher =
            roles.Contains("Teacher");

        int? staffId = null;

        List<int>? allowedSectionIds = null;
        List<int>? allowedTeacherAssignmentIds = null;

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        staffId = staff?.Id;

        if (!isWholeSchool && staff == null)
        {
            return Forbid();
        }

        // ============================================================
        // SECTION HEAD SCOPE
        // ============================================================

        if (isSectionHead && !isWholeSchool)
        {
            var sectionQuery =
                _context.SectionHeadAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                sectionQuery =
                    sectionQuery.Where(x =>
                        x.AcademicYearId ==
                        academicYearId.Value);
            }

            allowedSectionIds =
                await sectionQuery
                    .Select(x => x.SectionId)
                    .Distinct()
                    .ToListAsync();

            if (allowedSectionIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "SectionHead",
                        staffId,
                        sections = allowedSectionIds,
                        teacherAssignments = (object?)null
                    },

                    filters = new
                    {
                        academicYearId,
                        termId,
                        examId,
                        sectionId,
                        classId,
                        subjectId
                    },

                    gradeCount = 0,
                    grades = Array.Empty<object>()
                });
            }

            if (sectionId.HasValue &&
                !allowedSectionIds.Contains(
                    sectionId.Value))
            {
                return Forbid();
            }
        }

        // ============================================================
        // TEACHER SCOPE
        // ============================================================

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead)
        {
            var assignmentQuery =
                _context.TeacherAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                assignmentQuery =
                    assignmentQuery.Where(x =>
                        x.AcademicYearId ==
                        academicYearId.Value);
            }

            allowedTeacherAssignmentIds =
                await assignmentQuery
                    .Select(x => x.Id)
                    .Distinct()
                    .ToListAsync();

            if (allowedTeacherAssignmentIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "Teacher",
                        staffId,
                        sections = (object?)null,
                        teacherAssignments =
                            allowedTeacherAssignmentIds
                    },

                    filters = new
                    {
                        academicYearId,
                        termId,
                        examId,
                        sectionId,
                        classId,
                        subjectId
                    },

                    gradeCount = 0,
                    grades = Array.Empty<object>()
                });
            }
        }

        if (!isWholeSchool &&
            !isSectionHead &&
            !isTeacher)
        {
            return Forbid();
        }

        // ============================================================
        // PUBLISHED RESULT BASE QUERY
        // ============================================================

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
                mark.StudentId,

                mark.MarksObtained,

                MaximumMarks =
                    mark.Exam.MaximumMarks,

                AcademicYearId =
                    mark.TeacherAssignment
                        .AcademicYearId,

                TermId =
                    mark.Exam.AcademicTermId,

                mark.ExamId,

                SectionId =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .SectionId,

                SectionName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .Section
                        .Name,

                GradeId =
                    mark.TeacherAssignment
                        .SchoolClass
                        .GradeId,

                GradeName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .Name,

                ClassId =
                    mark.TeacherAssignment
                        .SchoolClassId,

                ClassName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Name,

                SubjectId =
                    mark.TeacherAssignment
                        .SubjectId,

                mark.TeacherAssignmentId
            };

        // ============================================================
        // ROLE SCOPE
        // ============================================================

        if (isSectionHead &&
            !isWholeSchool &&
            allowedSectionIds != null)
        {
            query =
                query.Where(x =>
                    allowedSectionIds
                        .Contains(x.SectionId));
        }

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead &&
            allowedTeacherAssignmentIds != null)
        {
            query =
                query.Where(x =>
                    allowedTeacherAssignmentIds
                        .Contains(
                            x.TeacherAssignmentId));
        }

        // ============================================================
        // FILTERS
        // ============================================================

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                    academicYearId.Value);
        }

        if (termId.HasValue)
        {
            query =
                query.Where(x =>
                    x.TermId ==
                    termId.Value);
        }

        if (examId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ExamId ==
                    examId.Value);
        }

        if (sectionId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SectionId ==
                    sectionId.Value);
        }

        if (classId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ClassId ==
                    classId.Value);
        }

        if (subjectId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SubjectId ==
                    subjectId.Value);
        }

        // ============================================================
        // LOAD DATA
        // ============================================================

        var resultRows =
            await query
                .AsNoTracking()
                .ToListAsync();

        // ============================================================
        // GRADE COMPARISON
        // ============================================================

        var gradeComparison =
            resultRows
                .GroupBy(x => new
                {
                    x.GradeId,
                    x.GradeName,
                    x.SectionId,
                    x.SectionName
                })
                .Select(group =>
                {
                    var totalMarks =
                        group.Sum(x =>
                            x.MarksObtained);

                    var maximumTotal =
                        group.Sum(x =>
                            x.MaximumMarks);

                    var percentage =
                        maximumTotal > 0
                            ? Math.Round(
                                totalMarks /
                                maximumTotal *
                                100m,
                                2)
                            : 0m;

                    return new
                    {
                        grade = new
                        {
                            id =
                                group.Key.GradeId,

                            name =
                                group.Key.GradeName,

                            section = new
                            {
                                id =
                                    group.Key.SectionId,

                                name =
                                    group.Key.SectionName
                            }
                        },

                        classCount =
                            group
                                .Select(x =>
                                    x.ClassId)
                                .Distinct()
                                .Count(),

                        studentCount =
                            group
                                .Select(x =>
                                    x.StudentId)
                                .Distinct()
                                .Count(),

                        resultCount =
                            group.Count(),

                        totalMarks,

                        maximumTotal,

                        percentage
                    };
                })
                .OrderByDescending(x =>
                    x.percentage)
                .ThenBy(x =>
                    x.grade.section.name)
                .ThenBy(x =>
                    x.grade.name)
                .ToList();

        // ============================================================
        // SCOPE TYPE
        // ============================================================

        string scopeType;

        if (isWholeSchool)
        {
            scopeType = "WholeSchool";
        }
        else if (isSectionHead)
        {
            scopeType = "SectionHead";
        }
        else
        {
            scopeType = "Teacher";
        }

        return Ok(new
        {
            scope = new
            {
                type = scopeType,
                staffId,
                sections = allowedSectionIds,
                teacherAssignments =
                    allowedTeacherAssignmentIds
            },

            filters = new
            {
                academicYearId,
                termId,
                examId,
                sectionId,
                classId,
                subjectId
            },

            gradeCount =
                gradeComparison.Count,

            grades =
                gradeComparison
        });
    }

    [HttpGet("subject-comparison")]
    public async Task<IActionResult> GetSubjectComparison(
    int? academicYearId,
    int? termId,
    int? examId,
    int? sectionId,
    int? gradeId,
    int? classId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager
                .FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager
                .GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        var isTeacher =
            roles.Contains("Teacher");

        int? staffId = null;

        List<int>? allowedSectionIds = null;
        List<int>? allowedTeacherAssignmentIds = null;

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        staffId = staff?.Id;

        if (!isWholeSchool && staff == null)
        {
            return Forbid();
        }

        // ============================================================
        // SECTION HEAD SCOPE
        // ============================================================

        if (isSectionHead && !isWholeSchool)
        {
            var sectionQuery =
                _context.SectionHeadAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                sectionQuery =
                    sectionQuery.Where(x =>
                        x.AcademicYearId ==
                        academicYearId.Value);
            }

            allowedSectionIds =
                await sectionQuery
                    .Select(x => x.SectionId)
                    .Distinct()
                    .ToListAsync();

            if (allowedSectionIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "SectionHead",
                        staffId,
                        sections = allowedSectionIds,
                        teacherAssignments = (object?)null
                    },

                    filters = new
                    {
                        academicYearId,
                        termId,
                        examId,
                        sectionId,
                        gradeId,
                        classId
                    },

                    subjectCount = 0,
                    subjects = Array.Empty<object>()
                });
            }

            if (sectionId.HasValue &&
                !allowedSectionIds.Contains(
                    sectionId.Value))
            {
                return Forbid();
            }
        }

        // ============================================================
        // TEACHER SCOPE
        // ============================================================

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead)
        {
            var assignmentQuery =
                _context.TeacherAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                assignmentQuery =
                    assignmentQuery.Where(x =>
                        x.AcademicYearId ==
                        academicYearId.Value);
            }

            allowedTeacherAssignmentIds =
                await assignmentQuery
                    .Select(x => x.Id)
                    .Distinct()
                    .ToListAsync();

            if (allowedTeacherAssignmentIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "Teacher",
                        staffId,
                        sections = (object?)null,
                        teacherAssignments =
                            allowedTeacherAssignmentIds
                    },

                    filters = new
                    {
                        academicYearId,
                        termId,
                        examId,
                        sectionId,
                        gradeId,
                        classId
                    },

                    subjectCount = 0,
                    subjects = Array.Empty<object>()
                });
            }
        }

        if (!isWholeSchool &&
            !isSectionHead &&
            !isTeacher)
        {
            return Forbid();
        }

        // ============================================================
        // PUBLISHED RESULT BASE QUERY
        // ============================================================

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
                mark.StudentId,

                mark.MarksObtained,

                MaximumMarks =
                    mark.Exam.MaximumMarks,

                AcademicYearId =
                    mark.TeacherAssignment
                        .AcademicYearId,

                TermId =
                    mark.Exam.AcademicTermId,

                mark.ExamId,

                SectionId =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .SectionId,

                SectionName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .Section
                        .Name,

                GradeId =
                    mark.TeacherAssignment
                        .SchoolClass
                        .GradeId,

                GradeName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .Name,

                ClassId =
                    mark.TeacherAssignment
                        .SchoolClassId,

                ClassName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Name,

                SubjectId =
                    mark.TeacherAssignment
                        .SubjectId,

                SubjectName =
                    mark.TeacherAssignment
                        .Subject
                        .Name,

                mark.TeacherAssignmentId
            };

        // ============================================================
        // ROLE SCOPE
        // ============================================================

        if (isSectionHead &&
            !isWholeSchool &&
            allowedSectionIds != null)
        {
            query =
                query.Where(x =>
                    allowedSectionIds
                        .Contains(x.SectionId));
        }

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead &&
            allowedTeacherAssignmentIds != null)
        {
            query =
                query.Where(x =>
                    allowedTeacherAssignmentIds
                        .Contains(
                            x.TeacherAssignmentId));
        }

        // ============================================================
        // FILTERS
        // ============================================================

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                    academicYearId.Value);
        }

        if (termId.HasValue)
        {
            query =
                query.Where(x =>
                    x.TermId ==
                    termId.Value);
        }

        if (examId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ExamId ==
                    examId.Value);
        }

        if (sectionId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SectionId ==
                    sectionId.Value);
        }

        if (gradeId.HasValue)
        {
            query =
                query.Where(x =>
                    x.GradeId ==
                    gradeId.Value);
        }

        if (classId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ClassId ==
                    classId.Value);
        }

        // ============================================================
        // LOAD DATA
        // ============================================================

        var resultRows =
            await query
                .AsNoTracking()
                .ToListAsync();

        // ============================================================
        // SUBJECT COMPARISON
        // ============================================================

        var subjectComparison =
            resultRows
                .GroupBy(x => new
                {
                    x.SubjectId,
                    x.SubjectName
                })
                .Select(group =>
                {
                    var totalMarks =
                        group.Sum(x =>
                            x.MarksObtained);

                    var maximumTotal =
                        group.Sum(x =>
                            x.MaximumMarks);

                    var percentage =
                        maximumTotal > 0
                            ? Math.Round(
                                totalMarks /
                                maximumTotal *
                                100m,
                                2)
                            : 0m;

                    return new
                    {
                        subject = new
                        {
                            id =
                                group.Key.SubjectId,

                            name =
                                group.Key.SubjectName
                        },

                        studentCount =
                            group
                                .Select(x =>
                                    x.StudentId)
                                .Distinct()
                                .Count(),

                        classCount =
                            group
                                .Select(x =>
                                    x.ClassId)
                                .Distinct()
                                .Count(),

                        resultCount =
                            group.Count(),

                        totalMarks,

                        maximumTotal,

                        percentage
                    };
                })
                .OrderByDescending(x =>
                    x.percentage)
                .ThenBy(x =>
                    x.subject.name)
                .ToList();

        // ============================================================
        // SCOPE TYPE
        // ============================================================

        string scopeType;

        if (isWholeSchool)
        {
            scopeType = "WholeSchool";
        }
        else if (isSectionHead)
        {
            scopeType = "SectionHead";
        }
        else
        {
            scopeType = "Teacher";
        }

        return Ok(new
        {
            scope = new
            {
                type = scopeType,
                staffId,
                sections = allowedSectionIds,
                teacherAssignments =
                    allowedTeacherAssignmentIds
            },

            filters = new
            {
                academicYearId,
                termId,
                examId,
                sectionId,
                gradeId,
                classId
            },

            subjectCount =
                subjectComparison.Count,

            subjects =
                subjectComparison
        });
    }

    [HttpGet("exam-comparison")]
    public async Task<IActionResult> GetExamComparison(
    int? academicYearId,
    int? termId,
    int? sectionId,
    int? gradeId,
    int? classId,
    int? subjectId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        var isTeacher =
            roles.Contains("Teacher");

        int? staffId = null;

        List<int>? allowedSectionIds = null;
        List<int>? allowedTeacherAssignmentIds = null;

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        staffId = staff?.Id;

        if (!isWholeSchool && staff == null)
        {
            return Forbid();
        }

        // ============================================================
        // SECTION HEAD SCOPE
        // ============================================================

        if (isSectionHead && !isWholeSchool)
        {
            var sectionQuery =
                _context.SectionHeadAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                sectionQuery =
                    sectionQuery.Where(x =>
                        x.AcademicYearId ==
                        academicYearId.Value);
            }

            allowedSectionIds =
                await sectionQuery
                    .Select(x => x.SectionId)
                    .Distinct()
                    .ToListAsync();

            if (allowedSectionIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "SectionHead",
                        staffId,
                        sections = allowedSectionIds,
                        teacherAssignments = (object?)null
                    },

                    filters = new
                    {
                        academicYearId,
                        termId,
                        sectionId,
                        gradeId,
                        classId,
                        subjectId
                    },

                    examCount = 0,
                    exams = Array.Empty<object>()
                });
            }

            if (sectionId.HasValue &&
                !allowedSectionIds.Contains(
                    sectionId.Value))
            {
                return Forbid();
            }
        }

        // ============================================================
        // TEACHER SCOPE
        // ============================================================

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead)
        {
            var assignmentQuery =
                _context.TeacherAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                assignmentQuery =
                    assignmentQuery.Where(x =>
                        x.AcademicYearId ==
                        academicYearId.Value);
            }

            allowedTeacherAssignmentIds =
                await assignmentQuery
                    .Select(x => x.Id)
                    .Distinct()
                    .ToListAsync();

            if (allowedTeacherAssignmentIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "Teacher",
                        staffId,
                        sections = (object?)null,
                        teacherAssignments =
                            allowedTeacherAssignmentIds
                    },

                    filters = new
                    {
                        academicYearId,
                        termId,
                        sectionId,
                        gradeId,
                        classId,
                        subjectId
                    },

                    examCount = 0,
                    exams = Array.Empty<object>()
                });
            }
        }

        if (!isWholeSchool &&
            !isSectionHead &&
            !isTeacher)
        {
            return Forbid();
        }

        // ============================================================
        // PUBLISHED RESULT BASE QUERY
        // ============================================================

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
                mark.StudentId,

                mark.MarksObtained,

                MaximumMarks =
                    mark.Exam.MaximumMarks,

                AcademicYearId =
                    mark.TeacherAssignment.AcademicYearId,

                AcademicYearName =
                    mark.TeacherAssignment
                        .AcademicYear
                        .Name,

                TermId =
                    mark.Exam.AcademicTermId,

                TermName =
                    mark.Exam.AcademicTerm.Name,

                mark.ExamId,

                ExamName =
                    mark.Exam.Name,

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

                mark.TeacherAssignmentId
            };

        // ============================================================
        // ROLE SCOPE
        // ============================================================

        if (isSectionHead &&
            !isWholeSchool &&
            allowedSectionIds != null)
        {
            query =
                query.Where(x =>
                    allowedSectionIds.Contains(
                        x.SectionId));
        }

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead &&
            allowedTeacherAssignmentIds != null)
        {
            query =
                query.Where(x =>
                    allowedTeacherAssignmentIds
                        .Contains(
                            x.TeacherAssignmentId));
        }

        // ============================================================
        // FILTERS
        // ============================================================

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                    academicYearId.Value);
        }

        if (termId.HasValue)
        {
            query =
                query.Where(x =>
                    x.TermId ==
                    termId.Value);
        }

        if (sectionId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SectionId ==
                    sectionId.Value);
        }

        if (gradeId.HasValue)
        {
            query =
                query.Where(x =>
                    x.GradeId ==
                    gradeId.Value);
        }

        if (classId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ClassId ==
                    classId.Value);
        }

        if (subjectId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SubjectId ==
                    subjectId.Value);
        }

        // ============================================================
        // LOAD DATA
        // ============================================================

        var resultRows =
            await query
                .AsNoTracking()
                .ToListAsync();

        // ============================================================
        // EXAM COMPARISON
        // ============================================================

        var examComparison =
            resultRows
                .GroupBy(x => new
                {
                    x.ExamId,
                    x.ExamName,
                    x.TermId,
                    x.TermName,
                    x.AcademicYearId,
                    x.AcademicYearName
                })
                .Select(group =>
                {
                    var totalMarks =
                        group.Sum(x =>
                            x.MarksObtained);

                    var maximumTotal =
                        group.Sum(x =>
                            x.MaximumMarks);

                    var percentage =
                        maximumTotal > 0
                            ? Math.Round(
                                totalMarks /
                                maximumTotal *
                                100m,
                                2)
                            : 0m;

                    return new
                    {
                        exam = new
                        {
                            id =
                                group.Key.ExamId,

                            name =
                                group.Key.ExamName,

                            term = new
                            {
                                id =
                                    group.Key.TermId,

                                name =
                                    group.Key.TermName
                            },

                            academicYear = new
                            {
                                id =
                                    group.Key.AcademicYearId,

                                name =
                                    group.Key.AcademicYearName
                            }
                        },

                        studentCount =
                            group
                                .Select(x =>
                                    x.StudentId)
                                .Distinct()
                                .Count(),

                        resultCount =
                            group.Count(),

                        totalMarks,

                        maximumTotal,

                        percentage
                    };
                })
                .OrderByDescending(x =>
                    x.percentage)
                .ThenBy(x =>
                    x.exam.name)
                .ToList();

        // ============================================================
        // SCOPE TYPE
        // ============================================================

        string scopeType;

        if (isWholeSchool)
        {
            scopeType = "WholeSchool";
        }
        else if (isSectionHead)
        {
            scopeType = "SectionHead";
        }
        else
        {
            scopeType = "Teacher";
        }

        return Ok(new
        {
            scope = new
            {
                type = scopeType,
                staffId,
                sections = allowedSectionIds,
                teacherAssignments =
                    allowedTeacherAssignmentIds
            },

            filters = new
            {
                academicYearId,
                termId,
                sectionId,
                gradeId,
                classId,
                subjectId
            },

            examCount =
                examComparison.Count,

            exams =
                examComparison
        });
    }

    [HttpGet("term-comparison")]
    public async Task<IActionResult> GetTermComparison(
    int? academicYearId,
    int? sectionId,
    int? gradeId,
    int? classId,
    int? subjectId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        var isTeacher =
            roles.Contains("Teacher");

        int? staffId = null;

        List<int>? allowedSectionIds = null;
        List<int>? allowedTeacherAssignmentIds = null;

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        staffId = staff?.Id;

        if (!isWholeSchool && staff == null)
        {
            return Forbid();
        }

        // ============================================================
        // SECTION HEAD SCOPE
        // ============================================================

        if (isSectionHead && !isWholeSchool)
        {
            var sectionQuery =
                _context.SectionHeadAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                sectionQuery =
                    sectionQuery.Where(x =>
                        x.AcademicYearId ==
                        academicYearId.Value);
            }

            allowedSectionIds =
                await sectionQuery
                    .Select(x => x.SectionId)
                    .Distinct()
                    .ToListAsync();

            if (allowedSectionIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "SectionHead",
                        staffId,
                        sections = allowedSectionIds,
                        teacherAssignments = (object?)null
                    },

                    filters = new
                    {
                        academicYearId,
                        sectionId,
                        gradeId,
                        classId,
                        subjectId
                    },

                    termCount = 0,
                    terms = Array.Empty<object>()
                });
            }

            if (sectionId.HasValue &&
                !allowedSectionIds.Contains(
                    sectionId.Value))
            {
                return Forbid();
            }
        }

        // ============================================================
        // TEACHER SCOPE
        // ============================================================

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead)
        {
            var assignmentQuery =
                _context.TeacherAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                assignmentQuery =
                    assignmentQuery.Where(x =>
                        x.AcademicYearId ==
                        academicYearId.Value);
            }

            allowedTeacherAssignmentIds =
                await assignmentQuery
                    .Select(x => x.Id)
                    .Distinct()
                    .ToListAsync();

            if (allowedTeacherAssignmentIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "Teacher",
                        staffId,
                        sections = (object?)null,
                        teacherAssignments =
                            allowedTeacherAssignmentIds
                    },

                    filters = new
                    {
                        academicYearId,
                        sectionId,
                        gradeId,
                        classId,
                        subjectId
                    },

                    termCount = 0,
                    terms = Array.Empty<object>()
                });
            }
        }

        if (!isWholeSchool &&
            !isSectionHead &&
            !isTeacher)
        {
            return Forbid();
        }

        // ============================================================
        // PUBLISHED RESULT BASE QUERY
        // ============================================================

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
                mark.StudentId,

                mark.MarksObtained,

                MaximumMarks =
                    mark.Exam.MaximumMarks,

                AcademicYearId =
                    mark.TeacherAssignment
                        .AcademicYearId,

                AcademicYearName =
                    mark.TeacherAssignment
                        .AcademicYear
                        .Name,

                TermId =
                    mark.Exam.AcademicTermId,

                TermName =
                    mark.Exam.AcademicTerm.Name,

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

                mark.TeacherAssignmentId
            };

        // ============================================================
        // ROLE SCOPE
        // ============================================================

        if (isSectionHead &&
            !isWholeSchool &&
            allowedSectionIds != null)
        {
            query =
                query.Where(x =>
                    allowedSectionIds.Contains(
                        x.SectionId));
        }

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead &&
            allowedTeacherAssignmentIds != null)
        {
            query =
                query.Where(x =>
                    allowedTeacherAssignmentIds
                        .Contains(
                            x.TeacherAssignmentId));
        }

        // ============================================================
        // FILTERS
        // ============================================================

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                    academicYearId.Value);
        }

        if (sectionId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SectionId ==
                    sectionId.Value);
        }

        if (gradeId.HasValue)
        {
            query =
                query.Where(x =>
                    x.GradeId ==
                    gradeId.Value);
        }

        if (classId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ClassId ==
                    classId.Value);
        }

        if (subjectId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SubjectId ==
                    subjectId.Value);
        }

        // ============================================================
        // LOAD DATA
        // ============================================================

        var resultRows =
            await query
                .AsNoTracking()
                .ToListAsync();

        // ============================================================
        // TERM COMPARISON
        // ============================================================

        var termComparison =
            resultRows
                .GroupBy(x => new
                {
                    x.TermId,
                    x.TermName,
                    x.AcademicYearId,
                    x.AcademicYearName
                })
                .Select(group =>
                {
                    var totalMarks =
                        group.Sum(x =>
                            x.MarksObtained);

                    var maximumTotal =
                        group.Sum(x =>
                            x.MaximumMarks);

                    var percentage =
                        maximumTotal > 0
                            ? Math.Round(
                                totalMarks /
                                maximumTotal *
                                100m,
                                2)
                            : 0m;

                    return new
                    {
                        term = new
                        {
                            id =
                                group.Key.TermId,

                            name =
                                group.Key.TermName,

                            academicYear = new
                            {
                                id =
                                    group.Key.AcademicYearId,

                                name =
                                    group.Key.AcademicYearName
                            }
                        },

                        studentCount =
                            group
                                .Select(x =>
                                    x.StudentId)
                                .Distinct()
                                .Count(),

                        resultCount =
                            group.Count(),

                        totalMarks,

                        maximumTotal,

                        percentage
                    };
                })
                .OrderBy(x =>
                    x.term.academicYear.name)
                .ThenBy(x =>
                    x.term.name)
                .ToList();

        // ============================================================
        // SCOPE TYPE
        // ============================================================

        string scopeType;

        if (isWholeSchool)
        {
            scopeType = "WholeSchool";
        }
        else if (isSectionHead)
        {
            scopeType = "SectionHead";
        }
        else
        {
            scopeType = "Teacher";
        }

        return Ok(new
        {
            scope = new
            {
                type = scopeType,
                staffId,
                sections = allowedSectionIds,
                teacherAssignments =
                    allowedTeacherAssignmentIds
            },

            filters = new
            {
                academicYearId,
                sectionId,
                gradeId,
                classId,
                subjectId
            },

            termCount =
                termComparison.Count,

            terms =
                termComparison
        });
    }


    [HttpGet("academic-year-comparison")]
    public async Task<IActionResult> GetAcademicYearComparison(
    int? sectionId,
    int? gradeId,
    int? classId,
    int? subjectId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        var isTeacher =
            roles.Contains("Teacher");

        int? staffId = null;

        List<int>? allowedSectionIds = null;
        List<int>? allowedTeacherAssignmentIds = null;

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        staffId = staff?.Id;

        if (!isWholeSchool && staff == null)
        {
            return Forbid();
        }

        // ============================================================
        // SECTION HEAD SCOPE
        // ============================================================

        if (isSectionHead && !isWholeSchool)
        {
            var sectionQuery =
                _context.SectionHeadAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            allowedSectionIds =
                await sectionQuery
                    .Select(x => x.SectionId)
                    .Distinct()
                    .ToListAsync();

            if (allowedSectionIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "SectionHead",
                        staffId,
                        sections = allowedSectionIds,
                        teacherAssignments = (object?)null
                    },

                    filters = new
                    {
                        sectionId,
                        gradeId,
                        classId,
                        subjectId
                    },

                    academicYearCount = 0,
                    academicYears = Array.Empty<object>()
                });
            }

            if (sectionId.HasValue &&
                !allowedSectionIds.Contains(
                    sectionId.Value))
            {
                return Forbid();
            }
        }

        // ============================================================
        // TEACHER SCOPE
        // ============================================================

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead)
        {
            var assignmentQuery =
                _context.TeacherAssignments
                    .Where(x =>
                        x.StaffId == staffId &&
                        x.IsActive);

            allowedTeacherAssignmentIds =
                await assignmentQuery
                    .Select(x => x.Id)
                    .Distinct()
                    .ToListAsync();

            if (allowedTeacherAssignmentIds.Count == 0)
            {
                return Ok(new
                {
                    scope = new
                    {
                        type = "Teacher",
                        staffId,
                        sections = (object?)null,
                        teacherAssignments =
                            allowedTeacherAssignmentIds
                    },

                    filters = new
                    {
                        sectionId,
                        gradeId,
                        classId,
                        subjectId
                    },

                    academicYearCount = 0,
                    academicYears = Array.Empty<object>()
                });
            }
        }

        if (!isWholeSchool &&
            !isSectionHead &&
            !isTeacher)
        {
            return Forbid();
        }

        // ============================================================
        // PUBLISHED RESULT BASE QUERY
        // ============================================================

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
                mark.StudentId,

                mark.MarksObtained,

                MaximumMarks =
                    mark.Exam.MaximumMarks,

                AcademicYearId =
                    mark.TeacherAssignment
                        .AcademicYearId,

                AcademicYearName =
                    mark.TeacherAssignment
                        .AcademicYear
                        .Name,

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

                mark.TeacherAssignmentId
            };

        // ============================================================
        // ROLE SCOPE
        // ============================================================

        if (isSectionHead &&
            !isWholeSchool &&
            allowedSectionIds != null)
        {
            query =
                query.Where(x =>
                    allowedSectionIds.Contains(
                        x.SectionId));
        }

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead &&
            allowedTeacherAssignmentIds != null)
        {
            query =
                query.Where(x =>
                    allowedTeacherAssignmentIds
                        .Contains(
                            x.TeacherAssignmentId));
        }

        // ============================================================
        // FILTERS
        // ============================================================

        if (sectionId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SectionId ==
                    sectionId.Value);
        }

        if (gradeId.HasValue)
        {
            query =
                query.Where(x =>
                    x.GradeId ==
                    gradeId.Value);
        }

        if (classId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ClassId ==
                    classId.Value);
        }

        if (subjectId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SubjectId ==
                    subjectId.Value);
        }

        // ============================================================
        // LOAD DATA
        // ============================================================

        var resultRows =
            await query
                .AsNoTracking()
                .ToListAsync();

        // ============================================================
        // ACADEMIC YEAR COMPARISON
        // ============================================================

        var academicYearComparison =
            resultRows
                .GroupBy(x => new
                {
                    x.AcademicYearId,
                    x.AcademicYearName
                })
                .Select(group =>
                {
                    var totalMarks =
                        group.Sum(x =>
                            x.MarksObtained);

                    var maximumTotal =
                        group.Sum(x =>
                            x.MaximumMarks);

                    var percentage =
                        maximumTotal > 0
                            ? Math.Round(
                                totalMarks /
                                maximumTotal *
                                100m,
                                2)
                            : 0m;

                    return new
                    {
                        academicYear = new
                        {
                            id =
                                group.Key.AcademicYearId,

                            name =
                                group.Key.AcademicYearName
                        },

                        studentCount =
                            group
                                .Select(x =>
                                    x.StudentId)
                                .Distinct()
                                .Count(),

                        resultCount =
                            group.Count(),

                        totalMarks,

                        maximumTotal,

                        percentage
                    };
                })
                .OrderBy(x =>
                    x.academicYear.name)
                .ToList();

        // ============================================================
        // SCOPE TYPE
        // ============================================================

        string scopeType;

        if (isWholeSchool)
        {
            scopeType = "WholeSchool";
        }
        else if (isSectionHead)
        {
            scopeType = "SectionHead";
        }
        else
        {
            scopeType = "Teacher";
        }

        return Ok(new
        {
            scope = new
            {
                type = scopeType,
                staffId,
                sections = allowedSectionIds,
                teacherAssignments =
                    allowedTeacherAssignmentIds
            },

            filters = new
            {
                sectionId,
                gradeId,
                classId,
                subjectId
            },

            academicYearCount =
                academicYearComparison.Count,

            academicYears =
                academicYearComparison
        });
    }
}