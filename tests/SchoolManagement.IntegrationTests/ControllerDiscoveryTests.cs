using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.API.Controllers;

namespace SchoolManagement.IntegrationTests;

public class ControllerDiscoveryTests
{
    [Fact]
    public async Task ApiControllersCanBeDiscoveredWithoutExposingReportFilter()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(ReportsController).Assembly.GetName().Name
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(ReportsController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        var actions = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToList();
        Assert.Contains(actions, action => action.ControllerTypeInfo.AsType() == typeof(ReportsController)
            && action.ActionName == nameof(ReportsController.GetStudentAcademicProfilePdf));
        Assert.DoesNotContain(actions, action => action.ControllerTypeInfo.AsType() == typeof(ReportsController)
            && action.ActionName == nameof(ReportsController.OnActionExecutionAsync));
    }
}
