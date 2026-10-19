using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Application.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Users.DTOs;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Authorization;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/users")]
public class UserRolesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _audit;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserRolesController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager, ApplicationDbContext context, IAuditLogService audit)
    {
        _context = context; _audit = audit;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // GET: api/users/{userId}/roles
    [HasPermission("UserRoles.View")]
    [HttpGet("{userId}/roles")]
    public async Task<IActionResult> GetUserRoles(
        string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            userId = user.Id,
            user.FullName,
            user.Email,
            roles
        });
    }

    // PUT: api/users/{userId}/role
    [HasPermission("UserRoles.Manage")]
    [HttpPut("{userId}/role")]
    public async Task<IActionResult> AssignRole(
        string userId,
        AssignUserRoleRequest request)
    {
        var staff = await _context.Staff.AsNoTracking().SingleOrDefaultAsync(s => s.ApplicationUserId == userId);
        var role = await _roleManager.FindByIdAsync(request.RoleId);
        if (staff == null || string.IsNullOrEmpty(role?.Name)) return BadRequest(new { message = "Select a valid staff account and role." });
        var manager = new StaffController(_context, _userManager, _roleManager, _audit) { ControllerContext = ControllerContext };
        return await manager.EditStaff(staff.Id, new(staff.FullName, staff.Designation, role.Name));
    }
}
