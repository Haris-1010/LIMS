using Lims.Api.Security;
using Lims.Application.Security;
using Lims.Infrastructure.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>
    /// Returns an anti-forgery token for the current user. The client sends it in the X-XSRF-TOKEN header
    /// on every POST/PUT/DELETE. Fetch a new one after signing in or out (tokens are tied to the user).
    /// </summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request.Username, request.Password,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);

        if (result.Session is null)
        {
            return result.Failure switch
            {
                LoginFailure.LockedOut => Problem(statusCode: 423, title: "This account is temporarily locked after repeated failed sign-ins.",
                    extensions: new Dictionary<string, object?> { ["code"] = "locked_out", ["lockedUntilUtc"] = result.LockedUntilUtc }),
                LoginFailure.Inactive => Problem(statusCode: 403, title: "This account is deactivated.",
                    extensions: new Dictionary<string, object?> { ["code"] = "account_inactive" }),
                _ => Problem(statusCode: 401, title: "Invalid username or password.",
                    extensions: new Dictionary<string, object?> { ["code"] = "invalid_credentials" }),
            };
        }

        var props = new AuthenticationProperties { IsPersistent = request.RememberMe };
        if (request.RememberMe) props.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7);
        SessionValidator.MarkValidated(props);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, SessionPrincipal.Create(result.Session), props);
        return Ok(ToMe(result.Session));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var session = await auth.BuildSessionAsync(User.UserId(), User.ActiveBranchSelection(), ct);
        return session is null ? Unauthorized() : Ok(ToMe(session));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(User.UserId(), request.CurrentPassword, request.NewPassword, ct);
        // The stamp changed, which ends other sessions; re-issue this one so the user stays signed in here.
        return await Reissue(User.ActiveBranchSelection(), ct);
    }

    /// <summary>Switch the working branch. 0 = all branches (only for users with access to every branch).</summary>
    [HttpPost("active-branch")]
    public async Task<IActionResult> ActiveBranch(ActiveBranchRequest request, CancellationToken ct)
    {
        var selection = request.BranchId ?? AuthService.AllBranchesSelection;
        var session = await auth.BuildSessionAsync(User.UserId(), selection, ct);
        if (session is null) return Unauthorized();
        var granted = selection == AuthService.AllBranchesSelection ? session.AllBranches && session.ActiveBranchId is null
                                                                    : session.ActiveBranchId == selection;
        if (!granted)
            return Problem(statusCode: 403, title: "You do not have access to this branch.",
                extensions: new Dictionary<string, object?> { ["code"] = "branch_forbidden" });
        return await Reissue(selection, ct);
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> Preferences(PreferencesRequest request, CancellationToken ct)
    {
        await auth.SetLanguageAsync(User.UserId(), request.Language, ct);
        return NoContent();
    }

    private async Task<IActionResult> Reissue(int? branchSelection, CancellationToken ct)
    {
        var session = await auth.BuildSessionAsync(User.UserId(), branchSelection, ct);
        if (session is null) return Unauthorized();
        var current = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var props = current.Properties ?? new AuthenticationProperties();
        SessionValidator.MarkValidated(props);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, SessionPrincipal.Create(session), props);
        return Ok(ToMe(session));
    }

    /// <summary>Client view of the session (the security stamp stays server-side).</summary>
    private static object ToMe(SessionInfo s) => new
    {
        s.UserId, s.Username, s.DisplayNameEn, s.DisplayNameUr, s.Designation, s.PreferredLanguage, s.MustChangePassword,
        s.OrganizationId, s.OrganizationNameEn, s.OrganizationNameUr, s.AllBranches, s.Branches, s.ActiveBranchId,
        s.Permissions, s.RoleNames,
    };
}
