using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Reports;
using Lims.Domain.Core;
using Lims.Domain.Reports;
using Lims.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Reports;

/// <summary>
/// Named report designs of the organisation. A design changes only how a report is drawn, so it is chosen when a report is
/// viewed or printed and is never copied into the frozen snapshot. With no design saved the built-in classic design is used.
/// </summary>
public sealed class ReportLayoutService(LimsDbContext db)
{
    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");

    public async Task<List<ReportLayoutDto>> ListAsync(CancellationToken ct)
    {
        var rows = await db.ReportLayouts.AsNoTracking().OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    /// <summary>True when the id is a design of the caller's organisation (used to record which design a print used).</summary>
    public Task<bool> ExistsAsync(int id, CancellationToken ct) => db.ReportLayouts.AnyAsync(x => x.Id == id, ct);

    public async Task<int> CreateAsync(ReportLayoutInput input, CancellationToken ct)
    {
        var design = Validate(input);
        var layout = new ReportLayout { OrganizationId = OrgId, Name = input.Name.Trim(), DesignJson = ReportDesignRules.Serialize(design) };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (input.IsDefault) await ClearDefaultAsync(null, ct);
        layout.IsDefault = input.IsDefault;
        db.ReportLayouts.Add(layout);
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return layout.Id;
    }

    public async Task UpdateAsync(int id, ReportLayoutUpdate input, CancellationToken ct)
    {
        var design = Validate(input);
        var layout = await db.ReportLayouts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Report design");
        db.Entry(layout).Property(x => x.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (input.IsDefault && !layout.IsDefault) await ClearDefaultAsync(id, ct);
        layout.Name = input.Name.Trim();
        layout.IsDefault = input.IsDefault;
        layout.DesignJson = ReportDesignRules.Serialize(design);
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var layout = await db.ReportLayouts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Report design");
        db.ReportLayouts.Remove(layout);
        await db.SaveChangesAsync(ct);
    }

    private static ReportDesign Validate(ReportLayoutInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw new BusinessRuleException("report_layout_name_required", "Give the design a name.");
        var problem = ReportDesignRules.Problem(input.Design);
        if (problem is not null) throw new BusinessRuleException("report_layout_invalid", $"The design setting '{problem}' is not valid.", 400, new { setting = problem });
        return input.Design;
    }

    private async Task ClearDefaultAsync(int? exceptId, CancellationToken ct)
    {
        var current = await db.ReportLayouts.Where(x => x.IsDefault && x.Id != exceptId).ToListAsync(ct);
        foreach (var c in current) c.IsDefault = false;
        // Saved first so the filtered unique index (one default per organisation) never sees two defaults.
        if (current.Count > 0) await db.SaveChangesAsync(ct);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql && sql.Message.Contains("UX_ReportLayouts_Name"))
        {
            throw BusinessRuleException.Conflict("report_layout_name_taken", "A design with this name already exists.");
        }
    }

    private static ReportLayoutDto ToDto(ReportLayout x) =>
        new(x.Id, x.Name, x.IsDefault, ReportDesignRules.Read(x.DesignJson), RowVersion.ToToken(x.RowVersion));
}
