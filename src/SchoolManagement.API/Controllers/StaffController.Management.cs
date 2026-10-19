using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.API.Controllers;

public partial class StaffController
{
    public record StaffEdit(string FullName, string? Designation, string? RoleName = null);
    public record StaffStatus(bool IsActive, string Reason);
    public record StaffReset(string TemporaryPassword, string Reason);
    private static bool Privileged(IEnumerable<string> roles) => roles.Any(r => r is "Admin" or "Principal" or "Deputy Principal");

    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    [HttpPut("{id:int}/management")]
    public Task<IActionResult> EditStaff(int id, StaffEdit request) => ManageStaff(id, request, null, null);
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    [HttpPut("{id:int}/status")]
    public Task<IActionResult> SetStaffStatus(int id, StaffStatus request) => ManageStaff(id, null, request, null);
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/reset-password")]
    public Task<IActionResult> ResetStaffPassword(int id, StaffReset request) => ManageStaff(id, null, null, request);

    private async Task<IActionResult> ManageStaff(int id, StaffEdit? edit, StaffStatus? status, StaffReset? reset)
    {
        if (edit != null && (string.IsNullOrWhiteSpace(edit.FullName) || edit.FullName.Length > 200 || (edit.Designation?.Length ?? 0) > 200)) return BadRequest(new { message = "Enter a staff name and designation of up to 200 characters." });
        var reason = status?.Reason ?? reset?.Reason;
        if ((status != null || reset != null) && (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)) return BadRequest(new { message = "Enter a reason of up to 1000 characters." });
        if (reset != null && (string.IsNullOrWhiteSpace(reset.TemporaryPassword) || reset.TemporaryPassword.Length > 128)) return BadRequest(new { message = "Enter a temporary password that meets the password requirements." });
        return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
            _context.ChangeTracker.Clear();
            await using var tx = await _context.Database.BeginTransactionAsync();
            await _context.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource = 'StaffAccountLifecycle', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @r < 0 THROW 51000, 'Staff management is busy. Retry shortly.', 1;");
            var actor = await _userManager.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            if (actor == null || !actor.IsActive) return Unauthorized();
            var actorRoles = await _userManager.GetRolesAsync(actor); var admin = actorRoles.Contains("Admin");
            if (!Privileged(actorRoles) || reset != null && !admin) return Forbid();
            var staff = await _context.Staff.SingleOrDefaultAsync(s => s.Id == id);
            if (staff?.ApplicationUserId == null) return NotFound();
            var user = await _userManager.FindByIdAsync(staff.ApplicationUserId); if (user == null) return NotFound();
            if (user.Id == actor.Id) return BadRequest(new { message = "Use My Profile or Change Password for your own account. You cannot change your own role or active status here." });
            var roles = await _userManager.GetRolesAsync(user);
            if (!admin && Privileged(roles)) return Forbid();
            if (edit?.RoleName != null && (!await _roleManager.RoleExistsAsync(edit.RoleName) || edit.RoleName is "Student" or "Parent" || !admin && Privileged([edit.RoleName]))) return BadRequest(new { message = "Select a permitted staff role. Leadership accounts are managed by Admin." });
            var before = new { staff.FullName, staff.Designation, staff.IsActive, userActive = user.IsActive, Roles = roles.ToArray() };
            if (edit != null) {
                staff.FullName = edit.FullName.Trim(); staff.Designation = edit.Designation?.Trim(); user.FullName = staff.FullName;
                if (edit.RoleName != null && !roles.SequenceEqual([edit.RoleName])) {
                    var removed = await _userManager.RemoveFromRolesAsync(user, roles); if (!removed.Succeeded) return BadRequest(new { message = "Unable to update roles." });
                    var added = await _userManager.AddToRoleAsync(user, edit.RoleName); if (!added.Succeeded) return BadRequest(new { message = "Unable to assign this role." });
                }
            }
            if (status != null) { staff.IsActive = status.IsActive; user.IsActive = status.IsActive; }
            if (reset != null) {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, reset.TemporaryPassword);
                if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
                user.MustChangePassword = true;
            }
            var saved = await _userManager.UpdateAsync(user); if (!saved.Succeeded) return BadRequest(new { message = "Unable to save staff account." });
            var stamp = await _userManager.UpdateSecurityStampAsync(user); if (!stamp.Succeeded) return BadRequest(new { message = "Unable to invalidate old sessions." });
            await _context.SaveChangesAsync();
            await _auditLogService.LogAsync(reset != null ? "ResetStaffPassword" : status != null ? "SetStaffStatus" : "UpdateStaff", "Staff", staff.Id.ToString(), reason ?? "Staff profile/role updated.", before, new { staff.FullName, staff.Designation, staff.IsActive, userActive = user.IsActive, Roles = await _userManager.GetRolesAsync(user), user.MustChangePassword });
            await tx.CommitAsync();
            return Ok(new { message = reset != null ? "Temporary password set. The staff member must change it at sign-in. Previous sessions are invalidated." : "Staff account updated. Previous sessions are invalidated." });
        });
    }
}
