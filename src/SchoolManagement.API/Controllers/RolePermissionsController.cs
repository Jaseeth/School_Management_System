using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Permissions.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/roles")]
public class RolePermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<IdentityRole> _roleManager;

    public RolePermissionsController(
        ApplicationDbContext context,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _roleManager = roleManager;
    }

    // GET: api/roles/{roleId}/permissions
    [HttpGet("{roleId}/permissions")]
    public async Task<IActionResult> GetRolePermissions(
        string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);

        if (role == null)
        {
            return NotFound(new
            {
                message = "Role not found."
            });
        }

        var permissions = await _context.RolePermissions
            .Where(x => x.RoleId == roleId)
            .Include(x => x.Permission)
            .Select(x => new
            {
                x.Permission.Id,
                x.Permission.Name,
                x.Permission.Description
            })
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Ok(new
        {
            roleId = role.Id,
            roleName = role.Name,
            permissions
        });
    }

    // PUT: api/roles/{roleId}/permissions
    [HttpPut("{roleId}/permissions")]
    public async Task<IActionResult> AssignPermissions(
        string roleId,
        AssignRolePermissionsRequest request)
    {
        var role = await _roleManager.FindByIdAsync(roleId);

        if (role == null)
        {
            return NotFound(new
            {
                message = "Role not found."
            });
        }

        var permissionIds =
            request.PermissionIds
                .Distinct()
                .ToList();

        var validPermissions = await _context.Permissions
            .Where(x =>
                permissionIds.Contains(x.Id) &&
                x.IsActive)
            .Select(x => x.Id)
            .ToListAsync();

        if (validPermissions.Count != permissionIds.Count)
        {
            return BadRequest(new
            {
                message =
                    "One or more permission IDs are invalid or inactive."
            });
        }

        var existing = await _context.RolePermissions
            .Where(x => x.RoleId == roleId)
            .ToListAsync();

        _context.RolePermissions.RemoveRange(existing);

        foreach (var permissionId in permissionIds)
        {
            _context.RolePermissions.Add(
                new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId
                });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Role permissions updated successfully."
        });
    }
}