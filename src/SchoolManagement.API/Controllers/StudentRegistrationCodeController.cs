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
using System.Text;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-registration-codes")]
[Authorize]
public class StudentRegistrationCodeController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IEmailService _emailService;

    public StudentRegistrationCodeController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _emailService = emailService;
    }

    // ============================================================
    // GENERATE STUDENT REGISTRATION CODE
    //
    // Admin only
    // Used when student does not have access to normal
    // email-based self-registration.
    // ============================================================

    [HttpPost("generate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Generate(
        GenerateStudentRegistrationCodeRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var adminStaff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (adminStaff == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active staff record."
            });
        }

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

        // ========================================================
        // STUDENT ALREADY HAS AN ACCOUNT
        // ========================================================

        if (!string.IsNullOrWhiteSpace(
            student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "This student already has a registered account."
            });
        }


        // ========================================================
        // INVALIDATE PREVIOUS ACTIVE UNUSED CODES
        // ========================================================

        var previousCodes =
            await _context.StudentRegistrationCodes
                .Where(x =>
                    x.StudentId == student.Id &&
                    x.IsActive &&
                    !x.IsUsed)
                .ToListAsync();

        foreach (var previousCode in previousCodes)
        {
            previousCode.IsActive =
                false;
        }


        // ========================================================
        // GENERATE NEW ONE-TIME CODE
        // ========================================================

        var registrationCode =
            GenerateRegistrationCode();

        var codeHash =
            HashCode(registrationCode);

        var now =
            DateTime.UtcNow;

        var expiresAt =
            now.AddMinutes(30);

        var registration =
            new StudentRegistrationCode
            {
                StudentId =
                    student.Id,

                CodeHash =
                    codeHash,

                ExpiresAt =
                    expiresAt,

                IsUsed =
                    false,

                UsedAt =
                    null,

                FailedAttempts =
                    0,

                MaxAttempts =
                    5,

                CreatedByStaffId =
                    adminStaff.Id,

                CreatedAt =
                    now,

                IsActive =
                    true
            };

        _context.StudentRegistrationCodes.Add(
            registration);

        await _context.SaveChangesAsync();


        // ========================================================
        // RETURN PLAIN CODE ONLY THIS ONE TIME
        // ========================================================

        return Ok(new
        {
            message =
                "Student registration code generated successfully.",

            student = new
            {
                id =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName
            },

            registrationCode =
                registrationCode,

            expiresAt =
                expiresAt,

            expiresInMinutes =
                30,

            maxAttempts =
                registration.MaxAttempts
        });
    }

    [AllowAnonymous]
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(
    ValidateStudentRegistrationCodeRequest request)
    {
        var indexNumber =
            request.IndexNumber.Trim();

        var registrationCode =
            request.RegistrationCode
                .Trim()
                .ToUpperInvariant();

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.IndexNumber == indexNumber &&
                    x.IsActive);

        if (student == null)
        {
            return BadRequest(new
            {
                message =
                    "Invalid index number or registration code."
            });
        }

        if (!string.IsNullOrWhiteSpace(
            student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "This student already has a registered account."
            });
        }

        var registration =
            await _context.StudentRegistrationCodes
                .Where(x =>
                    x.StudentId == student.Id &&
                    x.IsActive &&
                    !x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (registration == null)
        {
            return BadRequest(new
            {
                message =
                    "Invalid index number or registration code."
            });
        }

        var now =
            DateTime.UtcNow;

        if (registration.ExpiresAt <= now)
        {
            registration.IsActive =
                false;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Registration code has expired. Please contact Admin for a new code."
            });
        }

        if (registration.FailedAttempts >=
            registration.MaxAttempts)
        {
            registration.IsActive =
                false;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Registration code has been locked because of too many failed attempts. Please contact Admin."
            });
        }

        var enteredCodeHash =
            HashCode(registrationCode);

        if (!string.Equals(
            enteredCodeHash,
            registration.CodeHash,
            StringComparison.Ordinal))
        {
            registration.FailedAttempts++;

            if (registration.FailedAttempts >=
                registration.MaxAttempts)
            {
                registration.IsActive =
                    false;
            }

            await _context.SaveChangesAsync();

            var attemptsRemaining =
                Math.Max(
                    registration.MaxAttempts -
                    registration.FailedAttempts,
                    0);

            return BadRequest(new
            {
                message =
                    attemptsRemaining == 0
                        ? "Registration code has been locked because of too many failed attempts. Please contact Admin."
                        : "Invalid index number or registration code.",

                attemptsRemaining =
                    attemptsRemaining
            });
        }

        return Ok(new
        {
            message =
                "Registration code validated successfully.",

            student = new
            {
                id =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName
            },

            registrationCodeValid =
                true,

            expiresAt =
                registration.ExpiresAt
        });
    }


    [AllowAnonymous]
    [HttpPost("request-email-otp")]
    public async Task<IActionResult> RequestEmailOtp(
    RequestStudentRegistrationEmailOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IndexNumber))
        {
            return BadRequest(new
            {
                message = "Index number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.RegistrationCode))
        {
            return BadRequest(new
            {
                message = "Registration code is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        var indexNumber =
            request.IndexNumber.Trim();

        var registrationCode =
            request.RegistrationCode
                .Trim()
                .ToUpperInvariant();

        var email =
            request.Email
                .Trim()
                .ToLowerInvariant();


        // ========================================================
        // FIND STUDENT
        // ========================================================

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.IndexNumber == indexNumber &&
                    x.IsActive);

        if (student == null)
        {
            return BadRequest(new
            {
                message =
                    "Invalid index number or registration code."
            });
        }

        if (!string.IsNullOrWhiteSpace(
            student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "This student already has a registered account."
            });
        }


        // ========================================================
        // VALIDATE REGISTRATION CODE
        // ========================================================

        var registration =
            await _context.StudentRegistrationCodes
                .Where(x =>
                    x.StudentId == student.Id &&
                    x.IsActive &&
                    !x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (registration == null)
        {
            return BadRequest(new
            {
                message =
                    "Invalid index number or registration code."
            });
        }

        var now =
            DateTime.UtcNow;

        if (registration.ExpiresAt <= now)
        {
            registration.IsActive =
                false;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Registration code has expired. Please contact Admin for a new code."
            });
        }

        if (registration.FailedAttempts >=
            registration.MaxAttempts)
        {
            registration.IsActive =
                false;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Registration code has been locked. Please contact Admin."
            });
        }

        var enteredCodeHash =
            HashCode(registrationCode);

        if (!string.Equals(
            enteredCodeHash,
            registration.CodeHash,
            StringComparison.Ordinal))
        {
            registration.FailedAttempts++;

            if (registration.FailedAttempts >=
                registration.MaxAttempts)
            {
                registration.IsActive =
                    false;
            }

            await _context.SaveChangesAsync();

            var attemptsRemaining =
                Math.Max(
                    registration.MaxAttempts -
                    registration.FailedAttempts,
                    0);

            return BadRequest(new
            {
                message =
                    attemptsRemaining == 0
                        ? "Registration code has been locked. Please contact Admin."
                        : "Invalid index number or registration code.",

                attemptsRemaining
            });
        }


        // ========================================================
        // EMAIL MUST NOT ALREADY BELONG TO AN ACCOUNT
        // ========================================================

        var existingUser =
            await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                message =
                    "This email address is already registered."
            });
        }


        // ========================================================
        // UNIQUE OTP PURPOSE FOR THIS STUDENT
        // ========================================================

        var otpPurpose =
            $"StudentRegistrationCodeEmail:{student.Id}";


        // ========================================================
        // 60 SECOND RESEND COOLDOWN
        // ========================================================

        var recentOtp =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose == otpPurpose)
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

                return StatusCode(429, new
                {
                    message =
                        "Please wait before requesting another OTP.",

                    retryAfterSeconds
                });
            }
        }


        // ========================================================
        // REMOVE PREVIOUS UNUSED OTP
        // ========================================================

        var oldOtps =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose == otpPurpose &&
                    !x.IsUsed)
                .ToListAsync();

        if (oldOtps.Count > 0)
        {
            _context.OtpVerifications
                .RemoveRange(oldOtps);
        }


        // ========================================================
        // GENERATE OTP
        // ========================================================

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
                    otpPurpose,

                CreatedAt =
                    DateTime.UtcNow,

                ExpiresAt =
                    DateTime.UtcNow.AddMinutes(5),

                IsUsed =
                    false
            };

        _context.OtpVerifications.Add(
            otpVerification);

        await _context.SaveChangesAsync();


        // ========================================================
        // SEND EMAIL
        // ========================================================

        try
        {
            await _emailService.SendOtpAsync(
            email,
            student.FullName,
            otp);
        }
        catch
        {

        }


        return Ok(new
        {
            message =
                "Email verification OTP sent successfully.",

            studentName =
                student.FullName,

            email =
                email,

            expiresInMinutes =
                5
        });
    }

    [AllowAnonymous]
    [HttpPost("verify-email-otp")]
    public async Task<IActionResult> VerifyEmailOtp(
    VerifyStudentRegistrationEmailOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IndexNumber) ||
            string.IsNullOrWhiteSpace(request.RegistrationCode) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(new
            {
                message =
                    "Index number, registration code, email and OTP are required."
            });
        }

        var indexNumber =
            request.IndexNumber.Trim();

        var registrationCode =
            request.RegistrationCode
                .Trim()
                .ToUpperInvariant();

        var email =
            request.Email
                .Trim()
                .ToLowerInvariant();

        var otp =
            request.Otp.Trim();


        // ========================================================
        // FIND STUDENT
        // ========================================================

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.IndexNumber == indexNumber &&
                    x.IsActive);

        if (student == null ||
            !string.IsNullOrWhiteSpace(
                student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Unable to verify email."
            });
        }


        // ========================================================
        // REGISTRATION CODE MUST STILL BE VALID
        // ========================================================

        var registration =
            await _context.StudentRegistrationCodes
                .Where(x =>
                    x.StudentId == student.Id &&
                    x.IsActive &&
                    !x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (registration == null ||
            registration.ExpiresAt <= DateTime.UtcNow ||
            registration.FailedAttempts >=
                registration.MaxAttempts)
        {
            return BadRequest(new
            {
                message =
                    "Registration code is no longer valid."
            });
        }

        var enteredCodeHash =
            HashCode(registrationCode);

        if (!string.Equals(
            enteredCodeHash,
            registration.CodeHash,
            StringComparison.Ordinal))
        {
            registration.FailedAttempts++;

            if (registration.FailedAttempts >=
                registration.MaxAttempts)
            {
                registration.IsActive =
                    false;
            }

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Invalid registration code."
            });
        }


        // ========================================================
        // VERIFY OTP
        // ========================================================

        var otpPurpose =
            $"StudentRegistrationCodeEmail:{student.Id}";

        var otpRecord =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose == otpPurpose &&
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

        if (otpRecord.Code != otp)
        {
            return BadRequest(new
            {
                message =
                    "Invalid OTP."
            });
        }


        // ========================================================
        // EMAIL VERIFIED
        // ========================================================

        otpRecord.IsUsed =
            true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Email verified successfully.",

            indexNumber =
                student.IndexNumber,

            email =
                email,

            emailVerified =
                true
        });
    }


    // ============================================================
    // HELPERS
    // ============================================================

    private static string GenerateRegistrationCode()
    {
        const string characters =
            "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        const int length =
            6;

        var result =
            new char[length];

        for (var i = 0; i < length; i++)
        {
            var index =
                RandomNumberGenerator.GetInt32(
                    characters.Length);

            result[i] =
                characters[index];
        }

        return new string(result);
    }

    [AllowAnonymous]
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteRegistration(
    CompleteStudentRegistrationWithCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IndexNumber))
        {
            return BadRequest(new
            {
                message =
                    "Index number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.RegistrationCode))
        {
            return BadRequest(new
            {
                message =
                    "Registration code is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message =
                    "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message =
                    "Password is required."
            });
        }

        if (request.Password !=
            request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message =
                    "Passwords do not match."
            });
        }

        var indexNumber =
            request.IndexNumber.Trim();

        var registrationCode =
            request.RegistrationCode
                .Trim()
                .ToUpperInvariant();

        var email =
            request.Email
                .Trim()
                .ToLowerInvariant();


        // ========================================================
        // FIND STUDENT
        // ========================================================

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.IndexNumber == indexNumber &&
                    x.IsActive);

        if (student == null)
        {
            return BadRequest(new
            {
                message =
                    "Unable to complete registration."
            });
        }

        if (!string.IsNullOrWhiteSpace(
            student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "This student already has a registered account."
            });
        }


        // ========================================================
        // VALIDATE REGISTRATION CODE
        // ========================================================

        var registration =
            await _context.StudentRegistrationCodes
                .Where(x =>
                    x.StudentId == student.Id &&
                    x.IsActive &&
                    !x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (registration == null)
        {
            return BadRequest(new
            {
                message =
                    "Registration code is not valid."
            });
        }

        var now =
            DateTime.UtcNow;

        if (registration.ExpiresAt <= now)
        {
            registration.IsActive =
                false;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Registration code has expired."
            });
        }

        if (registration.FailedAttempts >=
            registration.MaxAttempts)
        {
            registration.IsActive =
                false;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Registration code has been locked."
            });
        }

        var enteredCodeHash =
            HashCode(registrationCode);

        if (!string.Equals(
            enteredCodeHash,
            registration.CodeHash,
            StringComparison.Ordinal))
        {
            registration.FailedAttempts++;

            if (registration.FailedAttempts >=
                registration.MaxAttempts)
            {
                registration.IsActive =
                    false;
            }

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Invalid registration code."
            });
        }


        // ========================================================
        // EMAIL MUST HAVE BEEN VERIFIED
        // ========================================================

        var otpPurpose =
            $"StudentRegistrationCodeEmail:{student.Id}";

        var verifiedOtp =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose == otpPurpose &&
                    x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (verifiedOtp == null)
        {
            return BadRequest(new
            {
                message =
                    "Email has not been verified."
            });
        }


        // ========================================================
        // EMAIL MUST NOT ALREADY BE USED
        // ========================================================

        var existingUser =
            await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                message =
                    "This email address is already registered."
            });
        }


        // ========================================================
        // STUDENT ROLE MUST EXIST
        // ========================================================

        var studentRole =
            await _roleManager.FindByNameAsync(
                "Student");

        if (studentRole == null)
        {
            return StatusCode(500, new
            {
                message =
                    "Student role is not configured."
            });
        }


        // ========================================================
        // CREATE IDENTITY ACCOUNT
        // ========================================================

        var user =
            new ApplicationUser
            {
                UserName =
                    student.IndexNumber,

                Email =
                    email,

                FullName =
                    student.FullName,

                EmailConfirmed =
                    true,

                IsActive =
                    true,

                MustChangePassword =
                    false
            };

        var createResult =
            await _userManager.CreateAsync(
                user,
                request.Password);

        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "Unable to create student account.",

                errors =
                    createResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }


        // ========================================================
        // ASSIGN STUDENT ROLE
        // ========================================================

        var roleResult =
            await _userManager.AddToRoleAsync(
                user,
                "Student");

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            return StatusCode(500, new
            {
                message =
                    "Unable to assign Student role.",

                errors =
                    roleResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }


        // ========================================================
        // LINK ACCOUNT TO STUDENT
        // ========================================================

        student.ApplicationUserId =
            user.Id;


        // ========================================================
        // MARK REGISTRATION CODE AS USED
        // ========================================================

        registration.IsUsed =
            true;

        registration.UsedAt =
            now;

        registration.IsActive =
            false;


        await _context.SaveChangesAsync();


        return Ok(new
        {
            message =
                "Student account created successfully.",

            student = new
            {
                id =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName,

                email =
                    email
            },

            role =
                "Student"
        });
    }


    private static string HashCode(
        string value)
    {
        var bytes =
            Encoding.UTF8.GetBytes(
                value.Trim().ToUpperInvariant());

        var hash =
            SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }
}