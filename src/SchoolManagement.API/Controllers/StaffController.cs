using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Auditing;
using SchoolManagement.Application.Staff.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

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

    [HasPermission("Staff.Create")]
    [HttpPost]
    public async Task<IActionResult> CreateStaff(
        CreateStaffRequest request)
    {
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
                u.MustChangePassword
            })
            .ToListAsync();

        return Ok(staff);
    }
}
