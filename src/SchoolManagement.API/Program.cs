using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuestPDF.Infrastructure;
using SchoolManagement.API.Middleware;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Infrastructure;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Email;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.IO.Compression;
using System.Text;
using System.Threading.RateLimiting;

var builder =
    WebApplication.CreateBuilder(args);

// =====================================
// Production Configuration Validation
// =====================================

if (!builder.Environment.IsDevelopment())
{
    var connectionString =
        builder.Configuration
            .GetConnectionString(
                "DefaultConnection");

    if (string.IsNullOrWhiteSpace(
        connectionString))
    {
        throw new InvalidOperationException(
            "Production database connection string is missing.");
    }

    var productionJwtKey =
        builder.Configuration[
            "Jwt:Key"];

    if (string.IsNullOrWhiteSpace(
        productionJwtKey))
    {
        throw new InvalidOperationException(
            "Production JWT key is missing.");
    }

    if (productionJwtKey.Length < 32)
    {
        throw new InvalidOperationException(
            "Production JWT key must be at least 32 characters long.");
    }

    var productionJwtIssuer =
        builder.Configuration[
            "Jwt:Issuer"];

    if (string.IsNullOrWhiteSpace(
        productionJwtIssuer))
    {
        throw new InvalidOperationException(
            "JWT issuer is missing.");
    }

    var productionJwtAudience =
        builder.Configuration[
            "Jwt:Audience"];

    if (string.IsNullOrWhiteSpace(
        productionJwtAudience))
    {
        throw new InvalidOperationException(
            "JWT audience is missing.");
    }

    var resendApiKey =
        builder.Configuration[
            "Resend:ApiKey"];

    if (string.IsNullOrWhiteSpace(
        resendApiKey))
    {
        throw new InvalidOperationException(
            "Resend API key is missing.");
    }

    var firebaseProjectId =
        builder.Configuration[
            "Firebase:ProjectId"];

    if (string.IsNullOrWhiteSpace(
        firebaseProjectId))
    {
        throw new InvalidOperationException(
            "Firebase ProjectId is missing.");
    }

    var firebaseCredentialsPath =
        builder.Configuration[
            "Firebase:CredentialsPath"];

    if (string.IsNullOrWhiteSpace(
        firebaseCredentialsPath))
    {
        throw new InvalidOperationException(
            "Firebase credentials path is missing.");
    }
}

// =====================================
// Database
// =====================================

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
        options.UseSqlServer(
            builder.Configuration
                .GetConnectionString(
                    "DefaultConnection"),
            sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay:
                        TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            }));

// =====================================
// ASP.NET Core Identity
// =====================================

builder.Services
    .AddIdentity<
        ApplicationUser,
        IdentityRole>()
    .AddEntityFrameworkStores<
        ApplicationDbContext>()
    .AddDefaultTokenProviders();


builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;

    options.Cookie.SecurePolicy =
        CookieSecurePolicy.Always;

    options.Cookie.SameSite =
        SameSiteMode.Strict;

    options.SlidingExpiration =
        true;

    options.ExpireTimeSpan =
        TimeSpan.FromMinutes(60);
});


builder.Services.Configure<IdentityOptions>(options =>
{
    options.Lockout.DefaultLockoutTimeSpan =
        TimeSpan.FromMinutes(15);

    options.Lockout.MaxFailedAccessAttempts =
        5;

    options.Lockout.AllowedForNewUsers =
        true;
});



// =====================================
// JWT Authentication
// =====================================

var jwtKey =
    builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT Key is missing.");

if (jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "JWT Key must be at least 32 characters long.");
}

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT Issuer is missing.");

var jwtAudience =
    builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT Audience is missing.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata =
            !builder.Environment.IsDevelopment();

        options.SaveToken =
            false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer =
                    true,

                ValidateAudience =
                    true,

                ValidateLifetime =
                    true,

                ValidateIssuerSigningKey =
                    true,

                ValidIssuer =
                    jwtIssuer,

                ValidAudience =
                    jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtKey)),

                ClockSkew =
                    TimeSpan.Zero
            };
    });

// =====================================
// Controllers
// =====================================

builder.Services
    .AddControllers();

// =====================================
// CORS
// =====================================

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "FrontendPolicy",
        policy =>
        {
            if (builder.Environment
                .IsDevelopment())
            {
                policy
                    .WithOrigins(
                        "http://localhost:3000",
                        "http://localhost:5173",
                        "https://localhost:3000",
                        "https://localhost:5173")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
            else
            {
                policy
                    .WithOrigins(
                        "https://your-school-domain.com")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        });
});

// =====================================
// Swagger
// =====================================

builder.Services
    .AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(
    options =>
    {
        options.AddSecurityDefinition(
            "bearer",
            new OpenApiSecurityScheme
            {
                Type =
                    SecuritySchemeType.Http,

                Scheme =
                    "bearer",

                BearerFormat =
                    "JWT",

                Description =
                    "Enter your JWT token"
            });

        options.AddSecurityRequirement(
            document =>
                new OpenApiSecurityRequirement
                {
                    [
                        new OpenApiSecuritySchemeReference(
                            "bearer",
                            document)
                    ] = []
                });
    });

// =====================================
// Custom Services
// =====================================

builder.Services
    .AddScoped<JwtTokenService>();

builder.Services.Configure<
    ResendSettings>(
        builder.Configuration
            .GetSection(
                "Resend"));

builder.Services
    .AddHttpClient<
        IEmailService,
        ResendEmailService>();

builder.Services
    .AddSingleton<
        IAuthorizationPolicyProvider,
        PermissionPolicyProvider>();

builder.Services
    .AddScoped<
        IAuthorizationHandler,
        PermissionAuthorizationHandler>();

builder.Services
    .AddScoped<
        DevelopmentDataSeeder>();

builder.Services
    .AddNotificationInfrastructure(
        builder.Configuration);

// =====================================
// Rate Limiting
// =====================================

builder.Services.AddRateLimiter(
    options =>
    {
        options.RejectionStatusCode =
            StatusCodes
                .Status429TooManyRequests;

        // ---------------------------------
        // Authentication
        // ---------------------------------

        options.AddPolicy(
            "AuthPolicy",
            httpContext =>
                RateLimitPartition
                    .GetFixedWindowLimiter(
                        partitionKey:
                            httpContext
                                .Connection
                                .RemoteIpAddress?
                                .ToString()
                            ?? "unknown",

                        factory:
                            _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit =
                                        10,

                                    Window =
                                        TimeSpan
                                            .FromMinutes(
                                                1),

                                    QueueLimit =
                                        0,

                                    AutoReplenishment =
                                        true
                                }));

        // ---------------------------------
        // OTP
        // ---------------------------------

        options.AddPolicy(
            "OtpPolicy",
            httpContext =>
                RateLimitPartition
                    .GetFixedWindowLimiter(
                        partitionKey:
                            httpContext
                                .Connection
                                .RemoteIpAddress?
                                .ToString()
                            ?? "unknown",

                        factory:
                            _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit =
                                        5,

                                    Window =
                                        TimeSpan
                                            .FromMinutes(
                                                1),

                                    QueueLimit =
                                        0,

                                    AutoReplenishment =
                                        true
                                }));

        // ---------------------------------
        // Registration
        // ---------------------------------

        options.AddPolicy(
            "RegistrationPolicy",
            httpContext =>
                RateLimitPartition
                    .GetFixedWindowLimiter(
                        partitionKey:
                            httpContext
                                .Connection
                                .RemoteIpAddress?
                                .ToString()
                            ?? "unknown",

                        factory:
                            _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit =
                                        5,

                                    Window =
                                        TimeSpan
                                            .FromMinutes(
                                                1),

                                    QueueLimit =
                                        0,

                                    AutoReplenishment =
                                        true
                                }));
    });

// =====================================
// Request / File Upload Limits
// =====================================

const long maxRequestSize =
    10 * 1024 * 1024; // 10 MB

builder.WebHost.ConfigureKestrel(
    options =>
    {
        options.Limits
            .MaxRequestBodySize =
            maxRequestSize;
    });

builder.Services.Configure<FormOptions>(
    options =>
    {
        options.MultipartBodyLengthLimit =
            maxRequestSize;

        options.ValueLengthLimit =
            1024 * 1024; // 1 MB

        options.MultipartHeadersLengthLimit =
            16 * 1024; // 16 KB
    });

// =====================================
// Health Checks
// =====================================

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(name: "database");


builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});


builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});


// =====================================
// Response Compression
// =====================================

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;

    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(
    options =>
    {
        options.Level =
            CompressionLevel.Fastest;
    });

builder.Services.Configure<GzipCompressionProviderOptions>(
    options =>
    {
        options.Level =
            CompressionLevel.Fastest;
    });


// =====================================
// Request Timeouts
// =====================================

builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy =
        new RequestTimeoutPolicy
        {
            Timeout =
                TimeSpan.FromSeconds(30)
        };
});

// =====================================
// Build App
// =====================================

var app =
    builder.Build();


app.UseForwardedHeaders();


// =====================================
// Swagger UI
// =====================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope =
        app.Services.CreateScope();

    var seeder =
        scope.ServiceProvider
            .GetRequiredService<
                DevelopmentDataSeeder>();

    await seeder.SeedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// =====================================
// Global Exception Handling
// =====================================

app.UseMiddleware<
    GlobalExceptionMiddleware>();

// =====================================
// HTTPS
// =====================================

app.UseHttpsRedirection();

app.UseResponseCompression();

app.UseRequestTimeouts();

// =====================================
// Security Headers
// =====================================

app.Use(async (context, next) =>
{
    context.Response.Headers[
        "X-Content-Type-Options"] =
        "nosniff";

    context.Response.Headers[
        "X-Frame-Options"] =
        "DENY";

    context.Response.Headers[
        "Referrer-Policy"] =
        "strict-origin-when-cross-origin";

    context.Response.Headers[
        "Permissions-Policy"] =
        "camera=(), microphone=(), geolocation=()";

    context.Response.Headers[
        "Cross-Origin-Opener-Policy"] =
        "same-origin";

    await next();
});

// =====================================
// Static Files
// =====================================

app.UseStaticFiles();

// =====================================
// CORS
// =====================================

app.UseCors(
    "FrontendPolicy");

// =====================================
// Rate Limiting
// =====================================

app.UseRateLimiter();

// =====================================
// Authentication
// =====================================

app.UseAuthentication();

// =====================================
// Force Password Change
// =====================================

app.UseMiddleware<
    ForcePasswordChangeMiddleware>();

// =====================================
// Authorization
// =====================================

app.UseAuthorization();

// =====================================
// Controllers
// =====================================

app.MapControllers();

// =====================================
// Health Check Endpoint
// =====================================

app.MapHealthChecks(
    "/health");

// =====================================
// QuestPDF
// =====================================

QuestPDF.Settings.License =
    LicenseType.Community;

// =====================================
// Run
// =====================================

app.Run();