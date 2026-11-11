using Lims.Api.Licensing;

namespace Lims.Api.Security;

/// <summary>
/// Refuses every API call (403, code <c>license_required</c>) until this server holds a valid signed licence. The web app
/// itself still loads, so it can show the activation screen; the licence endpoints, the anti-forgery token and the health
/// check stay open. Off in the Development and Testing environments (<see cref="LicenseSettings.Enforce"/>).
/// </summary>
public sealed class LicenseEnforcementMiddleware(RequestDelegate next)
{
    public const string LicenseRequiredCode = "license_required";

    public async Task InvokeAsync(HttpContext http, LicenseService license)
    {
        if (!license.IsEnforced || LicenseGate.IsOpen(http.Request.Path))
        {
            await next(http);
            return;
        }

        var status = license.GetStatus();
        if (status.IsValid)
        {
            await next(http);
            return;
        }

        http.Response.StatusCode = StatusCodes.Status403Forbidden;
        http.Response.ContentType = "application/problem+json";
        await http.Response.WriteAsJsonAsync(new
        {
            title = status.Message,
            status = StatusCodes.Status403Forbidden,
            code = LicenseRequiredCode,
            reason = status.Reason,
            traceId = http.TraceIdentifier,
        });
    }
}

/// <summary>What works without a licence: everything outside /api (the web app and its files), and these API paths.</summary>
public static class LicenseGate
{
    private static readonly string[] OpenApiPaths = ["/api/license", "/api/health", "/api/auth/csrf"];

    public static bool IsOpen(PathString path)
    {
        if (!path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)) return true;
        return OpenApiPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }
}
