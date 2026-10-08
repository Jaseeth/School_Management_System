using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Auditing;
using SchoolManagement.Application.Staff.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StaffController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditLogService _auditLogService;

    public StaffController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _auditLogService = auditLogService;
    }

    [Authorize]
    [HttpGet("my/profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsActive)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);

        var staff = await _context.Staff
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId && x.IsActive);

        if (staff == null && !roles.Contains("Admin"))
        {
            return StatusCode(403, new
            {
                message = "No active staff profile is linked to this account."
            });
        }

        return Ok(new
        {
            Id = staff?.Id,
            StaffNumber = staff?.StaffNumber,
            FullName = user.FullName,
            Designation = staff?.Designation ??
                (roles.Contains("Admin") ? "Administrator" : null),
            Email = user.Email,
            Roles = roles
        });
    }

    [Authorize]
    [HttpPut("my/profile")]
    public async Task<IActionResult> UpdateMyProfile(
        UpdateMyStaffProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsActive)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);

        var staff = await _context.Staff.FirstOrDefaultAsync(x =>
            x.ApplicationUserId == userId && x.IsActive);

        if (staff == null && !roles.Contains("Admin"))
        {
            return StatusCode(403, new
            {
                message = "No active staff profile is linked to this account."
            });
        }

        var fullName = request.FullName?.Trim();
        var email = request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 200)
        {
            return BadRequest(new
            {
                message = "Full name is required and must not exceed 200 characters."
            });
        }

        if (string.IsNullOrWhiteSpace(email) ||
            email.Length > 256 ||
            !System.Net.Mail.MailAddress.TryCreate(email, out var parsedEmail) ||
            !string.Equals(
                parsedEmail.Address,
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Enter a valid email address." });
        }

        var emailChanged = !string.Equals(
            user.Email,
            email,
            StringComparison.OrdinalIgnoreCase);

        if (emailChanged)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                !await _userManager.CheckPasswordAsync(
                    user,
                    request.CurrentPassword))
            {
                return BadRequest(new
                {
                    message = "Enter your correct current password to change your email."
                });
            }

            var existing = await _userManager.FindByEmailAsync(email);

            if (existing != null && existing.Id != user.Id)
            {
                return BadRequest(new
                {
                    message = "This email address is already registered."
                });
            }
        }

        var oldValues = new { user.FullName, user.Email };
        var strategy = _context.Database.CreateExecutionStrategy();

        var identityResult = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            user.FullName = fullName;

            if (emailChanged)
            {
                user.Email = email;
                user.UserName = email;
                user.EmailConfirmed = false;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return result;

            if (staff != null)
                staff.FullName = fullName;

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                action: "UpdateOwnProfile",
                entityName: staff == null ? "ApplicationUser" : "Staff",
                entityId: staff?.Id.ToString() ?? user.Id,
                description: $"User {user.Id} updated their profile.",
                oldValues: oldValues,
                newValues: new { user.FullName, user.Email });

            await transaction.CommitAsync();
            return result;
        });

        if (!identityResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to update staff profile.",
                errors = identityResult.Errors
                    .Select(x => x.Description)
                    .ToList()
            });
        }

        return Ok(new
        {
            message = "Profile updated successfully.",
            profile = new
            {
                Id = staff?.Id,
                StaffNumber = staff?.StaffNumber,
                FullName = user.FullName,
                Designation = staff?.Designation ??
                    (roles.Contains("Admin") ? "Administrator" : null),
                Email = user.Email
            }
        });
    }

    [HasPermission("Staff.Create")]
    [HttpPost]
    public async Task<IActionResult> CreateStaff(
        CreateStaffRequest request)
    {
        request.StaffNumber = request.StaffNumber.Trim();
        request.FullName = request.FullName.Trim();
        request.Email = request.Email.Trim();
        request.Designation = request.Designation?.Trim();
        request.RoleId = request.RoleId.Trim();

        if (string.IsNullOrWhiteSpace(request.StaffNumber))
        {
            return BadRequest(new
            {
                message = "Staff number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        var staffNumberExists =
            await _context.Staff
                .AnyAsync(x =>
                    x.StaffNumber == request.StaffNumber);

        if (staffNumberExists)
        {
            return BadRequest(new
            {
                message = "Staff number already exists."
            });
        }

        var existingUser =
            await _userManager.FindByEmailAsync(request.Email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                message = "Email address is already registered."
            });
        }

        var role =
            await _roleManager.FindByIdAsync(request.RoleId);

        if (role == null ||
            string.IsNullOrWhiteSpace(role.Name))
        {
            return BadRequest(new
            {
                message = "Invalid role."
            });
        }

        if (string.Equals(role.Name, "Student", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role.Name, "Parent", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Select a staff role. Student and Parent accounts must use their own account workflows."
            });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true
        };

        var createUserResult =
            await _userManager.CreateAsync(
                user,
                request.TemporaryPassword);

        if (!createUserResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to create staff account.",
                errors = createUserResult.Errors
                    .Select(x => x.Description)
            });
        }

        var roleResult =
            await _userManager.AddToRoleAsync(
                user,
                role.Name);

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            return BadRequest(new
            {
                message = "Unable to assign staff role.",
                errors = roleResult.Errors
                    .Select(x => x.Description)
            });
        }

        var staff = new Staff
        {
            StaffNumber = request.StaffNumber,
            FullName = request.FullName,
            Designation = request.Designation,
            ApplicationUserId = user.Id,
            IsActive = true
        };

        _context.Staff.Add(staff);

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "CreateAccount",
            entityName: "Staff",
            entityId: staff.Id.ToString(),
            description:
                $"Staff account {staff.StaffNumber} - {staff.FullName} was created.",
            newValues: new
            {
                staff.StaffNumber,
                staff.FullName,
                staff.Designation,
                Email = user.Email,
                Role = role.Name,
                staff.IsActive,
                user.MustChangePassword
            });

        return Ok(new
        {
            message = "Staff account created successfully.",
            staff.Id,
            staff.StaffNumber,
            staff.FullName,
            Email = user.Email,
            Role = role.Name,
            MustChangePassword = user.MustChangePassword
        });
    }

    [HasPermission("Staff.View")]
    [HttpGet]
    public async Task<IActionResult> GetStaff()
    {
        var staff = await (
            from s in _context.Staff
            join u in _context.Users
                on s.ApplicationUserId equals u.Id
            orderby s.FullName
            select new
            {
                s.Id,
                s.StaffNumber,
                s.FullName,
                Email = u.Email,
                s.Designation,
                s.ApplicationUserId,
                s.IsActive,
                AccountIsActive = u.IsActive,
                Roles = (from userRole in _context.UserRoles
                         join role in _context.Roles on userRole.RoleId equals role.Id
                         where userRole.UserId == u.Id
                         orderby role.Name
                         select role.Name).ToList(),
                u.MustChangePassword
            })
            .ToListAsync();

        return Ok(staff);
    }
}
