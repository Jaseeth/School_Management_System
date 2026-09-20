using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Authentication.DTOs;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Cryptography;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-password-admin")]
[Authorize(Roles = "Admin")]
public class StudentPasswordAdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public StudentPasswordAdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ============================================================
    // ADMIN RESET STUDENT PASSWORD
    //
    // Used when student cannot use normal email OTP recovery.
    // Admin must verify student identity first.
    // ============================================================

    [HttpPost("reset")]
    public async Task<IActionResult> ResetStudentPassword(
        AdminResetStudentPasswordRequest request)
    {
        var indexNumber =
            request.IndexNumber.Trim();

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.IndexNumber == indexNumber &&
                    x.IsActive);

        if (student == null)
        {
            return NotFound(new
            {
                message =
                    "Active student not found."
            });
        }

        if (string.IsNullOrWhiteSpace(
            student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "This student does not have a registered login account."
            });
        }

        var user =
            await _userManager.FindByIdAsync(
                student.ApplicationUserId);

        if (user == null ||
            !user.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Student login account not found or inactive."
            });
        }


        // ========================================================
        // GENERATE TEMPORARY PASSWORD
        // ========================================================

        var temporaryPassword =
            GenerateTemporaryPassword();


        // ========================================================
        // RESET PASSWORD USING IDENTITY TOKEN
        // ========================================================

        var resetToken =
            await _userManager
                .GeneratePasswordResetTokenAsync(user);

        var resetResult =
            await _userManager.ResetPasswordAsync(
                user,
                resetToken,
                temporaryPassword);

        if (!resetResult.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "Unable to reset student password.",

                errors =
                    resetResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }


        // ========================================================
        // FORCE PASSWORD CHANGE ON NEXT LOGIN
        // ========================================================

        user.MustChangePassword =
            true;

        var updateResult =
            await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return StatusCode(500, new
            {
                message =
                    "Password was reset, but unable to enable forced password change."
            });
        }


        // ========================================================
        // RETURN TEMP PASSWORD ONLY ONCE
        // ========================================================

        return Ok(new
        {
            message =
                "Student password reset successfully.",

            student = new
            {
                id =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName
            },

            temporaryPassword =
                temporaryPassword,

            mustChangePassword =
                true
        });
    }


    private static string GenerateTemporaryPassword()
    {
        const string characters =
            "ABCDEFGHJKLMNPQRSTUVWXYZ" +
            "abcdefghijkmnopqrstuvwxyz" +
            "23456789";

        var randomPart =
            new char[8];

        for (var i = 0;
             i < randomPart.Length;
             i++)
        {
            var index =
                RandomNumberGenerator.GetInt32(
                    characters.Length);

            randomPart[i] =
                characters[index];
        }

        return $"Tmp@{new string(randomPart)}";
    }
}