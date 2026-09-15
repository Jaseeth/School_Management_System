using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Roles.DTOs;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly RoleManager<IdentityRole> _roleManager;

    public RolesController(
        RoleManager<IdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }

    // GET: api/roles
    [HttpGet]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _roleManager.Roles
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name
            })
            .ToListAsync();

        return Ok(roles);
    }

    // GET: api/roles/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetRole(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);

        if (role == null)
        {
            return NotFound(new
            {
                message = "Role not found."
            });
        }

        return Ok(new
        {
            role.Id,
            role.Name
        });
    }

    // POST: api/roles
    [HttpPost]
    public async Task<IActionResult> CreateRole(
        CreateRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                message = "Role name is required."
            });
        }

        var roleName = request.Name.Trim();

        var existingRole =
            await _roleManager.FindByNameAsync(roleName);

        if (existingRole != null)
        {
            return BadRequest(new
            {
                message = "Role already exists."
            });
        }

        var role = new IdentityRole
        {
            Name = roleName
        };

        var result =
            await _roleManager.CreateAsync(role);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to create role.",
                errors = result.Errors
                    .Select(x => x.Description)
            });
        }

        return Ok(new
        {
            message = "Role created successfully.",
            role.Id,
            role.Name
        });
    }

    // PUT: api/roles/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRole(
        string id,
        UpdateRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                message = "Role name is required."
            });
        }

        var role = await _roleManager.FindByIdAsync(id);

        if (role == null)
        {
            return NotFound(new
            {
                message = "Role not found."
            });
        }

        var roleName = request.Name.Trim();

        var existingRole =
            await _roleManager.FindByNameAsync(roleName);

        if (existingRole != null &&
            existingRole.Id != id)
        {
            return BadRequest(new
            {
                message = "Another role with this name already exists."
            });
        }

        role.Name = roleName;

        var result =
            await _roleManager.UpdateAsync(role);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to update role.",
                errors = result.Errors
                    .Select(x => x.Description)
            });
        }

        return Ok(new
        {
            message = "Role updated successfully.",
            role.Id,
            role.Name
        });
    }

    // DELETE: api/roles/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRole(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);

        if (role == null)
        {
            return NotFound(new
            {
                message = "Role not found."
            });
        }

        var result =
            await _roleManager.DeleteAsync(role);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to delete role.",
                errors = result.Errors
                    .Select(x => x.Description)
            });
        }

        return Ok(new
        {
            message = "Role deleted successfully."
        });
    }
}