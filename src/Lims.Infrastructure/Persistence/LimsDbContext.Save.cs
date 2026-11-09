using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lims.Application.Abstractions;
using Lims.Domain.Audit;
using Lims.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Lims.Infrastructure.Persistence;

/// <summary>
/// Save pipeline, applied to every SaveChanges:
///  1. Tenant write guard: new rows get the caller's OrganizationId; rows of another organisation are rejected.
///  2. Stamps: CreatedAt/By, UpdatedAt/By (callers cannot overwrite Created*).
///  3. Soft delete: deleting an ISoftDelete entity marks it deleted instead.
///  4. Audit: IAudited changes are written to audit.AuditLogs in the same transaction as the change.
/// </summary>
public sealed partial class LimsDbContext
{
    private static readonly JsonSerializerOptions AuditJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private static readonly HashSet<string> NotAudited =
    [
        nameof(AuditableEntity<int>.RowVersion),
        nameof(AuditableEntity<int>.UpdatedAtUtc),
        nameof(AuditableEntity<int>.UpdatedByUserId),
        nameof(AuditableEntity<int>.CreatedAtUtc),
        nameof(AuditableEntity<int>.CreatedByUserId),
    ];

    private bool _savingAudit;

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        if (_savingAudit)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);

        ChangeTracker.DetectChanges();
        var now = clock.UtcNow;
        var pending = new List<PendingAudit>();

        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged) continue;

            GuardTenant(entry);
            ApplySoftDelete(entry, now);
            ApplyStamps(entry, now);

            if (entry.Entity is IAudited)
                pending.Add(Capture(entry));
        }

        if (pending.Count == 0)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);

        // The change and its audit rows commit together or not at all.
        var ownTransaction = Database.CurrentTransaction is null ? await Database.BeginTransactionAsync(ct) : null;
        try
        {
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);

            foreach (var p in pending)
                AuditLogs.Add(p.ToLog(now, request));

            _savingAudit = true;
            try { await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct); }
            finally { _savingAudit = false; }

            if (ownTransaction is not null) await ownTransaction.CommitAsync(ct);
            return result;
        }
        finally
        {
            if (ownTransaction is not null) await ownTransaction.DisposeAsync();
        }
    }

    /// <summary>
    /// Records a business action that is not a simple column change (e.g. permissions granted to a role).
    /// Saved with the next SaveChanges, in the same transaction.
    /// </summary>
    public void AddBusinessAudit(string action, string entityType, object entityId, object? changes = null, string? reason = null)
    {
        AuditLogs.Add(new AuditLog
        {
            OrganizationId = request.OrganizationId,
            BranchId = request.ActiveBranchId,
            UserId = request.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString() ?? "",
            Changes = changes is null ? null : JsonSerializer.Serialize(changes, AuditJson),
            Reason = reason,
            IpAddress = request.IpAddress,
            CorrelationId = request.CorrelationId,
            AtUtc = clock.UtcNow,
        });
    }

    private void GuardTenant(EntityEntry entry)
    {
        if (entry.Entity is not ITenantEntity tenant || request.IsSystem) return;

        var orgId = request.OrganizationId
            ?? throw new TenantViolationException($"Cannot save {entry.Metadata.ClrType.Name}: no organisation in the request context.");

        if (entry.State == EntityState.Added && tenant.OrganizationId == 0)
            tenant.OrganizationId = orgId;

        var original = entry.State == EntityState.Added
            ? tenant.OrganizationId
            : (int)entry.Property(nameof(ITenantEntity.OrganizationId)).OriginalValue!;

        if (original != orgId || tenant.OrganizationId != orgId)
            throw new TenantViolationException(
                $"{entry.Metadata.ClrType.Name} belongs to organisation {original}, not the caller's organisation {orgId}.");
    }

    private void ApplySoftDelete(EntityEntry entry, DateTime now)
    {
        if (entry.State != EntityState.Deleted || entry.Entity is not ISoftDelete soft) return;
        entry.State = EntityState.Modified;
        soft.IsDeleted = true;
        soft.DeletedAtUtc = now;
        soft.DeletedByUserId = request.UserId;
    }

    private void ApplyStamps(EntityEntry entry, DateTime now)
    {
        if (!IsAuditable(entry.Metadata.ClrType)) return;

        if (entry.State == EntityState.Added)
        {
            entry.Property(nameof(AuditableEntity<int>.CreatedAtUtc)).CurrentValue = now;
            entry.Property(nameof(AuditableEntity<int>.CreatedByUserId)).CurrentValue = request.UserId;
        }
        else if (entry.State == EntityState.Modified)
        {
            entry.Property(nameof(AuditableEntity<int>.CreatedAtUtc)).IsModified = false;
            entry.Property(nameof(AuditableEntity<int>.CreatedByUserId)).IsModified = false;
            entry.Property(nameof(AuditableEntity<int>.UpdatedAtUtc)).CurrentValue = now;
            entry.Property(nameof(AuditableEntity<int>.UpdatedByUserId)).CurrentValue = request.UserId;
        }
    }

    private static PendingAudit Capture(EntityEntry entry)
    {
        var action = entry.State switch
        {
            EntityState.Added => "Insert",
            EntityState.Deleted => "Delete",
            _ => entry.Entity is ISoftDelete { IsDeleted: true } &&
                 entry.Property(nameof(ISoftDelete.IsDeleted)).IsModified ? "Delete" : "Update",
        };

        var changes = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            if (NotAudited.Contains(name) || prop.Metadata.IsPrimaryKey()) continue;
            var sensitive = prop.Metadata.PropertyInfo?.GetCustomAttribute<SensitiveAttribute>() is not null;

            switch (entry.State)
            {
                case EntityState.Added:
                    changes[name] = new { @new = sensitive ? "***" : prop.CurrentValue };
                    break;
                case EntityState.Deleted:
                    changes[name] = new { old = sensitive ? "***" : prop.OriginalValue };
                    break;
                case EntityState.Modified when prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue):
                    changes[name] = sensitive
                        ? new { old = "***", @new = "***" } // record that it changed, never the value
                        : new { old = prop.OriginalValue, @new = prop.CurrentValue };
                    break;
            }
        }

        return new PendingAudit(entry, action, changes);
    }

    private sealed record PendingAudit(EntityEntry Entry, string Action, Dictionary<string, object?> Changes)
    {
        public AuditLog ToLog(DateTime now, RequestContext request)
        {
            // Keys are read after the first save so database-generated ids are known.
            var key = string.Join(",", Entry.Metadata.FindPrimaryKey()!.Properties.Select(p => Entry.Property(p.Name).CurrentValue));
            var orgId = Entry.Entity switch
            {
                ITenantEntity t => t.OrganizationId,
                Domain.Core.Organization o => o.Id,
                _ => request.OrganizationId,
            };
            return new AuditLog
            {
                OrganizationId = orgId,
                BranchId = Entry.Entity is IBranchEntity b ? b.BranchId : request.ActiveBranchId,
                UserId = request.UserId,
                Action = Action,
                EntityType = Entry.Metadata.ClrType.Name,
                EntityId = key,
                Changes = JsonSerializer.Serialize(Changes, AuditJson),
                IpAddress = request.IpAddress,
                CorrelationId = request.CorrelationId,
                AtUtc = now,
            };
        }
    }
}
