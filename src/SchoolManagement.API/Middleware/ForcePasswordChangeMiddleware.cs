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

        // Allow the password-change endpoint
        var path = context.Request.Path.Value?.ToLowerInvariant();

        if (path == "/api/auth/change-password")
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            await _next(context);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);

        if (user == null)
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return;
        }

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