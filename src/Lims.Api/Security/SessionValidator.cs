using System.Globalization;
using Lims.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Lims.Api.Security;

/// <summary>
/// Re-checks every session against the database at most every Auth:RevalidateSeconds (default 2 minutes):
/// a deactivated/locked user, an inactive organisation or a changed security stamp (password change or
/// reset) ends the session; otherwise permissions and branch access are refreshed from current roles.
/// API calls get 401/403 instead of login-page redirects.
/// </summary>
public sealed class SessionValidator(IConfiguration config) : CookieAuthenticationEvents
{
    private TimeSpan Interval => TimeSpan.FromSeconds(config.GetValue("Auth:RevalidateSeconds", 120));
    private const string ValidatedKey = "lims.validatedUtc";

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal?.Identity?.IsAuthenticated != true) return;

        var props = context.Properties;
        var now = DateTimeOffset.UtcNow;
        if (props.Items.TryGetValue(ValidatedKey, out var raw) &&
            DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var last) &&
            now - last < Interval)
            return;

        var auth = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
        var session = await auth.BuildSessionAsync(principal.UserId(), principal.ActiveBranchSelection(), context.HttpContext.RequestAborted);

        if (session is null || session.SecurityStamp != principal.Stamp())
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        context.ReplacePrincipal(SessionPrincipal.Create(session));
        props.Items[ValidatedKey] = now.ToString("O", CultureInfo.InvariantCulture);
        context.ShouldRenew = true;
    }

    public static void MarkValidated(AuthenticationProperties props) =>
        props.Items[ValidatedKey] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
