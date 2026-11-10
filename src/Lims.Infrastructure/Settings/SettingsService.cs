using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Settings;
using Lims.Domain.Core;
using Lims.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Settings;

/// <summary>
/// The business settings the system actually acts on. Only settings listed in <see cref="Known"/> can be read or changed here, each with a fixed
/// kind and limits, so a typo or an out-of-range value can never be saved. Changes are audited automatically (the setting row is an audited entity).
/// Settings are organisation-wide; branch overrides are not used.
/// </summary>
public sealed class SettingsService(LimsDbContext db)
{
    private static SettingDefinition Find(string key) => SettingsCatalog.Known.FirstOrDefault(d => d.Key == key) ?? throw BusinessRuleException.NotFound("Setting");

    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");

    public async Task<List<SettingDto>> ListAsync(CancellationToken ct)
    {
        var keys = SettingsCatalog.Known.Select(k => k.Key).ToList();
        var rows = await db.BusinessSettings.AsNoTracking().Where(s => s.BranchId == null && keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, ct);
        return SettingsCatalog.Known.Select(d => ToDto(d, rows.GetValueOrDefault(d.Key))).ToList();
    }

    private static SettingDto ToDto(SettingDefinition d, BusinessSetting? row)
    {
        var value = row?.Value ?? d.Default;
        return new SettingDto(d.Key, d.Group, d.Kind, value, d.Default, string.Equals(value, d.Default, StringComparison.OrdinalIgnoreCase), d.Min, d.Max,
            d.DescriptionEn, d.DescriptionUr, row is null ? "" : Convert.ToBase64String(row.RowVersion));
    }

    public async Task<SettingDto> UpdateAsync(string key, SettingUpdate input, CancellationToken ct)
    {
        var def = Find(key);
        var value = SettingsCatalog.Normalise(def, input.Value);
        var row = await db.BusinessSettings.SingleOrDefaultAsync(s => s.BranchId == null && s.Key == key, ct);
        if (row is null)
        {
            if (!string.IsNullOrEmpty(input.RowVersion)) throw BusinessRuleException.Conflict("concurrency", "This setting was changed by someone else. Reload and try again.");
            row = new BusinessSetting { OrganizationId = OrgId, BranchId = null, Key = key, Value = value };
            db.BusinessSettings.Add(row);
        }
        else
        {
            if (string.IsNullOrEmpty(input.RowVersion)) throw BusinessRuleException.Conflict("concurrency", "This setting was changed by someone else. Reload and try again.");
            db.Entry(row).Property(x => x.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
            row.Value = value;
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw BusinessRuleException.Conflict("concurrency", "This setting was changed by someone else. Reload and try again.");
        }
        return ToDto(def, row);
    }
}
