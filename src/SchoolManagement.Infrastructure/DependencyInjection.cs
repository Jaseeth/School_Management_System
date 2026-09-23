using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Infrastructure.Notifications;
using SchoolManagement.Application.Auditing;
using SchoolManagement.Infrastructure.Auditing;

namespace SchoolManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Firebase configuration from appsettings.json /
        // appsettings.Development.json
        services.Configure<FirebaseSettings>(
            configuration.GetSection("Firebase"));

        // Firebase push notification service
        services.AddScoped<
            IPushNotificationService,
            FirebasePushNotificationService>();

        services.AddHttpContextAccessor();

        services.AddScoped<
            IAuditLogService,
            AuditLogService>();

        return services;
    }
}