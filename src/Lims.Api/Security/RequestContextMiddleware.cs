using Lims.Application.Abstractions;

namespace Lims.Api.Security;

/// <summary>
/// Fills the scoped <see cref="RequestContext"/> from the authenticated session so the data layer can
/// enforce organisation/branch isolation and stamp audit rows. Anonymous requests leave it empty
/// (which makes tenant data invisible).
/// </summary>
public sealed class RequestContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext http, RequestContext ctx)
    {
        ctx.IpAddress = http.Connection.RemoteIpAddress?.ToString();
        ctx.CorrelationId = http.TraceIdentifier;

        var user = http.User;
        if (user.Identity?.IsAuthenticated == true && user.OrganizationId() is int org)
        {
            ctx.OrganizationId = org;
            ctx.UserId = user.UserId();
            ctx.AllBranches = user.AllBranches();
            ctx.BranchIds = user.BranchIds();
            var active = user.ActiveBranchSelection();
            ctx.ActiveBranchId = active is > 0 ? active : null;
            ctx.Permissions = user.FindAll(LimsClaims.Permission).Select(c => c.Value).ToHashSet();
        }
        return next(http);
    }
}
