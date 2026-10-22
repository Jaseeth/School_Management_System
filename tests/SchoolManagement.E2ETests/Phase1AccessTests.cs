using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolManagement.E2ETests;

[Collection("Phase1")]
public class Phase1AccessTests(Phase1Fixture host)
{
    [Theory]
    [InlineData("Admin")][InlineData("Principal")][InlineData("Deputy Principal")]
    [InlineData("Section Head")][InlineData("Teacher")][InlineData("Librarian")]
    public async Task HttpPipelineEnforcesBackupAndSettingsAndPublishingRoles(string role)
    {
        using var handler=new HttpClientHandler { ServerCertificateCustomValidationCallback=HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
        using var client=new HttpClient(handler){BaseAddress=new Uri(host.BaseUrl)};
        await host.BeforeLogin();
        var login=await client.PostAsJsonAsync("/api/Auth/login",new {email=Phase1Fixture.Email(role),password=host.Password});
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        var json=await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization=new("Bearer",json.GetProperty("token").GetString());
        Assert.Equal(role=="Admin"?HttpStatusCode.OK:HttpStatusCode.Forbidden,(await client.GetAsync("/api/backups")).StatusCode);
        var leader=role is "Admin" or "Principal" or "Deputy Principal";
        Assert.Equal(leader?HttpStatusCode.OK:HttpStatusCode.Forbidden,(await client.GetAsync("/api/school-settings/options")).StatusCode);
        if(leader) Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/marks-review/approved")).StatusCode);
    }
}
