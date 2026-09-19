using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Text;
using Microsoft.OpenApi;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Infrastructure.Email;
using Microsoft.AspNetCore.Authorization;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.API.Middleware;
using SchoolManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// =====================================
// Database
// =====================================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// =====================================
// ASP.NET Core Identity
// =====================================

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// =====================================
// JWT Authentication
// =====================================

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key is missing.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

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
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ClockSkew = TimeSpan.Zero
            };
    });

// =====================================
// Controllers
// =====================================

builder.Services.AddControllers();

// =====================================
// Swagger
// =====================================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "bearer",
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT token"
        });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                "bearer",
                document)] = []
        });
});

// =====================================
// Custom Services
// =====================================

builder.Services.AddScoped<JwtTokenService>();

builder.Services.Configure<ResendSettings>(
    builder.Configuration.GetSection("Resend"));

builder.Services.AddHttpClient<IEmailService, ResendEmailService>();

builder.Services.AddSingleton<
    IAuthorizationPolicyProvider,
    PermissionPolicyProvider>();

builder.Services.AddScoped<
    IAuthorizationHandler,
    PermissionAuthorizationHandler>();

builder.Services.AddScoped<DevelopmentDataSeeder>();

builder.Services.AddNotificationInfrastructure(
    builder.Configuration);

var app = builder.Build();

// =====================================
// Swagger UI
// =====================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();

    var seeder =
        scope.ServiceProvider
            .GetRequiredService<DevelopmentDataSeeder>();

    await seeder.SeedAsync();
}

app.UseStaticFiles();

app.UseHttpsRedirection();

// Authentication MUST come before Authorization
app.UseAuthentication();

app.UseMiddleware<ForcePasswordChangeMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();