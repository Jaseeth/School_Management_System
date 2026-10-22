using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Authorization;

public class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<IdentityRole> _roleManager;

    public PermissionAuthorizationHandler(
        ApplicationDbContext context,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _roleManager = roleManager;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var uid = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!await _context.Users.AnyAsync(u => u.Id == uid && u.IsActive)) return;
        var liveRoles = await (from ur in _context.UserRoles join r in _context.Roles on ur.RoleId equals r.Id where ur.UserId == uid select r.Name).ToListAsync();
        if (!context.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToHashSet().SetEquals(liveRoles)) return;

        var roleNames = context.User
            .Claims
            .Where(x => x.Type ==
                System.Security.Claims.ClaimTypes.Role)
            .Select(x => x.Value)
            .Distinct()
            .ToList();

        if (roleNames.Count == 0)
        {
            return;
        }

        var roleIds = new List<string>();

        foreach (var roleName in roleNames)
        {
            var role =
                await _roleManager.FindByNameAsync(roleName);

            if (role != null)
            {
                roleIds.Add(role.Id);
            }
        }

        if (roleIds.Count == 0)
        {
            return;
        }

        var hasPermission =
            await _context.RolePermissions
                .AnyAsync(x =>
                    roleIds.Contains(x.RoleId) &&
                    x.Permission.Name ==
                        requirement.PermissionName &&
                    x.Permission.IsActive);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
