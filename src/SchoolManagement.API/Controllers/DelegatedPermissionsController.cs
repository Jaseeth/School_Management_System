using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SchoolManagement.Application.Permissions.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/delegated-permissions")]
public class DelegatedPermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DelegatedPermissionsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // =====================================
    // Grant Permission
    // =====================================

    [HasPermission("DelegatedPermissions.Manage")]
    [HttpPost("grant")]
    public async Task<IActionResult> GrantPermission(
        GrantStaffPermissionRequest request)
    {
        var grantedByStaff =
            await GetLoggedInStaffAsync();

        if (grantedByStaff == null)
        {
            return Forbid();
        }

        // ---------------------------------
        // Validate target Staff
        // ---------------------------------

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.Id == request.StaffId &&
                    x.IsActive);

        if (staff == null)
        {
            return BadRequest(new
            {
                message = "Invalid staff member."
            });
        }

        if (string.IsNullOrWhiteSpace(
            staff.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Selected staff member does not have a user account."
            });
        }

        // ---------------------------------
        // Target must be a Teacher
        // ---------------------------------

        var targetUser =
            await _userManager.FindByIdAsync(
                staff.ApplicationUserId);

        if (targetUser == null)
        {
            return BadRequest(new
            {
                message =
                    "Selected staff user account was not found."
            });
        }

        var isTeacher =
            await _userManager.IsInRoleAsync(
                targetUser,
                "Teacher");

        if (!isTeacher)
        {
            return BadRequest(new
            {
                message =
                    "Marks permissions can only be delegated to a teacher."
            });
        }

        // ---------------------------------
        // Validate Permission
        // ---------------------------------

        var permission =
            await _context.Permissions
                .FirstOrDefaultAsync(x =>
                    x.Id == request.PermissionId &&
                    x.IsActive);

        if (permission == null)
        {
            return BadRequest(new
            {
                message = "Invalid permission."
            });
        }

        // Only these individual permissions
        // may currently be delegated.
        var allowedDelegatedPermissions =
            new[]
            {
                "Marks.Review",
                "Marks.Publish"
            };

        if (!allowedDelegatedPermissions
            .Contains(permission.Name))
        {
            return BadRequest(new
            {
                message =
                    "Only Marks.Review and Marks.Publish can currently be delegated."
            });
        }

        // ---------------------------------
        // Section is mandatory
        // ---------------------------------

        if (!request.SectionId.HasValue)
        {
            return BadRequest(new
            {
                message =
                    "Section is required for marks permission delegation."
            });
        }

        var section =
            await _context.Sections
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.SectionId.Value &&
                    x.IsActive);

        if (section == null)
        {
            return BadRequest(new
            {
                message = "Invalid section."
            });
        }

        // ---------------------------------
        // Check current user's section scope
        // ---------------------------------

        var canManageSection =
            await CanManageSectionAsync(
                grantedByStaff,
                section.Id);

        if (!canManageSection)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You are not allowed to delegate permissions for this section."
                });
        }

        // ---------------------------------
        // Prevent duplicate active grant
        // ---------------------------------

        var existing =
            await _context
                .StaffPermissionDelegations
                .FirstOrDefaultAsync(x =>
                    x.StaffId ==
                        request.StaffId &&
                    x.PermissionId ==
                        request.PermissionId &&
                    x.SectionId ==
                        request.SectionId &&
                    x.IsActive);

        if (existing != null)
        {
            return BadRequest(new
            {
                message =
                    "This delegated permission is already active for this teacher and section."
            });
        }

        // ---------------------------------
        // Create lifetime delegation
        // ---------------------------------

        var delegation =
            new StaffPermissionDelegation
            {
                StaffId =
                    request.StaffId,

                PermissionId =
                    request.PermissionId,

                SectionId =
                    request.SectionId,

                GrantedByStaffId =
                    grantedByStaff.Id,

                GrantedAt =
                    DateTime.UtcNow,

                IsActive =
                    true
            };

        _context.StaffPermissionDelegations
            .Add(delegation);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Permission delegated successfully.",

            delegation.Id,

            staff = new
            {
                staff.Id,
                staff.StaffNumber,
                staff.FullName
            },

            permission = new
            {
                permission.Id,
                permission.Name
            },

            section = new
            {
                section.Id,
                section.Name
            },

            grantedBy = new
            {
                grantedByStaff.Id,
                grantedByStaff.StaffNumber,
                grantedByStaff.FullName
            },

            delegation.GrantedAt,

            // Lifetime permission.
            // It remains active until revoked.
            expiresAt = (DateTime?)null,

            delegation.IsActive
        });
    }

    // =====================================
    // View Active Delegations
    // =====================================

    [HasPermission("DelegatedPermissions.View")]
    [HttpGet]
    public async Task<IActionResult> GetDelegations()
    {
        var currentStaff =
            await GetLoggedInStaffAsync();

        if (currentStaff == null)
        {
            return Forbid();
        }

        var hasGlobalAccess =
            await HasRolePermissionAsync(
                "SectionHeads.Manage");

        var query =
            _context
                .StaffPermissionDelegations
                .Where(x =>
                    x.IsActive &&
                    x.Permission.IsActive);

        // ---------------------------------
        // Section Head:
        // only see their assigned sections
        // ---------------------------------

        if (!hasGlobalAccess)
        {
            var sectionIds =
                await GetManagedSectionIdsAsync(
                    currentStaff.Id);

            if (sectionIds.Count == 0)
            {
                return Ok(Array.Empty<object>());
            }

            query =
                query.Where(x =>
                    x.SectionId.HasValue &&
                    sectionIds.Contains(
                        x.SectionId.Value));
        }

        var delegations =
            await query
                .OrderBy(x =>
                    x.Section!.Name)
                .ThenBy(x =>
                    x.Staff.FullName)
                .ThenBy(x =>
                    x.Permission.Name)
                .Select(x => new
                {
                    x.Id,

                    Staff = new
                    {
                        x.Staff.Id,
                        x.Staff.StaffNumber,
                        x.Staff.FullName
                    },

                    Permission = new
                    {
                        x.Permission.Id,
                        x.Permission.Name
                    },

                    Section =
                        x.Section == null
                            ? null
                            : new
                            {
                                x.Section.Id,
                                x.Section.Name
                            },

                    GrantedBy = new
                    {
                        x.GrantedByStaff.Id,
                        x.GrantedByStaff.StaffNumber,
                        x.GrantedByStaff.FullName
                    },

                    x.GrantedAt,

                    ExpiresAt =
                        (DateTime?)null,

                    x.IsActive
                })
                .ToListAsync();

        return Ok(delegations);
    }

    // =====================================
    // View Delegation History
    // =====================================

    [HasPermission("DelegatedPermissions.View")]
    [HttpGet("history")]
    public async Task<IActionResult>
        GetDelegationHistory()
    {
        var currentStaff =
            await GetLoggedInStaffAsync();

        if (currentStaff == null)
        {
            return Forbid();
        }

        var hasGlobalAccess =
            await HasRolePermissionAsync(
                "SectionHeads.Manage");

        var query =
            _context
                .StaffPermissionDelegations
                .AsQueryable();

        // ---------------------------------
        // Section Head:
        // history only for own sections
        // ---------------------------------

        if (!hasGlobalAccess)
        {
            var sectionIds =
                await GetManagedSectionIdsAsync(
                    currentStaff.Id);

            if (sectionIds.Count == 0)
            {
                return Ok(Array.Empty<object>());
            }

            query =
                query.Where(x =>
                    x.SectionId.HasValue &&
                    sectionIds.Contains(
                        x.SectionId.Value));
        }

        var delegations =
            await query
                .OrderByDescending(x =>
                    x.GrantedAt)
                .Select(x => new
                {
                    x.Id,

                    Staff = new
                    {
                        x.Staff.Id,
                        x.Staff.StaffNumber,
                        x.Staff.FullName
                    },

                    Permission = new
                    {
                        x.Permission.Id,
                        x.Permission.Name
                    },

                    Section =
                        x.Section == null
                            ? null
                            : new
                            {
                                x.Section.Id,
                                x.Section.Name
                            },

                    GrantedBy = new
                    {
                        x.GrantedByStaff.Id,
                        x.GrantedByStaff.StaffNumber,
                        x.GrantedByStaff.FullName
                    },

                    x.GrantedAt,

                    ExpiresAt =
                        (DateTime?)null,

                    x.IsActive,

                    RevokedBy =
                        x.RevokedByStaff == null
                            ? null
                            : new
                            {
                                x.RevokedByStaff.Id,
                                x.RevokedByStaff.StaffNumber,
                                x.RevokedByStaff.FullName
                            },

                    x.RevokedAt
                })
                .ToListAsync();

        return Ok(delegations);
    }

    // =====================================
    // Revoke Permission
    // =====================================

    [HasPermission("DelegatedPermissions.Manage")]
    [HttpPost("{id:int}/revoke")]
    public async Task<IActionResult> RevokePermission(
        int id)
    {
        var revokedByStaff =
            await GetLoggedInStaffAsync();

        if (revokedByStaff == null)
        {
            return Forbid();
        }

        var delegation =
            await _context
                .StaffPermissionDelegations
                .Include(x => x.Staff)
                .Include(x => x.Permission)
                .Include(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.IsActive);

        if (delegation == null)
        {
            return NotFound(new
            {
                message =
                    "Active delegated permission not found."
            });
        }

        if (!delegation.SectionId.HasValue)
        {
            return BadRequest(new
            {
                message =
                    "This delegated permission does not have a section."
            });
        }

        // ---------------------------------
        // Check revoke section scope
        // ---------------------------------

        var canManageSection =
            await CanManageSectionAsync(
                revokedByStaff,
                delegation.SectionId.Value);

        if (!canManageSection)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You are not allowed to revoke permissions for this section."
                });
        }

        // ---------------------------------
        // Revoke but keep audit history
        // ---------------------------------

        delegation.IsActive =
            false;

        delegation.RevokedByStaffId =
            revokedByStaff.Id;

        delegation.RevokedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Delegated permission revoked successfully.",

            delegation.Id,

            staff = new
            {
                delegation.Staff.Id,
                delegation.Staff.StaffNumber,
                delegation.Staff.FullName
            },

            permission = new
            {
                delegation.Permission.Id,
                delegation.Permission.Name
            },

            section =
                delegation.Section == null
                    ? null
                    : new
                    {
                        delegation.Section.Id,
                        delegation.Section.Name
                    },

            delegation.RevokedAt,

            revokedBy = new
            {
                revokedByStaff.Id,
                revokedByStaff.StaffNumber,
                revokedByStaff.FullName
            }
        });
    }

    // =====================================
    // Check normal role permission
    // =====================================

    private async Task<bool>
        HasRolePermissionAsync(
            string permissionName)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var roleIds =
            await _context.UserRoles
                .Where(x =>
                    x.UserId == userId)
                .Select(x =>
                    x.RoleId)
                .ToListAsync();

        if (roleIds.Count == 0)
        {
            return false;
        }

        return await _context.RolePermissions
            .AnyAsync(x =>
                roleIds.Contains(x.RoleId) &&
                x.Permission.Name ==
                    permissionName &&
                x.Permission.IsActive);
    }

    // =====================================
    // Can Current Staff Manage Section?
    // =====================================

    private async Task<bool>
        CanManageSectionAsync(
            Staff currentStaff,
            int sectionId)
    {
        // ---------------------------------
        // Global managers
        // Example: Admin / Principal
        // ---------------------------------

        var hasGlobalSectionManagement =
            await HasRolePermissionAsync(
                "SectionHeads.Manage");

        if (hasGlobalSectionManagement)
        {
            return true;
        }

        // ---------------------------------
        // Otherwise user must currently
        // be assigned as Section Head
        // for this exact section.
        // ---------------------------------

        var today =
            DateTime.UtcNow.Date;

        return await _context
            .SectionHeadAssignments
            .AnyAsync(x =>
                x.StaffId ==
                    currentStaff.Id &&

                x.SectionId ==
                    sectionId &&

                x.IsActive &&

                x.AcademicYear.StartDate
                    <= today &&

                x.AcademicYear.EndDate
                    >= today);
    }

    // =====================================
    // Sections managed by Section Head
    // =====================================

    private async Task<List<int>>
        GetManagedSectionIdsAsync(
            int staffId)
    {
        var today =
            DateTime.UtcNow.Date;

        return await _context
            .SectionHeadAssignments
            .Where(x =>
                x.StaffId ==
                    staffId &&

                x.IsActive &&

                x.AcademicYear.StartDate
                    <= today &&

                x.AcademicYear.EndDate
                    >= today)
            .Select(x =>
                x.SectionId)
            .Distinct()
            .ToListAsync();
    }

    // =====================================
    // Current Logged-In Staff
    // =====================================

    private async Task<Staff?>
        GetLoggedInStaffAsync()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId ==
                    userId &&
                x.IsActive);
    }
}