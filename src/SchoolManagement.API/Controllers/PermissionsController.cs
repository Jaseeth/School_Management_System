using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Permissions.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PermissionsController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/permissions
    [HttpGet]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _context.Permissions
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.IsActive
            })
            .ToListAsync();

        return Ok(permissions);
    }

    // GET: api/permissions/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPermission(int id)
    {
        var permission = await _context.Permissions
            .FirstOrDefaultAsync(x => x.Id == id);

        if (permission == null)
        {
            return NotFound(new
            {
                message = "Permission not found."
            });
        }

        return Ok(permission);
    }

    // POST: api/permissions
    [HttpPost]
    public async Task<IActionResult> CreatePermission(
        CreatePermissionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                message = "Permission name is required."
            });
        }

        var permissionName = request.Name.Trim();

        var exists = await _context.Permissions
            .AnyAsync(x => x.Name == permissionName);

        if (exists)
        {
            return BadRequest(new
            {
                message = "Permission already exists."
            });
        }

        var permission = new Permission
        {
            Name = permissionName,
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = true
        };

        _context.Permissions.Add(permission);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Permission created successfully.",
            permission.Id,
            permission.Name
        });
    }

    // PUT: api/permissions/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePermission(
        int id,
        UpdatePermissionRequest request)
    {
        var permission = await _context.Permissions
            .FirstOrDefaultAsync(x => x.Id == id);

        if (permission == null)
        {
            return NotFound(new
            {
                message = "Permission not found."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                message = "Permission name is required."
            });
        }

        var permissionName = request.Name.Trim();

        var duplicate = await _context.Permissions
            .AnyAsync(x =>
                x.Name == permissionName &&
                x.Id != id);

        if (duplicate)
        {
            return BadRequest(new
            {
                message = "Another permission with this name already exists."
            });
        }

        permission.Name = permissionName;
        permission.Description =
            request.Description?.Trim() ?? string.Empty;
        permission.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Permission updated successfully."
        });
    }
}