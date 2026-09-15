using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Authentication.DTOs;
using SchoolManagement.Infrastructure.Identity;

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
                Roles = roles
            });
    }
}