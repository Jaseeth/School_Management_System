using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Authentication.DTOs;
using SchoolManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request)
    {
        var user =
            await _userManager.FindByEmailAsync(
                request.Email);

        if (user == null)
        {
            return Unauthorized(
                new
                {
                    message = "Invalid email or password."
                });
        }

        if (!user.IsActive)
        {
            return Unauthorized(
                new
                {
                    message = "Account is inactive."
                });
        }

        var passwordValid =
            await _userManager.CheckPasswordAsync(
                user,
                request.Password);

        if (!passwordValid)
        {
            return Unauthorized(
                new
                {
                    message = "Invalid email or password."
                });
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var tokenResult =
            await _jwtTokenService
                .GenerateTokenAsync(user);

        return Ok(
            new LoginResponse
            {
                Token = tokenResult.Token,
                ExpiresAt = tokenResult.ExpiresAt,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Roles = roles,
                MustChangePassword = user.MustChangePassword
            });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
    ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return BadRequest(new
            {
                message = "Current password is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new
            {
                message = "New password is required."
            });
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "New password and confirmation do not match."
            });
        }

        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var result = await _userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to change password.",
                errors = result.Errors
                    .Select(x => x.Description)
            });
        }

        user.MustChangePassword = false;

        await _userManager.UpdateAsync(user);

        return Ok(new
        {
            message = "Password changed successfully.",
            mustChangePassword = false
        });
    }
}