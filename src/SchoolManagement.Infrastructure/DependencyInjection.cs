using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Infrastructure.Notifications;

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

        return services;
    }
}