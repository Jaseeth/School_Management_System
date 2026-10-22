using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.API.Services;

/// <summary>
/// Resolves a staff member's persisted roles together with the authenticated
/// principal's role claims for the current request.
/// </summary>
public static class EffectiveStaffRoles
{
    public static async Task<HashSet<string>> GetAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        ClaimsPrincipal principal)
    {
        var roles = (await userManager.GetRolesAsync(user)).ToHashSet(StringComparer.Ordinal);
        roles.UnionWith(principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value));
        return roles;
    }
}
