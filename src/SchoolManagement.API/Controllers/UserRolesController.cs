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
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserRolesController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
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
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        var role = await _roleManager.FindByIdAsync(
            request.RoleId);

        if (role == null || string.IsNullOrWhiteSpace(role.Name))
        {
            return BadRequest(new
            {
                message = "Invalid role."
            });
        }

        // Remove current roles
        var currentRoles =
            await _userManager.GetRolesAsync(user);

        if (currentRoles.Count > 0)
        {
            var removeResult =
                await _userManager.RemoveFromRolesAsync(
                    user,
                    currentRoles);

            if (!removeResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Unable to remove current roles.",
                    errors = removeResult.Errors
                        .Select(x => x.Description)
                });
            }
        }

        // Assign new role
        var addResult =
            await _userManager.AddToRoleAsync(
                user,
                role.Name);

        if (!addResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to assign role.",
                errors = addResult.Errors
                    .Select(x => x.Description)
            });
        }

        return Ok(new
        {
            message = "User role updated successfully.",
            role = role.Name
        });
    }
}