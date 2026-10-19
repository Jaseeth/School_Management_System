namespace SchoolManagement.API.Services;

public static class BrowserPushReadiness
{
    public static object Describe(IConfiguration configuration)
    {
        var names = new[] { "ApiKey", "ProjectId", "MessagingSenderId", "AppId", "VapidKey" };
        var web = configuration.GetSection("Firebase:Web");
        var missing = names.Where(n => string.IsNullOrWhiteSpace(web[n])).ToArray();
        var projectsMatch = !string.IsNullOrWhiteSpace(web["ProjectId"]) && web["ProjectId"] == configuration["Firebase:ProjectId"];
        var keys = names.Select(n => "Firebase:Web:" + n).Append("Firebase:ProjectId");
        var sources = keys.Select(key => new {
            setting = key,
            source = configuration is IConfigurationRoot root
                ? root.Providers.Reverse().FirstOrDefault(p => p.TryGet(key, out _))?.GetType().Name ?? "Not found"
                : "Unknown"
        }).ToArray();
        return new { configured = missing.Length == 0 && projectsMatch, missing, projectsMatch, sources };
    }
}
