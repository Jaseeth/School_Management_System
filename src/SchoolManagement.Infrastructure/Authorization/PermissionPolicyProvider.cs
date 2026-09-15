using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SchoolManagement.Infrastructure.Authorization;

public class PermissionPolicyProvider
    : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(
        IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?>
        GetPolicyAsync(string policyName)
    {
        const string prefix = "Permission:";

        if (!policyName.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase))
        {
            return await base.GetPolicyAsync(policyName);
        }

        var permissionName =
            policyName.Substring(prefix.Length);

        var policy =
            new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(
                    new PermissionRequirement(
                        permissionName))
                .Build();

        return policy;
    }
}