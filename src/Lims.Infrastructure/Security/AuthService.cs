using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Security;
using Lims.Domain.Security;
using Lims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Security;

/// <summary>Sign-in, lockout, session building and self-service password change.</summary>
public sealed class AuthService(LimsDbContext db, IPasswordHasher<User> hasher, ILabClock clock)
{
    public const int AllBranchesSelection = 0;
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Verifying against a dummy hash for unknown usernames keeps response time the same,
    // so timing does not reveal which usernames exist.
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(new User(), "dummy-password-for-timing-1");

    public async Task<LoginResult> LoginAsync(string username, string password, string? ip, string? userAgent, CancellationToken ct)
    {
        var name = username.Trim();
        var now = clock.UtcNow;

        // No organisation is known before sign-in, so this one lookup ignores tenant filters.
        var user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Username == name, ct);
        if (user is null)
        {
            hasher.VerifyHashedPassword(new User(), DummyHash, password);
            await RecordAsync(null, name, false, "unknown_user", ip, userAgent, ct);
            return new LoginResult(null, LoginFailure.InvalidCredentials);
        }

        // From here on we act within the user's organisation (tenant guard, audit stamps).
        db.Request.OrganizationId = user.OrganizationId;

        if (user.LockoutEndUtc > now)
        {
            await RecordAsync(user, name, false, "locked_out", ip, userAgent, ct);
            return new LoginResult(null, LoginFailure.LockedOut, user.LockoutEndUtc);
        }

        var verify = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verify == PasswordVerificationResult.Failed)
        {
            user.AccessFailedCount++;
            var locked = user.AccessFailedCount >= MaxFailedAttempts;
            if (locked)
            {
                user.LockoutEndUtc = now.Add(LockoutDuration);
                user.AccessFailedCount = 0;
            }
            await db.SaveChangesAsync(ct);
            await RecordAsync(user, name, false, locked ? "locked_now" : "bad_password", ip, userAgent, ct);
            return locked
                ? new LoginResult(null, LoginFailure.LockedOut, user.LockoutEndUtc)
                : new LoginResult(null, LoginFailure.InvalidCredentials);
        }

        var orgActive = await db.Organizations.Where(o => o.Id == user.OrganizationId).Select(o => o.IsActive).SingleAsync(ct);
        if (!user.IsActive || !orgActive)
        {
            await RecordAsync(user, name, false, "inactive", ip, userAgent, ct);
            return new LoginResult(null, LoginFailure.Inactive);
        }

        db.Request.UserId = user.Id;
        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, password);
        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = now;
        await db.SaveChangesAsync(ct);
        await RecordAsync(user, name, true, null, ip, userAgent, ct);

        return new LoginResult(await BuildSessionAsync(user.Id, null, ct), null);
    }

    /// <summary>
    /// Loads the user's current permissions and branch access. Called at sign-in and periodically while
    /// the session is open, so role or branch changes take effect without signing out.
    /// Returns null if the user can no longer sign in (deactivated, locked, organisation inactive).
    /// </summary>
    public async Task<SessionInfo?> BuildSessionAsync(int userId, int? preferredBranchId, CancellationToken ct)
    {
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id, u.OrganizationId, u.Username, u.DisplayNameEn, u.DisplayNameUr, u.Designation, u.PreferredLanguage,
                u.MustChangePassword, u.AllBranches, u.DefaultBranchId, u.IsActive, u.LockoutEndUtc, u.SecurityStamp,
            })
            .SingleOrDefaultAsync(ct);
        if (user is null || !user.IsActive || user.LockoutEndUtc > clock.UtcNow) return null;

        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == user.OrganizationId)
            .Select(o => new { o.NameEn, o.NameUr, o.IsActive }).SingleAsync(ct);
        if (!org.IsActive) return null;

        var roles = await db.UserRoles.IgnoreQueryFilters().AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.OrganizationId == user.OrganizationId && ur.Role!.IsActive)
            .Select(ur => new { ur.Role!.NameEn, Keys = ur.Role.Permissions.Select(p => p.Permission!.Key) })
            .ToListAsync(ct);

        var branchQuery = db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.OrganizationId == user.OrganizationId && b.IsActive);
        if (!user.AllBranches)
            branchQuery = branchQuery.Where(b => db.UserBranches.IgnoreQueryFilters().Any(ub => ub.UserId == userId && ub.BranchId == b.Id));
        var branches = await branchQuery
            .OrderByDescending(b => b.IsMain).ThenBy(b => b.NameEn)
            .Select(b => new BranchRef(b.Id, b.Code, b.NameEn, b.NameUr, b.IsMain))
            .ToListAsync(ct);

        // preferredBranchId: null = use the default, 0 = "all branches", n = that branch (if allowed).
        // The "all branches" view (ActiveBranchId null) is only possible for users with access to every branch.
        int? active;
        if (preferredBranchId == AllBranchesSelection && user.AllBranches) active = null;
        else if (preferredBranchId is > 0 && branches.Any(b => b.Id == preferredBranchId)) active = preferredBranchId;
        else if (user.DefaultBranchId is int d && branches.Any(b => b.Id == d)) active = d;
        else if (user.AllBranches) active = null;
        else active = branches.Count > 0 ? branches[0].Id : null;

        return new SessionInfo(
            user.Id, user.OrganizationId, org.NameEn, org.NameUr, user.Username, user.DisplayNameEn, user.DisplayNameUr,
            user.Designation, user.PreferredLanguage, user.MustChangePassword, user.AllBranches, branches, active,
            roles.SelectMany(r => r.Keys).Distinct().Order().ToList(),
            roles.Select(r => r.NameEn).Order().ToList(),
            user.SecurityStamp);
    }

    public async Task ChangePasswordAsync(int userId, string current, string next, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct) ?? throw BusinessRuleException.NotFound("User");
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, current) == PasswordVerificationResult.Failed)
            throw new BusinessRuleException("current_password_incorrect", "The current password is incorrect.");
        if (current == next)
            throw new BusinessRuleException("password_unchanged", "The new password must be different from the current one.");
        PasswordPolicy.Enforce(next, user.Username);

        user.PasswordHash = hasher.HashPassword(user, next);
        user.MustChangePassword = false;
        user.SecurityStamp = Guid.NewGuid().ToString("N"); // signs out other sessions
        db.AddBusinessAudit("User.PasswordChanged", nameof(User), user.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetLanguageAsync(int userId, string language, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct) ?? throw BusinessRuleException.NotFound("User");
        user.PreferredLanguage = language;
        await db.SaveChangesAsync(ct);
    }

    private async Task RecordAsync(User? user, string name, bool ok, string? reason, string? ip, string? ua, CancellationToken ct)
    {
        db.LoginHistory.Add(new LoginHistory
        {
            OrganizationId = user?.OrganizationId,
            UserId = user?.Id,
            UsernameAttempted = name.Length > 64 ? name[..64] : name,
            Succeeded = ok,
            FailureReason = reason,
            IpAddress = ip,
            UserAgent = ua is { Length: > 512 } ? ua[..512] : ua,
            AtUtc = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
