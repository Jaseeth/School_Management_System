using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
namespace SchoolManagement.API.Controllers;
public partial class StaffController
{
    public record TeachingEdit(bool? CanTeach, List<int>? SubjectIds);
    [Authorize(Roles = "Admin,Principal,Deputy Principal"), HttpPut("{id:int}/teaching")]
    public async Task<IActionResult> UpdateTeaching(int id, TeachingEdit request)
    {
        if (!request.CanTeach.HasValue) return BadRequest(new { message = "Choose whether the staff member can teach." });
        return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
            _context.ChangeTracker.Clear(); await using var tx = await _context.Database.BeginTransactionAsync();
            await _context.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource = 'TeacherAssignmentManagement', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @r < 0 THROW 51000, 'Teaching settings are busy. Retry shortly.', 1;");
            var actor = await _userManager.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            if (actor == null || !actor.IsActive) return Unauthorized();
            var actorRoles = await _userManager.GetRolesAsync(actor); if (!Privileged(actorRoles)) return Forbid();
            var staff = await _context.Staff.Include(s => s.TeachingSubjects).SingleOrDefaultAsync(s => s.Id == id);
            if (staff?.ApplicationUserId == null) return NotFound();
            var target = await _userManager.FindByIdAsync(staff.ApplicationUserId); if (target == null) return NotFound();
            var roles = await _userManager.GetRolesAsync(target);
            if (Privileged(roles) && !actorRoles.Contains("Admin")) return Forbid();
            if (!roles.Any(r => r is "Admin" or "Section Head" or "Teacher")) return BadRequest(new { message = "Teaching subjects apply to Admin, Section Head and Teacher staff." });
            var ids = request.CanTeach == true ? (request.SubjectIds ?? []).Distinct().ToList() : [];
            if (request.CanTeach == true && (ids.Count is < 1 or > 500 || await _context.Subjects.CountAsync(s => s.IsActive && ids.Contains(s.Id)) != ids.Count)) return BadRequest(new { message = "Select one or more active subjects." });
            if (await _context.TeacherAssignments.AnyAsync(a => a.StaffId == id && a.IsActive && !ids.Contains(a.SubjectId)) || request.CanTeach == false && await _context.ClassTeacherAssignments.AnyAsync(a => a.StaffId == id && a.IsActive))
                return BadRequest(new { message = "Deactivate affected active teaching assignments before removing a teaching subject or choosing No." });
            var before = new { staff.CanTeach, Subjects = staff.TeachingSubjects.Select(s => s.SubjectId).ToArray() };
            _context.RemoveRange(staff.TeachingSubjects.Where(s => !ids.Contains(s.SubjectId)));
            foreach (var subjectId in ids.Except(staff.TeachingSubjects.Select(s => s.SubjectId)).ToList()) staff.TeachingSubjects.Add(new StaffTeachingSubject { SubjectId = subjectId });
            staff.CanTeach = request.CanTeach; await _context.SaveChangesAsync();
            await _auditLogService.LogAsync("Update", "Staff", id.ToString(), $"Updated teaching capabilities for {staff.FullName} ({staff.StaffNumber}).", before,
                new { staff.CanTeach, Subjects = await _context.Subjects.Where(s => ids.Contains(s.Id)).Select(s => s.Name).ToListAsync() });
            await tx.CommitAsync(); return Ok(new { message = "Teaching capabilities saved." });
        });
    }
}
