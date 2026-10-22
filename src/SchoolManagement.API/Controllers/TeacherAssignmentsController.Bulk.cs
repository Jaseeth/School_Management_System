using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Authorization;

namespace SchoolManagement.API.Controllers;
public partial class TeacherAssignmentsController
{
    private IQueryable<Staff> TeachingStaff() => _context.Staff.Where(s => s.IsActive && (s.CanTeach == true ||
        _context.TeacherAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.Subject.IsActive)) &&
        _context.Users.Any(u => u.Id == s.ApplicationUserId && u.IsActive) &&
        _context.UserRoles.Any(ur => ur.UserId == s.ApplicationUserId && _context.Roles.Any(r => r.Id == ur.RoleId && (r.Name == "Admin" || r.Name == "Section Head" || r.Name == "Teacher"))));

    [HasPermission("TeacherAssignments.Manage"), HttpGet("options")]
    public async Task<IActionResult> Options()
    {
        var years = await _context.AcademicYears.Where(y => y.IsActive).OrderByDescending(y => y.StartDate).Select(y => new { y.Id, y.Name }).ToListAsync();
        var staff = await TeachingStaff().OrderBy(s => s.FullName).Select(s => new { s.Id, s.FullName, s.StaffNumber,
            subjects = s.TeachingSubjects.Where(t => t.Subject.IsActive).Select(t => new { id = t.SubjectId, name = t.Subject.Name }).ToList() }).ToListAsync();
        var staffIds = staff.Select(s => s.Id).ToList();
        var existingSubjects = await _context.TeacherAssignments.Where(a => staffIds.Contains(a.StaffId) && a.IsActive && a.Subject.IsActive)
            .Select(a => new { a.StaffId, id = a.SubjectId, name = a.Subject.Name }).Distinct().ToListAsync();
        var staffOptions = staff.Select(s => new { s.Id, s.FullName, s.StaffNumber,
            subjects = s.subjects.Concat(existingSubjects.Where(a => a.StaffId == s.Id).Select(a => new { a.id, a.name }))
                .GroupBy(a => a.id).Select(g => new { id = g.Key, name = g.First().name }).OrderBy(a => a.name).ToList() }).ToList();
        var classes = await _context.SchoolClasses.Where(c => c.IsActive && c.Grade.IsActive && c.Grade.Section.IsActive)
            .OrderBy(c => c.Grade.Section.Name).ThenBy(c => c.Grade.Name).ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, gradeId = c.GradeId, grade = c.Grade.Name, sectionId = c.Grade.SectionId, section = c.Grade.Section.Name }).ToListAsync();
        return Ok(new { years, staff = staffOptions, classes });
    }

    public record AssignmentRow(int SubjectId, int SchoolClassId);
    public record BulkAssignmentRequest(int AcademicYearId, int StaffId, List<AssignmentRow> Rows);
    [HasPermission("TeacherAssignments.Manage"), HttpPost("bulk")]
    public async Task<IActionResult> AssignBulk(BulkAssignmentRequest request)
    {
        if (request.Rows == null || request.Rows.Count is < 1 or > 100 || request.Rows.Any(r => r == null || r.SubjectId <= 0 || r.SchoolClassId <= 0))
            return BadRequest(new { message = "Add between 1 and 100 complete subject/class assignment rows." });
        if (request.Rows.Distinct().Count() != request.Rows.Count) return BadRequest(new { message = "The same subject and class appears more than once. Remove duplicate rows." });
        return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
            _context.ChangeTracker.Clear();
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await _context.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource = 'TeacherAssignmentManagement', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @r < 0 THROW 51000, 'Assignments are busy. Retry shortly.', 1;");
            if (!await _context.AcademicYears.AnyAsync(y => y.Id == request.AcademicYearId && y.IsActive)) return BadRequest(new { message = "Select an active academic year." });
            var staff = await TeachingStaff().Include(s => s.TeachingSubjects).FirstOrDefaultAsync(s => s.Id == request.StaffId);
            if (staff == null) return BadRequest(new { message = "Select active Admin, Section Head or Teacher staff marked as able to teach. Configure Teaching subjects in Staff Management first." });
            var subjectIds = request.Rows.Select(r => r.SubjectId).Distinct().ToList(); var classIds = request.Rows.Select(r => r.SchoolClassId).Distinct().ToList();
            var configuredSubjectIds = staff.TeachingSubjects.Select(t => t.SubjectId).Distinct().ToList();
            var eligibleSubjects = await _context.Subjects.Where(s => s.IsActive && subjectIds.Contains(s.Id) &&
                (configuredSubjectIds.Contains(s.Id) || _context.TeacherAssignments.Any(a => a.StaffId == staff.Id && a.SubjectId == s.Id && a.IsActive)))
                .Select(s => s.Id).Distinct().ToListAsync();
            if (eligibleSubjects.Count != subjectIds.Count)
                return BadRequest(new { message = "Every subject must be active and included in this staff member's saved teaching subjects." });
            if (await _context.SchoolClasses.CountAsync(c => classIds.Contains(c.Id) && c.IsActive && c.Grade.IsActive && c.Grade.Section.IsActive) != classIds.Count)
                return BadRequest(new { message = "Every class, grade and section must be active." });
            var existing = await _context.TeacherAssignments.Where(a => a.AcademicYearId == request.AcademicYearId && classIds.Contains(a.SchoolClassId) && subjectIds.Contains(a.SubjectId)).ToListAsync();
            var conflicts = new List<string>();
            foreach (var row in request.Rows) {
                var same = existing.FirstOrDefault(a => a.StaffId == request.StaffId && a.SubjectId == row.SubjectId && a.SchoolClassId == row.SchoolClassId);
                if (same?.IsActive == true) conflicts.Add($"Row {request.Rows.IndexOf(row) + 1} is already assigned to this staff member for this class and subject.");
            }
            if (conflicts.Count > 0) return Conflict(new { message = string.Join(" ", conflicts) + " Nothing was saved." });
            var saved = new List<TeacherAssignment>(); var reactivated = 0;
            foreach (var row in request.Rows) {
                var assignment = existing.FirstOrDefault(a => a.StaffId == request.StaffId && a.SubjectId == row.SubjectId && a.SchoolClassId == row.SchoolClassId);
                if (assignment == null) { assignment = new() { StaffId = request.StaffId, AcademicYearId = request.AcademicYearId, SubjectId = row.SubjectId, SchoolClassId = row.SchoolClassId }; _context.TeacherAssignments.Add(assignment); }
                else { assignment.IsActive = true; reactivated++; }
                saved.Add(assignment);
            }
            await _context.SaveChangesAsync();
            if (_audit != null) {
                var savedIds = saved.Select(a => a.Id).ToList();
                await _audit.LogAsync("Assign", "TeacherAssignment", description: $"Saved {saved.Count} teaching assignments for {staff.FullName} ({staff.StaffNumber}).", newValues: new {
                    Staff = staff.FullName, StaffNumber = staff.StaffNumber, AcademicYear = await _context.AcademicYears.Where(y => y.Id == request.AcademicYearId).Select(y => y.Name).SingleAsync(), Reactivated = reactivated,
                    Assignments = await _context.TeacherAssignments.Where(a => savedIds.Contains(a.Id)).Select(a => new { Subject = a.Subject.Name, Section = a.SchoolClass.Grade.Section.Name, Grade = a.SchoolClass.Grade.Name, Class = a.SchoolClass.Name }).ToListAsync() });
            }
            await tx.CommitAsync();
            return Ok(new { message = $"Saved {saved.Count} assignment(s), including {reactivated} reactivated. No other assignments were changed.", assignments = saved.Select(a => new { a.Id, a.SubjectId, a.SchoolClassId }), count = saved.Count });
        });
    }
}
