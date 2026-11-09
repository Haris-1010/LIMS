using System.Globalization;
using System.Security.Claims;
using Lims.Application.Security;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Lims.Api.Security;

/// <summary>Claims stored in the session cookie (encrypted and signed by ASP.NET Core Data Protection).</summary>
public static class LimsClaims
{
    public const string UserId = ClaimTypes.NameIdentifier;
    public const string Name = ClaimTypes.Name;
    public const string Organization = "lims:org";
    public const string Stamp = "lims:stamp";
    public const string Permission = "lims:perm";
    public const string Branch = "lims:branch";
    public const string AllBranches = "lims:allBranches";
    /// <summary>Selected branch id, or "0" for the all-branches view.</summary>
    public const string ActiveBranch = "lims:activeBranch";
}

public static class SessionPrincipal
{
    public static ClaimsPrincipal Create(SessionInfo s)
    {
        var claims = new List<Claim>
        {
            new(LimsClaims.UserId, s.UserId.ToString(CultureInfo.InvariantCulture)),
            new(LimsClaims.Name, s.Username),
            new(LimsClaims.Organization, s.OrganizationId.ToString(CultureInfo.InvariantCulture)),
            new(LimsClaims.Stamp, s.SecurityStamp),
            new(LimsClaims.AllBranches, s.AllBranches ? "true" : "false"),
            new(LimsClaims.ActiveBranch, (s.ActiveBranchId ?? 0).ToString(CultureInfo.InvariantCulture)),
        };
        claims.AddRange(s.Permissions.Select(p => new Claim(LimsClaims.Permission, p)));
        claims.AddRange(s.Branches.Select(b => new Claim(LimsClaims.Branch, b.Id.ToString(CultureInfo.InvariantCulture))));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, LimsClaims.Name, null));
    }

    public static int UserId(this ClaimsPrincipal p) => Int(p, LimsClaims.UserId) ?? throw new InvalidOperationException("Not signed in.");
    public static int? OrganizationId(this ClaimsPrincipal p) => Int(p, LimsClaims.Organization);
    public static string? Stamp(this ClaimsPrincipal p) => p.FindFirstValue(LimsClaims.Stamp);
    public static bool AllBranches(this ClaimsPrincipal p) => p.FindFirstValue(LimsClaims.AllBranches) == "true";
    public static int[] BranchIds(this ClaimsPrincipal p) =>
        p.FindAll(LimsClaims.Branch).Select(c => int.Parse(c.Value, CultureInfo.InvariantCulture)).ToArray();
    /// <summary>Raw selection: 0 = all branches, otherwise a branch id.</summary>
    public static int? ActiveBranchSelection(this ClaimsPrincipal p) => Int(p, LimsClaims.ActiveBranch);

    private static int? Int(ClaimsPrincipal p, string type) =>
        int.TryParse(p.FindFirstValue(type), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}
