using System.Text.RegularExpressions;
using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Security;
using Lims.Domain.Security;
using Lims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Security;

/// <summary>User administration within the caller's organisation (tenant filters apply to every query).</summary>
public sealed partial class UserService(LimsDbContext db, IPasswordHasher<User> hasher, ILabClock clock)
{
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{2,63}$")]
    private static partial Regex UsernamePattern();

    public async Task<PagedResult<UserListItem>> ListAsync(UserQuery q, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(u => u.Username.Contains(s) || u.DisplayNameEn.Contains(s) ||
                                     (u.DisplayNameUr != null && u.DisplayNameUr.Contains(s)) ||
                                     (u.Email != null && u.Email.Contains(s)) || (u.Phone != null && u.Phone.Contains(s)));
        }
        if (q.IsActive is bool active) query = query.Where(u => u.IsActive == active);
        if (q.RoleId is int roleId) query = query.Where(u => u.Roles.Any(r => r.RoleId == roleId));

        query = (q.Sort, q.Desc) switch
        {
            ("username", false) => query.OrderBy(u => u.Username),
            ("username", true) => query.OrderByDescending(u => u.Username),
            ("lastLogin", false) => query.OrderBy(u => u.LastLoginAtUtc),
            ("lastLogin", true) => query.OrderByDescending(u => u.LastLoginAtUtc),
            (_, true) => query.OrderByDescending(u => u.DisplayNameEn),
            _ => query.OrderBy(u => u.DisplayNameEn),
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip(q.Skip).Take(q.PageSize)
            .Select(u => new UserListItem(
                u.Id, u.Username, u.DisplayNameEn, u.DisplayNameUr, u.Designation, u.Email, u.Phone, u.IsActive,
                u.LockoutEndUtc != null && u.LockoutEndUtc > now, u.LastLoginAtUtc, u.AllBranches,
                u.Roles.Select(r => r.Role!.NameEn).OrderBy(n => n).ToList(),
                u.Branches.Count))
            .ToListAsync(ct);

        return new PagedResult<UserListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<UserDetail> GetAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;
        return await db.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new UserDetail(
                u.Id, u.Username, u.DisplayNameEn, u.DisplayNameUr, u.Designation, u.Email, u.Phone, u.IsActive,
                u.LockoutEndUtc != null && u.LockoutEndUtc > now, u.LockoutEndUtc, u.MustChangePassword, u.LastLoginAtUtc,
                u.PreferredLanguage, u.AllBranches, u.DefaultBranchId,
                u.Roles.Select(r => r.RoleId).ToList(), u.Branches.Select(b => b.BranchId).ToList(),
                u.CreatedAtUtc, Convert.ToBase64String(u.RowVersion)))
            .SingleOrDefaultAsync(ct) ?? throw BusinessRuleException.NotFound("User");
    }

    public async Task<int> CreateAsync(UserCreate input, CancellationToken ct)
    {
        var username = input.Username.Trim().ToLowerInvariant();
        if (!UsernamePattern().IsMatch(username))
            throw new BusinessRuleException("username_invalid",
                "Username must be 3–64 characters: lowercase letters, digits, dot, dash or underscore.");
        // Usernames are unique across the whole installation, so check all organisations.
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username, ct))
            throw BusinessRuleException.Conflict("username_taken", "This username is already in use.");
        PasswordPolicy.Enforce(input.Password, username);

        var orgId = db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");
        var user = new User { OrganizationId = orgId, Username = username, MustChangePassword = true };
        Apply(user, input);
        user.PasswordHash = hasher.HashPassword(user, input.Password);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SetAccessAsync(user, input, ct);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return user.Id;
    }

    public async Task UpdateAsync(int id, UserUpdate input, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Roles).Include(u => u.Branches).SingleOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw BusinessRuleException.NotFound("User");
        db.Entry(user).Property(u => u.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);

        if (id == db.Request.UserId && !input.IsActive)
            throw new BusinessRuleException("cannot_deactivate_self", "You cannot deactivate your own account.");

        var wasActive = user.IsActive;
        Apply(user, input);
        user.IsActive = input.IsActive;
        if (wasActive && !input.IsActive)
            user.SecurityStamp = Guid.NewGuid().ToString("N"); // end open sessions immediately

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SetAccessAsync(user, input, ct);
        await db.SaveChangesAsync(ct);
        await AdminGuard.EnsureAdministratorRemainsAsync(db, clock.UtcNow, ct);
        await tx.CommitAsync(ct);
    }

    public async Task ResetPasswordAsync(int id, string newPassword, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct) ?? throw BusinessRuleException.NotFound("User");
        PasswordPolicy.Enforce(newPassword, user.Username);
        user.PasswordHash = hasher.HashPassword(user, newPassword);
        user.MustChangePassword = true;
        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        db.AddBusinessAudit("User.PasswordReset", nameof(User), user.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task UnlockAsync(int id, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct) ?? throw BusinessRuleException.NotFound("User");
        user.LockoutEndUtc = null;
        user.AccessFailedCount = 0;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LoginHistoryItem>> LoginHistoryAsync(int id, CancellationToken ct)
    {
        // LoginHistory has no tenant filter (failed attempts may have no organisation), so check explicitly.
        if (!await db.Users.AnyAsync(u => u.Id == id, ct)) throw BusinessRuleException.NotFound("User");
        return await db.LoginHistory.AsNoTracking()
            .Where(h => h.UserId == id && h.OrganizationId == db.Request.OrganizationId)
            .OrderByDescending(h => h.AtUtc).Take(50)
            .Select(h => new LoginHistoryItem(h.Id, h.AtUtc, h.Succeeded, h.FailureReason, h.IpAddress, h.UserAgent))
            .ToListAsync(ct);
    }

    private static void Apply(User user, UserEdit input)
    {
        user.DisplayNameEn = input.DisplayNameEn.Trim();
        user.DisplayNameUr = Blank(input.DisplayNameUr);
        user.Designation = Blank(input.Designation);
        user.Email = Blank(input.Email);
        user.Phone = Blank(input.Phone);
        user.PreferredLanguage = input.PreferredLanguage;
        user.AllBranches = input.AllBranches;
    }

    /// <summary>Validates and applies roles and branch access; records an audit entry when they change.</summary>
    private async Task SetAccessAsync(User user, UserEdit input, CancellationToken ct)
    {
        var roleIds = input.RoleIds.Distinct().ToArray();
        var branchIds = input.AllBranches ? [] : input.BranchIds.Distinct().ToArray();

        // Tenant filters mean ids from another organisation simply are not found.
        var validRoles = await db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Id).ToListAsync(ct);
        if (validRoles.Count != roleIds.Length) throw new BusinessRuleException("role_invalid", "One or more roles do not exist.");
        if (roleIds.Length == 0) throw new BusinessRuleException("role_required", "Assign at least one role.");

        var validBranches = await db.Branches.Where(b => branchIds.Contains(b.Id)).Select(b => b.Id).ToListAsync(ct);
        if (validBranches.Count != branchIds.Length) throw new BusinessRuleException("branch_invalid", "One or more branches do not exist.");
        if (!input.AllBranches && branchIds.Length == 0)
            throw new BusinessRuleException("branch_required", "Give access to at least one branch, or to all branches.");

        if (input.DefaultBranchId is int def)
        {
            var allowed = input.AllBranches ? await db.Branches.AnyAsync(b => b.Id == def, ct) : branchIds.Contains(def);
            if (!allowed) throw new BusinessRuleException("default_branch_invalid", "The default branch must be one the user can access.");
        }
        user.DefaultBranchId = input.DefaultBranchId;

        var oldRoles = user.Roles.Select(r => r.RoleId).Order().ToArray();
        var oldBranches = user.Branches.Select(b => b.BranchId).Order().ToArray();

        // Link rows are removed explicitly (nothing cascades in this schema).
        foreach (var r in user.Roles.Where(r => !roleIds.Contains(r.RoleId)).ToList()) db.UserRoles.Remove(r);
        foreach (var rid in roleIds.Except(oldRoles)) user.Roles.Add(new UserRole { OrganizationId = user.OrganizationId, RoleId = rid });
        foreach (var b in user.Branches.Where(b => !branchIds.Contains(b.BranchId)).ToList()) db.UserBranches.Remove(b);
        foreach (var bid in branchIds.Except(oldBranches)) user.Branches.Add(new UserBranch { OrganizationId = user.OrganizationId, BranchId = bid });

        var newRoles = roleIds.Order().ToArray();
        var newBranches = branchIds.Order().ToArray();
        if (user.Id != 0 && (!oldRoles.SequenceEqual(newRoles) || !oldBranches.SequenceEqual(newBranches)))
            db.AddBusinessAudit("User.AccessChanged", nameof(User), user.Id,
                new { roles = new { old = oldRoles, @new = newRoles }, branches = new { old = oldBranches, @new = newBranches } });
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
