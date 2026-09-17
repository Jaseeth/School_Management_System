using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Authentication.DTOs;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;
using System.Security.Cryptography;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        ApplicationDbContext context,
        IEmailService emailService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _emailService = emailService;
    }


    // ============================================================
    // LOGIN
    // ============================================================

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request)
    {
        var user =
            await _userManager.FindByEmailAsync(
                request.Email);

        if (user == null)
        {
            return Unauthorized(new
            {
                message =
                    "Invalid email or password."
            });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message =
                    "Account is inactive."
            });
        }

        var passwordValid =
            await _userManager.CheckPasswordAsync(
                user,
                request.Password);

        if (!passwordValid)
        {
            return Unauthorized(new
            {
                message =
                    "Invalid email or password."
            });
        }

        var roles =
            await _userManager.GetRolesAsync(
                user);

        var tokenResult =
            await _jwtTokenService
                .GenerateTokenAsync(user);

        return Ok(
            new LoginResponse
            {
                Token =
                    tokenResult.Token,

                ExpiresAt =
                    tokenResult.ExpiresAt,

                FullName =
                    user.FullName,

                Email =
                    user.Email ??
                    string.Empty,

                Roles =
                    roles,

                MustChangePassword =
                    user.MustChangePassword
            });
    }


    // ============================================================
    // CHANGE PASSWORD
    //
    // Used when logged-in user knows current password.
    // ============================================================

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.CurrentPassword))
        {
            return BadRequest(new
            {
                message =
                    "Current password is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.NewPassword))
        {
            return BadRequest(new
            {
                message =
                    "New password is required."
            });
        }

        if (request.NewPassword !=
            request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message =
                    "New password and confirmation do not match."
            });
        }

        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager
                .FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var result =
            await _userManager
                .ChangePasswordAsync(
                    user,
                    request.CurrentPassword,
                    request.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "Unable to change password.",

                errors =
                    result.Errors
                        .Select(x =>
                            x.Description)
            });
        }

        user.MustChangePassword =
            false;

        await _userManager
            .UpdateAsync(user);

        return Ok(new
        {
            message =
                "Password changed successfully.",

            mustChangePassword =
                false
        });
    }


    // ============================================================
    // STAFF FORGOT PASSWORD - STEP 1
    // REQUEST OTP
    // ============================================================

    [HttpPost("staff-forgot-password/request-otp")]
    public async Task<IActionResult>
        RequestStaffForgotPasswordOtp(
            StaffForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.StaffNumber))
        {
            return BadRequest(new
            {
                message =
                    "Staff number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Email))
        {
            return BadRequest(new
            {
                message =
                    "Email is required."
            });
        }

        var staffNumber =
            request.StaffNumber.Trim();

        var email =
            request.Email.Trim();

        // --------------------------------------------------------
        // Find Active Staff
        // --------------------------------------------------------

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.StaffNumber ==
                        staffNumber &&
                    x.IsActive);

        if (staff == null)
        {
            return BadRequest(new
            {
                message =
                    "Unable to process password reset request."
            });
        }

        // --------------------------------------------------------
        // Staff must have linked Identity account
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(
            staff.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "This staff member does not have a registered login account."
            });
        }

        var user =
            await _userManager.FindByIdAsync(
                staff.ApplicationUserId);

        if (user == null ||
            !user.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Unable to process password reset request."
            });
        }

        // --------------------------------------------------------
        // Email must match registered email
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                user.Email) ||
            !string.Equals(
                user.Email.Trim(),
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "The email address does not match the registered staff account."
            });
        }

        // --------------------------------------------------------
        // 60-second OTP resend cooldown
        // --------------------------------------------------------

        var recentOtp =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StaffForgotPassword")
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (recentOtp != null)
        {
            var secondsPassed =
                (DateTime.UtcNow -
                    recentOtp.CreatedAt)
                .TotalSeconds;

            if (secondsPassed < 60)
            {
                var retryAfterSeconds =
                    Math.Max(
                        1,
                        60 - (int)secondsPassed);

                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    new
                    {
                        message =
                            "Please wait before requesting another OTP.",

                        retryAfterSeconds
                    });
            }
        }

        // --------------------------------------------------------
        // Remove previous Staff Forgot Password OTPs
        // --------------------------------------------------------

        var oldOtps =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StaffForgotPassword")
                .ToListAsync();

        if (oldOtps.Count > 0)
        {
            _context.OtpVerifications
                .RemoveRange(oldOtps);
        }

        // --------------------------------------------------------
        // Generate Secure OTP
        // --------------------------------------------------------

        var otp =
            RandomNumberGenerator
                .GetInt32(
                    100000,
                    1000000)
                .ToString();

        var otpVerification =
            new OtpVerification
            {
                Email =
                    email,

                Code =
                    otp,

                Purpose =
                    "StaffForgotPassword",

                CreatedAt =
                    DateTime.UtcNow,

                ExpiresAt =
                    DateTime.UtcNow
                        .AddMinutes(5),

                IsUsed =
                    false
            };

        _context.OtpVerifications
            .Add(otpVerification);

        await _context
            .SaveChangesAsync();

        // --------------------------------------------------------
        // Send OTP
        // --------------------------------------------------------

        try
        {
            await _emailService.SendOtpAsync(
                email,
                staff.FullName,
                otp);
        }
        catch
        {
            // Email was not delivered.
            // Remove the OTP so an undelivered OTP
            // cannot be used later.
            _context.OtpVerifications
                .Remove(otpVerification);

            await _context.SaveChangesAsync();

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message =
                        "Unable to send the OTP email at the moment. Please try again later."
                });
        }

        return Ok(new
        {
            message =
                "Password reset OTP sent successfully.",

            staffName =
                staff.FullName,

            expiresInMinutes =
                5,

            resendAfterSeconds =
                60
        });
    }


    // ============================================================
    // STAFF FORGOT PASSWORD - STEP 2
    // VERIFY OTP
    // ============================================================

    [HttpPost("staff-forgot-password/verify-otp")]
    public async Task<IActionResult>
        VerifyStaffForgotPasswordOtp(
            StaffForgotPasswordVerifyOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.StaffNumber))
        {
            return BadRequest(new
            {
                message =
                    "Staff number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Email))
        {
            return BadRequest(new
            {
                message =
                    "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Otp))
        {
            return BadRequest(new
            {
                message =
                    "OTP is required."
            });
        }

        var staffNumber =
            request.StaffNumber.Trim();

        var email =
            request.Email.Trim();

        var enteredOtp =
            request.Otp.Trim();

        // --------------------------------------------------------
        // Validate Staff
        // --------------------------------------------------------

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.StaffNumber ==
                        staffNumber &&
                    x.IsActive);

        if (staff == null ||
            string.IsNullOrWhiteSpace(
                staff.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Unable to verify OTP."
            });
        }

        // --------------------------------------------------------
        // Validate Identity Account
        // --------------------------------------------------------

        var user =
            await _userManager.FindByIdAsync(
                staff.ApplicationUserId);

        if (user == null ||
            !user.IsActive ||
            string.IsNullOrWhiteSpace(
                user.Email) ||
            !string.Equals(
                user.Email.Trim(),
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Unable to verify OTP."
            });
        }

        // --------------------------------------------------------
        // Get latest unused Staff OTP
        // --------------------------------------------------------

        var otpRecord =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StaffForgotPassword" &&
                    !x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (otpRecord == null)
        {
            return BadRequest(new
            {
                message =
                    "OTP not found. Please request a new OTP."
            });
        }

        if (otpRecord.ExpiresAt <
            DateTime.UtcNow)
        {
            return BadRequest(new
            {
                message =
                    "OTP has expired. Please request a new OTP."
            });
        }

        if (otpRecord.Code !=
            enteredOtp)
        {
            return BadRequest(new
            {
                message =
                    "Invalid OTP."
            });
        }

        // --------------------------------------------------------
        // Mark OTP verified
        // --------------------------------------------------------

        otpRecord.IsUsed =
            true;

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "OTP verified successfully. You can now reset your password."
        });
    }


    // ============================================================
    // STAFF FORGOT PASSWORD - STEP 3
    // RESET PASSWORD
    // ============================================================

    [HttpPost("staff-forgot-password/reset-password")]
    public async Task<IActionResult>
        ResetStaffPassword(
            StaffResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.StaffNumber))
        {
            return BadRequest(new
            {
                message =
                    "Staff number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Email))
        {
            return BadRequest(new
            {
                message =
                    "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.NewPassword))
        {
            return BadRequest(new
            {
                message =
                    "New password is required."
            });
        }

        if (request.NewPassword !=
            request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message =
                    "New password and confirmation do not match."
            });
        }

        var staffNumber =
            request.StaffNumber.Trim();

        var email =
            request.Email.Trim();

        // --------------------------------------------------------
        // Find Staff
        // --------------------------------------------------------

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.StaffNumber ==
                        staffNumber &&
                    x.IsActive);

        if (staff == null ||
            string.IsNullOrWhiteSpace(
                staff.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Unable to reset password."
            });
        }

        // --------------------------------------------------------
        // Find Identity account
        // --------------------------------------------------------

        var user =
            await _userManager.FindByIdAsync(
                staff.ApplicationUserId);

        if (user == null ||
            !user.IsActive ||
            string.IsNullOrWhiteSpace(
                user.Email) ||
            !string.Equals(
                user.Email.Trim(),
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Unable to reset password."
            });
        }

        // --------------------------------------------------------
        // A verified OTP must exist
        // --------------------------------------------------------

        var verifiedOtp =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StaffForgotPassword" &&
                    x.IsUsed &&
                    x.ExpiresAt >=
                        DateTime.UtcNow)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (verifiedOtp == null)
        {
            return BadRequest(new
            {
                message =
                    "OTP verification is required before resetting the password."
            });
        }

        // --------------------------------------------------------
        // Generate ASP.NET Identity reset token
        // --------------------------------------------------------

        var passwordResetToken =
            await _userManager
                .GeneratePasswordResetTokenAsync(
                    user);

        var resetResult =
            await _userManager
                .ResetPasswordAsync(
                    user,
                    passwordResetToken,
                    request.NewPassword);

        if (!resetResult.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "Unable to reset password.",

                errors =
                    resetResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }

        // Staff selected their own password.
        user.MustChangePassword =
            false;

        await _userManager
            .UpdateAsync(user);

        // --------------------------------------------------------
        // Remove OTP records after successful reset
        // --------------------------------------------------------

        var resetOtps =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StaffForgotPassword")
                .ToListAsync();

        if (resetOtps.Count > 0)
        {
            _context.OtpVerifications
                .RemoveRange(resetOtps);

            await _context
                .SaveChangesAsync();
        }

        await _userManager
            .ResetAccessFailedCountAsync(user);

        return Ok(new
        {
            message =
                "Password reset successfully. You can now login with your new password."
        });
    }
}