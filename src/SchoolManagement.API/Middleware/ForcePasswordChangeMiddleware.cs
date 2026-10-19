using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.API.Middleware;

public class ForcePasswordChangeMiddleware
{
    private readonly RequestDelegate _next;

    public ForcePasswordChangeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager)
    {
        // User is not logged in
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value?.ToLowerInvariant();

        if (path is "/api/auth/login" or "/api/auth/student-login" || path?.StartsWith("/api/auth/staff-forgot-password/") == true || path?.StartsWith("/api/student-registration/") == true) { await _next(context); return; }

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var user = await userManager.FindByIdAsync(userId);

        if (user == null || !user.IsActive)
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return;
        }

        var tokenStamp = context.User.FindFirstValue("session_stamp");
        var currentRoles = await userManager.GetRolesAsync(user);
        var tokenRoles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        if (tokenStamp == null || tokenStamp != await userManager.GetSecurityStampAsync(user) || !tokenRoles.SetEquals(currentRoles)) {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Your account access changed. Sign in again to continue." });
            return;
        }
        if (path == "/api/auth/change-password") { await _next(context); return; }
        if (user.MustChangePassword)
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            await context.Response.WriteAsJsonAsync(new
            {
                message =
                    "You must change your temporary password before accessing the system.",
                mustChangePassword = true
            });

            return;
        }

        await _next(context);
    }
}