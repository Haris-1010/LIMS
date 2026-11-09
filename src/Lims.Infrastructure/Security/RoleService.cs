using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Security;
using Lims.Domain.Security;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Security;

/// <summary>Configurable roles. Permission keys come from the fixed catalog; roles are per organisation.</summary>
public sealed class RoleService(LimsDbContext db, ILabClock clock)
{
    public Task<List<PermissionItem>> PermissionsAsync(CancellationToken ct) =>
        db.Permissions.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new PermissionItem(p.Id, p.Key, p.Module, p.DescriptionEn, p.DescriptionUr))
            .ToListAsync(ct);

    public Task<List<RoleListItem>> ListAsync(CancellationToken ct) =>
        db.Roles.AsNoTracking()
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.NameEn)
            .Select(r => new RoleListItem(
                r.Id, r.Code, r.NameEn, r.NameUr, r.Description, r.IsSystem, r.IsActive,
                db.UserRoles.Count(ur => ur.RoleId == r.Id && ur.User!.IsActive),
                r.Permissions.Select(p => p.Permission!.Key).OrderBy(k => k).ToList(),
                Convert.ToBase64String(r.RowVersion)))
            .ToListAsync(ct);

    public async Task<int> CreateAsync(RoleCreate input, CancellationToken ct)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.Roles.AnyAsync(r => r.Code == code, ct))
            throw BusinessRuleException.Conflict("role_code_taken", "A role with this code already exists.");

        var orgId = db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");
        var role = new Role { OrganizationId = orgId, Code = code };
        Apply(role, input);
        await SetPermissionsAsync(role, input.Permissions, ct);
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return role.Id;
    }

    public async Task UpdateAsync(int id, RoleUpdate input, CancellationToken ct)
    {
        var role = await db.Roles.Include(r => r.Permissions).ThenInclude(p => p.Permission)
                       .SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw BusinessRuleException.NotFound("Role");
        db.Entry(role).Property(r => r.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);

        Apply(role, input);
        role.IsActive = input.IsActive;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SetPermissionsAsync(role, input.Permissions, ct);
        await db.SaveChangesAsync(ct);
        await AdminGuard.EnsureAdministratorRemainsAsync(db, clock.UtcNow, ct);
        await tx.CommitAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var role = await db.Roles.Include(r => r.Permissions).SingleOrDefaultAsync(r => r.Id == id, ct)
                   ?? throw BusinessRuleException.NotFound("Role");
        if (role.IsSystem)
            throw BusinessRuleException.Conflict("role_is_system", "Built-in roles cannot be deleted; deactivate them instead.");
        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == id, ct))
            throw BusinessRuleException.Conflict("role_in_use", "Remove this role from all users before deleting it.");

        db.RolePermissions.RemoveRange(role.Permissions);
        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Role role, RoleEdit input)
    {
        role.NameEn = input.NameEn.Trim();
        role.NameUr = string.IsNullOrWhiteSpace(input.NameUr) ? null : input.NameUr.Trim();
        role.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
    }

    private async Task SetPermissionsAsync(Role role, string[] keys, CancellationToken ct)
    {
        var wanted = keys.Distinct().ToArray();
        var perms = await db.Permissions.Where(p => wanted.Contains(p.Key)).ToDictionaryAsync(p => p.Key, p => p.Id, ct);
        var unknown = wanted.Except(perms.Keys).ToList();
        if (unknown.Count > 0)
            throw new BusinessRuleException("permission_invalid", $"Unknown permission(s): {string.Join(", ", unknown)}.");

        var current = role.Permissions.ToDictionary(p => p.PermissionId);
        var removed = current.Values.Where(p => !perms.ContainsValue(p.PermissionId)).ToList();
        var added = perms.Where(kv => !current.ContainsKey(kv.Value)).ToList();

        db.RolePermissions.RemoveRange(removed);
        foreach (var (_, pid) in added)
            role.Permissions.Add(new RolePermission { OrganizationId = role.OrganizationId, PermissionId = pid });

        if (role.Id != 0 && (removed.Count > 0 || added.Count > 0))
            db.AddBusinessAudit("Role.PermissionsChanged", nameof(Role), role.Id, new
            {
                added = added.Select(a => a.Key).Order(),
                removed = removed.Select(r => r.Permission?.Key ?? r.PermissionId.ToString()).Order(),
            });
    }
}
