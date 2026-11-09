using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Lims.Api.Security;

/// <summary>
/// Requires the signed-in user to hold a permission, e.g. [HasPermission(Permissions.UsersManage)].
/// Several keys in one attribute mean "any of these"; several attributes mean "all of these".
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(params string[] anyOf) : AuthorizeAttribute(PolicyPrefix + string.Join('|', anyOf))
{
    public const string PolicyPrefix = "perm:";
}

/// <summary>Creates "perm:x" policies on demand so every permission key does not need registering.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        var anyOf = policyName[HasPermissionAttribute.PolicyPrefix.Length..].Split('|');
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(LimsClaims.Permission, anyOf)
            .Build();
    }
}
