using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Authentication.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/staff-password-reset-codes")]
[Authorize]
public class StaffPasswordResetCodeController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public StaffPasswordResetCodeController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }


    // ============================================================
    // ADMIN GENERATES RESET CODE
    // ============================================================

    [Authorize(Roles = "Admin")]
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        GenerateStaffPasswordResetCodeRequest request)
    {
        var staffNumber =
            request.StaffNumber.Trim();

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.StaffNumber == staffNumber &&
                    x.IsActive);

        if (staff == null)
        {
            return NotFound(new
            {
                message =
                    "Active staff member not found."
            });
        }


        if (string.IsNullOrWhiteSpace(
            staff.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Staff member does not have a login account."
            });
        }


        var staffUser =
            await _userManager.FindByIdAsync(
                staff.ApplicationUserId);

        if (staffUser == null ||
            !staffUser.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Staff login account not found or inactive."
            });
        }


        // ========================================================
        // GET ADMIN USER ID
        // ========================================================

        var adminUserId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(
            adminUserId))
        {
            return Unauthorized();
        }


        // ========================================================
        // INVALIDATE PREVIOUS UNUSED CODES
        // ========================================================

        var oldCodes =
            await _context
                .StaffPasswordResetCodes
                .Where(x =>
                    x.StaffId == staff.Id &&
                    x.IsActive &&
                    !x.IsUsed)
                .ToListAsync();

        foreach (var oldCode in oldCodes)
        {
            oldCode.IsActive = false;
        }


        // ========================================================
        // GENERATE ONE-TIME RESET CODE
        // ========================================================

        var plainCode =
            GenerateCode();

        var codeHash =
            HashCode(plainCode);


        var resetCode =
            new StaffPasswordResetCode
            {
                StaffId =
                    staff.Id,

                CodeHash =
                    codeHash,

                ExpiresAt =
                    DateTime.UtcNow
                        .AddMinutes(30),

                IsUsed =
                    false,

                FailedAttempts =
                    0,

                MaxAttempts =
                    5,

                CreatedByApplicationUserId =
                    adminUserId,

                CreatedAt =
                    DateTime.UtcNow,

                IsActive =
                    true
            };


        _context.StaffPasswordResetCodes.Add(
            resetCode);

        await _context.SaveChangesAsync();


        // ========================================================
        // RETURN CODE ONLY ONCE
        // ========================================================

        return Ok(new
        {
            message =
                "Staff password reset code generated successfully.",

            staff = new
            {
                id =
                    staff.Id,

                staffNumber =
                    staff.StaffNumber,

                fullName =
                    staff.FullName,

                email =
                    staffUser.Email
            },

            resetCode =
                plainCode,

            expiresAt =
                resetCode.ExpiresAt,

            expiresInMinutes =
                30,

            maxAttempts =
                resetCode.MaxAttempts
        });
    }

    [AllowAnonymous]
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(
    ValidateStaffPasswordResetCodeRequest request)
    {
        var staffNumber =
            request.StaffNumber.Trim();

        var resetCode =
            request.ResetCode
                .Trim()
                .ToUpperInvariant();


        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.StaffNumber == staffNumber &&
                    x.IsActive);

        if (staff == null)
        {
            return NotFound(new
            {
                message =
                    "Active staff member not found."
            });
        }


        if (string.IsNullOrWhiteSpace(
            staff.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Staff member does not have a login account."
            });
        }


        var codeRecord =
            await _context
                .StaffPasswordResetCodes
                .Where(x =>
                    x.StaffId == staff.Id &&
                    x.IsActive &&
                    !x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();


        if (codeRecord == null)
        {
            return BadRequest(new
            {
                message =
                    "Active password reset code not found."
            });
        }


        // ========================================================
        // EXPIRY CHECK
        // ========================================================

        if (codeRecord.ExpiresAt <
            DateTime.UtcNow)
        {
            codeRecord.IsActive = false;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Password reset code has expired."
            });
        }


        // ========================================================
        // ATTEMPT LIMIT CHECK
        // ========================================================

        if (codeRecord.FailedAttempts >=
            codeRecord.MaxAttempts)
        {
            codeRecord.IsActive = false;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Password reset code is locked because the maximum number of attempts was reached."
            });
        }


        // ========================================================
        // CHECK CODE
        // ========================================================

        var providedHash =
            HashCode(resetCode);


        if (providedHash !=
            codeRecord.CodeHash)
        {
            codeRecord.FailedAttempts++;

            if (codeRecord.FailedAttempts >=
                codeRecord.MaxAttempts)
            {
                codeRecord.IsActive = false;
            }

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Invalid password reset code.",

                attemptsRemaining =
                    Math.Max(
                        0,
                        codeRecord.MaxAttempts -
                        codeRecord.FailedAttempts)
            });
        }


        return Ok(new
        {
            message =
                "Password reset code is valid.",

            staff = new
            {
                id =
                    staff.Id,

                staffNumber =
                    staff.StaffNumber,

                fullName =
                    staff.FullName
            },

            resetCodeValid =
                true,

            expiresAt =
                codeRecord.ExpiresAt
        });
    }

    [AllowAnonymous]
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteReset(
    CompleteStaffPasswordResetRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message =
                    "New password and confirm password do not match."
            });
        }


        var staffNumber =
            request.StaffNumber.Trim();

        var resetCode =
            request.ResetCode
                .Trim()
                .ToUpperInvariant();


        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.StaffNumber == staffNumber &&
                    x.IsActive);

        if (staff == null)
        {
            return NotFound(new
            {
                message =
                    "Active staff member not found."
            });
        }


        if (string.IsNullOrWhiteSpace(
            staff.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Staff member does not have a login account."
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
                    "Staff login account not found or inactive."
            });
        }


        var codeRecord =
            await _context
                .StaffPasswordResetCodes
                .Where(x =>
                    x.StaffId == staff.Id &&
                    x.IsActive &&
                    !x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();


        if (codeRecord == null)
        {
            return BadRequest(new
            {
                message =
                    "Active password reset code not found."
            });
        }


        // ========================================================
        // EXPIRY CHECK
        // ========================================================

        if (codeRecord.ExpiresAt <
            DateTime.UtcNow)
        {
            codeRecord.IsActive = false;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Password reset code has expired."
            });
        }


        // ========================================================
        // ATTEMPT LIMIT
        // ========================================================

        if (codeRecord.FailedAttempts >=
            codeRecord.MaxAttempts)
        {
            codeRecord.IsActive = false;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Password reset code is locked."
            });
        }


        // ========================================================
        // VERIFY RESET CODE AGAIN
        // ========================================================

        var providedHash =
            HashCode(resetCode);


        if (providedHash !=
            codeRecord.CodeHash)
        {
            codeRecord.FailedAttempts++;

            if (codeRecord.FailedAttempts >=
                codeRecord.MaxAttempts)
            {
                codeRecord.IsActive = false;
            }

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Invalid password reset code.",

                attemptsRemaining =
                    Math.Max(
                        0,
                        codeRecord.MaxAttempts -
                        codeRecord.FailedAttempts)
            });
        }


        // ========================================================
        // RESET PASSWORD
        // ========================================================

        var identityResetToken =
            await _userManager
                .GeneratePasswordResetTokenAsync(user);


        var resetResult =
            await _userManager.ResetPasswordAsync(
                user,
                identityResetToken,
                request.NewPassword);


        if (!resetResult.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "Unable to reset staff password.",

                errors =
                    resetResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }


        // ========================================================
        // RESET CODE CAN NEVER BE USED AGAIN
        // ========================================================

        codeRecord.IsUsed = true;
        codeRecord.UsedAt = DateTime.UtcNow;
        codeRecord.IsActive = false;


        // Staff chose their own final password,
        // so no forced password change is required.
        user.MustChangePassword = false;


        var userUpdateResult =
            await _userManager.UpdateAsync(user);

        if (!userUpdateResult.Succeeded)
        {
            return StatusCode(500, new
            {
                message =
                    "Password was reset but unable to update account status."
            });
        }


        await _context.SaveChangesAsync();


        return Ok(new
        {
            message =
                "Staff password reset successfully.",

            staff = new
            {
                id =
                    staff.Id,

                staffNumber =
                    staff.StaffNumber,

                fullName =
                    staff.FullName
            },

            mustChangePassword =
                false
        });
    }


    // ============================================================
    // HELPERS
    // ============================================================

    private static string GenerateCode()
    {
        const string characters =
            "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        var code =
            new char[6];

        for (var i = 0;
             i < code.Length;
             i++)
        {
            var index =
                RandomNumberGenerator
                    .GetInt32(
                        characters.Length);

            code[i] =
                characters[index];
        }

        return new string(code);
    }


    private static string HashCode(
        string code)
    {
        var normalizedCode =
            code.Trim()
                .ToUpperInvariant();

        var bytes =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    normalizedCode));

        return Convert.ToHexString(bytes);
    }
}