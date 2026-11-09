using Lims.Application.Common;
using Lims.Domain.Security;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Security;

/// <summary>
/// Prevents an organisation from locking itself out: after any user/role change there must still be at
/// least one active, unlocked user whose active roles grant both users.manage and roles.manage.
/// Call inside the transaction, after SaveChanges, so the check sees the new state.
/// </summary>
internal static class AdminGuard
{
    public static async Task EnsureAdministratorRemainsAsync(LimsDbContext db, DateTime nowUtc, CancellationToken ct)
    {
        var adminKeys = new[] { Permissions.UsersManage, Permissions.RolesManage };
        var hasAdmin = await db.Users
            .Where(u => u.IsActive && (u.LockoutEndUtc == null || u.LockoutEndUtc <= nowUtc))
            .AnyAsync(u => adminKeys.All(key =>
                u.Roles.Any(ur => ur.Role!.IsActive && ur.Role.Permissions.Any(rp => rp.Permission!.Key == key))), ct);

        if (!hasAdmin)
            throw BusinessRuleException.Conflict("last_administrator",
                "This change would leave no active user who can manage users and roles.");
    }
}
