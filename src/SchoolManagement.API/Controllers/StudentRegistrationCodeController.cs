using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Authentication.DTOs;
using SchoolManagement.Application.Auditing;
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
    private readonly IAuditLogService _auditLogService;

    public StudentRegistrationCodeController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IEmailService emailService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _emailService = emailService;
        _auditLogService = auditLogService;
    }

    // ============================================================
    // GENERATE REGISTRATION CODE
    // ADMIN ONLY
    // ============================================================

    [HttpPost("generate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Generate(
        GenerateStudentRegistrationCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IndexNumber))
        {
            return BadRequest(new
            {
                message = "Index number is required."
            });
        }

        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var currentUser =
            await _userManager.FindByIdAsync(
                userId);

        if (currentUser == null)
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

        if (!string.IsNullOrWhiteSpace(
            student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "This student already has a registered account."
            });
        }

        if (string.IsNullOrWhiteSpace(
            student.Email))
        {
            return BadRequest(new
            {
                message =
                    "No registered email address is available for this student. Please update the student's email first."
            });
        }

        // Invalidate previous active unused codes
        var previousCodes =
            await _context.StudentRegistrationCodes
                .Where(x =>
                    x.StudentId == student.Id &&
                    x.IsActive &&
                    !x.IsUsed)
                .ToListAsync();

        foreach (var previousCode in previousCodes)
        {
            previousCode.IsActive = false;
        }

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

        await _auditLogService.LogAsync(
            action: "GenerateRegistrationCode",
            entityName: "StudentRegistrationCode",
            entityId: registration.Id.ToString(),
            description:
                $"A student registration code was generated for {student.IndexNumber} - {student.FullName}.",
            newValues: new
            {
                registration.StudentId,
                student.IndexNumber,
                registration.ExpiresAt,
                registration.MaxAttempts,
                registration.IsActive
            });

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
                    student.FullName,

                maskedEmail =
                    MaskEmail(
                        student.Email)
            },

            registrationCode,

            expiresAt,

            expiresInMinutes =
                30,

            maxAttempts =
                registration.MaxAttempts
        });
    }

    // ============================================================
    // VALIDATE REGISTRATION CODE
    // ============================================================

    [AllowAnonymous]
    [EnableRateLimiting("RegistrationPolicy")]
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(
        ValidateStudentRegistrationCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(
                request.IndexNumber) ||
            string.IsNullOrWhiteSpace(
                request.RegistrationCode))
        {
            return BadRequest(new
            {
                message =
                    "Index number and registration code are required."
            });
        }

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
            registration.IsActive = false;

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
            registration.IsActive = false;

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
                registration.IsActive = false;
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

                attemptsRemaining
            });
        }

        if (string.IsNullOrWhiteSpace(
            student.Email))
        {
            return BadRequest(new
            {
                message =
                    "No registered email address is available for this student. Please contact the school administration."
            });
        }

        var email =
            student.Email
                .Trim()
                .ToLowerInvariant();

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
                    student.FullName,

                maskedEmail =
                    MaskEmail(email)
            },

            registrationCodeValid =
                true,

            expiresAt =
                registration.ExpiresAt
        });
    }

    // ============================================================
    // REQUEST EMAIL OTP
    // ============================================================

    [AllowAnonymous]
    [EnableRateLimiting("OtpPolicy")]
    [HttpPost("request-email-otp")]
    public async Task<IActionResult> RequestEmailOtp(
        RequestStudentRegistrationEmailOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.IndexNumber))
        {
            return BadRequest(new
            {
                message =
                    "Index number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.RegistrationCode))
        {
            return BadRequest(new
            {
                message =
                    "Registration code is required."
            });
        }

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

        if (string.IsNullOrWhiteSpace(
            student.Email))
        {
            return BadRequest(new
            {
                message =
                    "No registered email address is available for this student. Please contact the school administration."
            });
        }

        var email =
            student.Email
                .Trim()
                .ToLowerInvariant();

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
            registration.IsActive = false;

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
            registration.IsActive = false;

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
                registration.IsActive = false;
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

        var existingUser =
            await _userManager.FindByEmailAsync(
                email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                message =
                    "The registered student email is already linked to another account. Please contact the school administration."
            });
        }

        var otpPurpose =
            $"StudentRegistrationCodeEmail:{student.Id}";

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

                return StatusCode(
                    429,
                    new
                    {
                        message =
                            "Please wait before requesting another OTP.",

                        retryAfterSeconds
                    });
            }
        }

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
                    DateTime.UtcNow
                        .AddMinutes(5),

                IsUsed =
                    false
            };

        _context.OtpVerifications.Add(
            otpVerification);

        await _context.SaveChangesAsync();

        try
        {
            await _emailService.SendOtpAsync(
                email,
                student.FullName,
                otp);
        }
        catch
        {
            _context.OtpVerifications.Remove(
                otpVerification);

            await _context.SaveChangesAsync();

            return StatusCode(
                503,
                new
                {
                    message =
                        "Unable to send the OTP email at the moment. Please try again later."
                });
        }

        return Ok(new
        {
            message =
                "Email verification OTP sent successfully.",

            studentName =
                student.FullName,

            maskedEmail =
                MaskEmail(email),

            expiresInMinutes =
                5,

            resendAfterSeconds =
                60
        });
    }

    // ============================================================
    // VERIFY EMAIL OTP
    // ============================================================

    [AllowAnonymous]
    [EnableRateLimiting("OtpPolicy")]
    [HttpPost("verify-email-otp")]
    public async Task<IActionResult> VerifyEmailOtp(
        VerifyStudentRegistrationEmailOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(
                request.IndexNumber) ||
            string.IsNullOrWhiteSpace(
                request.RegistrationCode) ||
            string.IsNullOrWhiteSpace(
                request.Otp))
        {
            return BadRequest(new
            {
                message =
                    "Index number, registration code and OTP are required."
            });
        }

        var indexNumber =
            request.IndexNumber.Trim();

        var registrationCode =
            request.RegistrationCode
                .Trim()
                .ToUpperInvariant();

        var otp =
            request.Otp.Trim();

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

        if (string.IsNullOrWhiteSpace(
            student.Email))
        {
            return BadRequest(new
            {
                message =
                    "Unable to verify email."
            });
        }

        var email =
            student.Email
                .Trim()
                .ToLowerInvariant();

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
                registration.IsActive = false;
            }

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Invalid registration code."
            });
        }

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

        otpRecord.IsUsed =
            true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Email verified successfully.",

            indexNumber =
                student.IndexNumber,

            maskedEmail =
                MaskEmail(email),

            emailVerified =
                true
        });
    }

    // ============================================================
    // COMPLETE STUDENT ACCOUNT REGISTRATION
    // ============================================================

    [AllowAnonymous]
    [EnableRateLimiting("RegistrationPolicy")]
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteRegistration(
        CompleteStudentRegistrationWithCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.IndexNumber))
        {
            return BadRequest(new
            {
                message =
                    "Index number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.RegistrationCode))
        {
            return BadRequest(new
            {
                message =
                    "Registration code is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Password))
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

        if (string.IsNullOrWhiteSpace(
            student.Email))
        {
            return BadRequest(new
            {
                message =
                    "No registered email address is available for this student."
            });
        }

        var email =
            student.Email
                .Trim()
                .ToLowerInvariant();

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
            registration.IsActive = false;

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
            registration.IsActive = false;

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
                registration.IsActive = false;
            }

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Invalid registration code."
            });
        }

        var otpPurpose =
            $"StudentRegistrationCodeEmail:{student.Id}";

        var verifiedOtp =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose == otpPurpose &&
                    x.IsUsed &&
                    x.ExpiresAt >= now)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (verifiedOtp == null)
        {
            return BadRequest(new
            {
                message =
                    "Email verification is required before creating the student account."
            });
        }

        var existingUser =
            await _userManager.FindByEmailAsync(
                email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                message =
                    "The registered student email is already linked to another account."
            });
        }

        var studentRole =
            await _roleManager.FindByNameAsync(
                "Student");

        if (studentRole == null ||
            string.IsNullOrWhiteSpace(
                studentRole.Name))
        {
            return StatusCode(
                500,
                new
                {
                    message =
                        "Student role is not configured."
                });
        }

        var applicationUser =
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
                applicationUser,
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

        var roleResult =
            await _userManager.AddToRoleAsync(
                applicationUser,
                studentRole.Name);

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(
                applicationUser);

            return StatusCode(
                500,
                new
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

        student.ApplicationUserId =
            applicationUser.Id;

        registration.IsUsed =
            true;

        registration.UsedAt =
            now;

        registration.IsActive =
            false;

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "CreateAccount",
            entityName: "Student",
            entityId: student.Id.ToString(),
            description:
                $"Student account {student.IndexNumber} - {student.FullName} was created.",
            newValues: new
            {
                student.IndexNumber,
                student.FullName,
                Email = email,
                Role = studentRole.Name,
                student.IsActive
            });

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

                maskedEmail =
                    MaskEmail(email)
            },

            role =
                studentRole.Name
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

    private static string MaskEmail(
        string email)
    {
        if (string.IsNullOrWhiteSpace(
            email))
        {
            return string.Empty;
        }

        var parts =
            email.Split('@');

        if (parts.Length != 2)
        {
            return email;
        }

        var localPart =
            parts[0];

        var domain =
            parts[1];

        if (localPart.Length <= 1)
        {
            return $"*@{domain}";
        }

        if (localPart.Length == 2)
        {
            return
                $"{localPart[0]}*@{domain}";
        }

        return
            $"{localPart[0]}"
            + $"{new string('*', localPart.Length - 2)}"
            + $"{localPart[^1]}@{domain}";
    }

    private static string HashCode(
        string value)
    {
        var bytes =
            Encoding.UTF8.GetBytes(
                value
                    .Trim()
                    .ToUpperInvariant());

        var hash =
            SHA256.HashData(
                bytes);

        return Convert.ToHexString(
            hash);
    }
}