using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-results")]
[Authorize]
public class StudentResultsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StudentResultsController(
        ApplicationDbContext context)
    {
        _context = context;
    }


    // ============================================================
    // MY PUBLISHED RESULTS
    // ============================================================

    [HttpGet("my")]
    public async Task<IActionResult> GetMyResults()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // --------------------------------------------------------
        // Find Student linked to logged-in user
        // --------------------------------------------------------

        var student =
            await _context.Students
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (student == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        // --------------------------------------------------------
        // Published results only
        // --------------------------------------------------------

        var results =
            await (
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
                    mark.StudentId == student.Id &&

                    mark.IsPublished &&

                    submission.Status ==
                        MarksSubmissionStatus.Published

                orderby
                    mark.Exam.ExamDate descending,
                    mark.TeacherAssignment.Subject.Name

                select new
                {
                    MarkId =
                        mark.Id,

                    AcademicYear = new
                    {
                        mark.TeacherAssignment
                            .AcademicYear.Id,

                        mark.TeacherAssignment
                            .AcademicYear.Name
                    },

                    Term = new
                    {
                        mark.Exam
                            .AcademicTerm.Id,

                        mark.Exam
                            .AcademicTerm.Name
                    },

                    Exam = new
                    {
                        mark.Exam.Id,

                        mark.Exam.Name,

                        mark.Exam.ExamDate,

                        mark.Exam.MaximumMarks
                    },

                    Subject = new
                    {
                        mark.TeacherAssignment
                            .Subject.Id,

                        mark.TeacherAssignment
                            .Subject.Name
                    },

                    SchoolClass = new
                    {
                        mark.TeacherAssignment
                            .SchoolClass.Id,

                        mark.TeacherAssignment
                            .SchoolClass.Name,

                        Grade =
                            mark.TeacherAssignment
                                .SchoolClass
                                .Grade.Name,

                        Section =
                            mark.TeacherAssignment
                                .SchoolClass
                                .Grade
                                .Section.Name
                    },

                    mark.MarksObtained,

                    Percentage =
                        mark.Exam.MaximumMarks > 0
                            ? Math.Round(
                                mark.MarksObtained /
                                mark.Exam.MaximumMarks *
                                100,
                                2)
                            : 0,

                    PublishedAt =
                        submission.PublishedAt
                })
                .ToListAsync();

        return Ok(new
        {
            student = new
            {
                student.Id,
                student.IndexNumber,
                student.FullName,

                schoolClass = new
                {
                    student.SchoolClass.Id,
                    student.SchoolClass.Name,

                    grade =
                        student.SchoolClass
                            .Grade.Name,

                    section =
                        student.SchoolClass
                            .Grade
                            .Section.Name
                }
            },

            results
        });
    }


    // ============================================================
    // MY RESULT SUMMARY
    //
    // Optional filters:
    //
    // academicYearId
    // termId
    // examId
    //
    // Examples:
    //
    // /api/student-results/my/summary
    //
    // /api/student-results/my/summary?academicYearId=1
    //
    // /api/student-results/my/summary?academicYearId=1&termId=1
    //
    // /api/student-results/my/summary?examId=1
    // ============================================================

    [HttpGet("my/summary")]
    public async Task<IActionResult> GetMyResultSummary(
        [FromQuery] int? academicYearId,
        [FromQuery] int? termId,
        [FromQuery] int? examId)
    {
        // --------------------------------------------------------
        // Get logged-in user's Identity ID
        // --------------------------------------------------------

        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // --------------------------------------------------------
        // Find Student
        // --------------------------------------------------------

        var student =
            await _context.Students
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (student == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        // --------------------------------------------------------
        // Build Published Results Query
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
                mark.StudentId == student.Id &&

                mark.IsPublished &&

                submission.Status ==
                    MarksSubmissionStatus.Published

            select new
            {
                MarkId =
                    mark.Id,

                AcademicYearId =
                    mark.TeacherAssignment
                        .AcademicYear.Id,

                AcademicYearName =
                    mark.TeacherAssignment
                        .AcademicYear.Name,

                TermId =
                    mark.Exam
                        .AcademicTerm.Id,

                TermName =
                    mark.Exam
                        .AcademicTerm.Name,

                ExamId =
                    mark.Exam.Id,

                ExamName =
                    mark.Exam.Name,

                ExamDate =
                    mark.Exam.ExamDate,

                MaximumMarks =
                    mark.Exam.MaximumMarks,

                SubjectId =
                    mark.TeacherAssignment
                        .Subject.Id,

                SubjectName =
                    mark.TeacherAssignment
                        .Subject.Name,

                SchoolClassId =
                    mark.TeacherAssignment
                        .SchoolClass.Id,

                SchoolClassName =
                    mark.TeacherAssignment
                        .SchoolClass.Name,

                GradeName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade.Name,

                SectionName =
                    mark.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .Section.Name,

                MarksObtained =
                    mark.MarksObtained,

                PublishedAt =
                    submission.PublishedAt
            };


        // --------------------------------------------------------
        // Academic Year Filter
        // --------------------------------------------------------

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                        academicYearId.Value);
        }


        // --------------------------------------------------------
        // Term Filter
        // --------------------------------------------------------

        if (termId.HasValue)
        {
            query =
                query.Where(x =>
                    x.TermId ==
                        termId.Value);
        }


        // --------------------------------------------------------
        // Exam Filter
        // --------------------------------------------------------

        if (examId.HasValue)
        {
            query =
                query.Where(x =>
                    x.ExamId ==
                        examId.Value);
        }


        // --------------------------------------------------------
        // Load published marks
        // --------------------------------------------------------

        var publishedMarks =
            await query
                .OrderByDescending(x =>
                    x.ExamDate)
                .ThenBy(x =>
                    x.SubjectName)
                .ToListAsync();


        // --------------------------------------------------------
        // Group Results by Exam
        // --------------------------------------------------------

        var examSummaries =
            publishedMarks
                .GroupBy(x => new
                {
                    x.AcademicYearId,
                    x.AcademicYearName,

                    x.TermId,
                    x.TermName,

                    x.ExamId,
                    x.ExamName,
                    x.ExamDate
                })
                .Select(group =>
                {
                    // --------------------------------------------
                    // Total marks student obtained
                    // --------------------------------------------

                    var totalMarks =
                        group.Sum(x =>
                            x.MarksObtained);


                    // --------------------------------------------
                    // Maximum possible marks
                    //
                    // Example:
                    //
                    // Math    = 100
                    // English = 100
                    // Science = 100
                    //
                    // Maximum Total = 300
                    // --------------------------------------------

                    var maximumTotal =
                        group.Sum(x =>
                            x.MaximumMarks);


                    // --------------------------------------------
                    // Overall Percentage
                    // --------------------------------------------

                    var percentage =
                        maximumTotal > 0
                            ? Math.Round(
                                totalMarks /
                                maximumTotal *
                                100,
                                2)
                            : 0;


                    // --------------------------------------------
                    // Average Marks
                    //
                    // Example:
                    //
                    // 85 + 75 + 90 = 250
                    // 250 / 3 = 83.33
                    // --------------------------------------------

                    var average =
                        group.Any()
                            ? Math.Round(
                                group.Average(x =>
                                    x.MarksObtained),
                                2)
                            : 0;


                    // --------------------------------------------
                    // Subject Results
                    // --------------------------------------------

                    var subjects =
                        group
                            .OrderBy(x =>
                                x.SubjectName)
                            .Select(x => new
                            {
                                markId =
                                    x.MarkId,

                                subject = new
                                {
                                    id =
                                        x.SubjectId,

                                    name =
                                        x.SubjectName
                                },

                                marksObtained =
                                    x.MarksObtained,

                                maximumMarks =
                                    x.MaximumMarks,

                                percentage =
                                    x.MaximumMarks > 0
                                        ? Math.Round(
                                            x.MarksObtained /
                                            x.MaximumMarks *
                                            100,
                                            2)
                                        : 0,

                                publishedAt =
                                    x.PublishedAt
                            })
                            .ToList();


                    return new
                    {
                        academicYear =
                            new
                            {
                                id =
                                    group.Key
                                        .AcademicYearId,

                                name =
                                    group.Key
                                        .AcademicYearName
                            },

                        term =
                            new
                            {
                                id =
                                    group.Key.TermId,

                                name =
                                    group.Key.TermName
                            },

                        exam =
                            new
                            {
                                id =
                                    group.Key.ExamId,

                                name =
                                    group.Key.ExamName,

                                examDate =
                                    group.Key.ExamDate
                            },

                        schoolClass =
                            new
                            {
                                id =
                                    group.First()
                                        .SchoolClassId,

                                name =
                                    group.First()
                                        .SchoolClassName,

                                grade =
                                    group.First()
                                        .GradeName,

                                section =
                                    group.First()
                                        .SectionName
                            },

                        subjects,

                        summary =
                            new
                            {
                                subjectCount =
                                    group.Count(),

                                totalMarks,

                                maximumTotal,

                                average,

                                percentage
                            }
                    };
                })
                .OrderByDescending(x =>
                    x.exam.examDate)
                .ToList();


        // --------------------------------------------------------
        // Return Response
        // --------------------------------------------------------

        return Ok(new
        {
            student =
                new
                {
                    student.Id,

                    student.IndexNumber,

                    student.FullName,

                    schoolClass =
                        new
                        {
                            student.SchoolClass.Id,

                            student.SchoolClass.Name,

                            grade =
                                student.SchoolClass
                                    .Grade.Name,

                            section =
                                student.SchoolClass
                                    .Grade
                                    .Section.Name
                        }
                },

            filters =
                new
                {
                    academicYearId,

                    termId,

                    examId
                },

            examCount =
                examSummaries.Count,

            results =
                examSummaries
        });
    }

    // ============================================================
    // MY ACADEMIC RESULTS
    //
    // Structure:
    //
    // Academic Year
    //      ↓
    // Term
    //      ↓
    // Exam
    //      ↓
    // Subjects + Exam Summary
    //
    // Only published results are returned.
    // ============================================================

    [HttpGet("my/academic")]
    public async Task<IActionResult> GetMyAcademicResults()
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
        // Find Student linked to logged-in user
        // --------------------------------------------------------

        var student =
            await _context.Students
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (student == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        // --------------------------------------------------------
        // Load Published Marks Only
        // --------------------------------------------------------

        var publishedMarks =
            await (
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
                    mark.StudentId == student.Id &&

                    mark.IsPublished &&

                    submission.Status ==
                        MarksSubmissionStatus.Published

                select new
                {
                    MarkId =
                        mark.Id,

                    // Academic Year
                    AcademicYearId =
                        mark.TeacherAssignment
                            .AcademicYear.Id,

                    AcademicYearName =
                        mark.TeacherAssignment
                            .AcademicYear.Name,

                    AcademicYearStartDate =
                        mark.TeacherAssignment
                            .AcademicYear.StartDate,

                    // Term
                    TermId =
                        mark.Exam
                            .AcademicTerm.Id,

                    TermName =
                        mark.Exam
                            .AcademicTerm.Name,

                    TermStartDate =
                        mark.Exam
                            .AcademicTerm.StartDate,

                    // Exam
                    ExamId =
                        mark.Exam.Id,

                    ExamName =
                        mark.Exam.Name,

                    ExamDate =
                        mark.Exam.ExamDate,

                    MaximumMarks =
                        mark.Exam.MaximumMarks,

                    // Subject
                    SubjectId =
                        mark.TeacherAssignment
                            .Subject.Id,

                    SubjectName =
                        mark.TeacherAssignment
                            .Subject.Name,

                    // Class
                    SchoolClassId =
                        mark.TeacherAssignment
                            .SchoolClass.Id,

                    SchoolClassName =
                        mark.TeacherAssignment
                            .SchoolClass.Name,

                    GradeName =
                        mark.TeacherAssignment
                            .SchoolClass
                            .Grade.Name,

                    SectionName =
                        mark.TeacherAssignment
                            .SchoolClass
                            .Grade
                            .Section.Name,

                    // Marks
                    MarksObtained =
                        mark.MarksObtained,

                    PublishedAt =
                        submission.PublishedAt
                })
                .ToListAsync();


        // --------------------------------------------------------
        // Group By Academic Year
        // --------------------------------------------------------

        var academicYears =
            publishedMarks

                .GroupBy(x => new
                {
                    x.AcademicYearId,
                    x.AcademicYearName,
                    x.AcademicYearStartDate
                })

                .OrderByDescending(year =>
                    year.Key.AcademicYearStartDate)

                .Select(yearGroup => new
                {
                    id =
                        yearGroup.Key
                            .AcademicYearId,

                    name =
                        yearGroup.Key
                            .AcademicYearName,

                    // ============================================
                    // TERMS
                    // ============================================

                    terms =
                        yearGroup

                            .GroupBy(x => new
                            {
                                x.TermId,
                                x.TermName,
                                x.TermStartDate
                            })

                            .OrderBy(term =>
                                term.Key.TermStartDate)

                            .Select(termGroup => new
                            {
                                id =
                                    termGroup.Key.TermId,

                                name =
                                    termGroup.Key.TermName,

                                // =================================
                                // EXAMS
                                // =================================

                                exams =
                                    termGroup

                                        .GroupBy(x => new
                                        {
                                            x.ExamId,
                                            x.ExamName,
                                            x.ExamDate
                                        })

                                        .OrderBy(exam =>
                                            exam.Key.ExamDate)

                                        .Select(examGroup =>
                                        {
                                            // ---------------------
                                            // Total Obtained
                                            // ---------------------

                                            var totalMarks =
                                                examGroup.Sum(x =>
                                                    x.MarksObtained);


                                            // ---------------------
                                            // Maximum Total
                                            // ---------------------

                                            var maximumTotal =
                                                examGroup.Sum(x =>
                                                    x.MaximumMarks);


                                            // ---------------------
                                            // Overall Percentage
                                            // ---------------------

                                            var percentage =
                                                maximumTotal > 0
                                                    ? Math.Round(
                                                        totalMarks /
                                                        maximumTotal *
                                                        100,
                                                        2)
                                                    : 0;


                                            // ---------------------
                                            // Average
                                            // ---------------------

                                            var average =
                                                examGroup.Any()
                                                    ? Math.Round(
                                                        examGroup
                                                            .Average(x =>
                                                                x.MarksObtained),
                                                        2)
                                                    : 0;


                                            // ---------------------
                                            // Subjects
                                            // ---------------------

                                            var subjects =
                                                examGroup

                                                    .OrderBy(x =>
                                                        x.SubjectName)

                                                    .Select(x => new
                                                    {
                                                        markId =
                                                            x.MarkId,

                                                        subject = new
                                                        {
                                                            id =
                                                                x.SubjectId,

                                                            name =
                                                                x.SubjectName
                                                        },

                                                        marksObtained =
                                                            x.MarksObtained,

                                                        maximumMarks =
                                                            x.MaximumMarks,

                                                        percentage =
                                                            x.MaximumMarks > 0
                                                                ? Math.Round(
                                                                    x.MarksObtained /
                                                                    x.MaximumMarks *
                                                                    100,
                                                                    2)
                                                                : 0,

                                                        publishedAt =
                                                            x.PublishedAt
                                                    })

                                                    .ToList();


                                            // ---------------------
                                            // Exam Result
                                            // ---------------------

                                            return new
                                            {
                                                id =
                                                    examGroup.Key
                                                        .ExamId,

                                                name =
                                                    examGroup.Key
                                                        .ExamName,

                                                examDate =
                                                    examGroup.Key
                                                        .ExamDate,

                                                schoolClass =
                                                    new
                                                    {
                                                        id =
                                                            examGroup
                                                                .First()
                                                                .SchoolClassId,

                                                        name =
                                                            examGroup
                                                                .First()
                                                                .SchoolClassName,

                                                        grade =
                                                            examGroup
                                                                .First()
                                                                .GradeName,

                                                        section =
                                                            examGroup
                                                                .First()
                                                                .SectionName
                                                    },

                                                subjects,

                                                summary =
                                                    new
                                                    {
                                                        subjectCount =
                                                            examGroup.Count(),

                                                        totalMarks,

                                                        maximumTotal,

                                                        average,

                                                        percentage
                                                    }
                                            };
                                        })

                                        .ToList()
                            })

                            .ToList()
                })

                .ToList();


        // --------------------------------------------------------
        // Return Response
        // --------------------------------------------------------

        return Ok(new
        {
            student = new
            {
                student.Id,

                student.IndexNumber,

                student.FullName,

                schoolClass = new
                {
                    student.SchoolClass.Id,

                    student.SchoolClass.Name,

                    grade =
                        student.SchoolClass
                            .Grade.Name,

                    section =
                        student.SchoolClass
                            .Grade
                            .Section.Name
                }
            },

            academicYearCount =
                academicYears.Count,

            academicYears
        });
    }
}