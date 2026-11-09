using Lims.Application.Abstractions;
using Lims.Application.Catalog;
using Lims.Application.Common;
using Lims.Domain.Catalog;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Catalog;

/// <summary>
/// Reference range workflow: Draft → Approved (by a user with catalog.clinical) → Retired.
/// Approved ranges are never edited; a correction is a new draft that replaces the old range on approval.
/// </summary>
public sealed class ReferenceRangeService(LimsDbContext db, ILabClock clock)
{
    public async Task<List<RangeDto>> ForParameterAsync(int parameterId, CancellationToken ct)
    {
        if (!await db.TestParameters.AnyAsync(p => p.Id == parameterId, ct)) throw BusinessRuleException.NotFound("Parameter");
        var ranges = await db.ReferenceRanges.AsNoTracking().Where(r => r.ParameterId == parameterId)
            .OrderBy(r => r.Status).ThenBy(r => r.Sex).ThenBy(r => r.AgeFromDays).ToListAsync(ct);
        var names = await UserNamesAsync(ranges, ct);
        return ranges.Select(r => ToDto(r, names)).ToList();
    }

    /// <summary>Draft ranges awaiting approval across the organisation (the pathologist's queue).</summary>
    public async Task<List<PendingRange>> PendingAsync(CancellationToken ct)
    {
        var drafts = await db.ReferenceRanges.AsNoTracking().Where(r => r.Status == RangeStatus.Draft)
            .Include(r => r.Parameter!).ThenInclude(p => p.Test)
            .OrderBy(r => r.CreatedAtUtc).ToListAsync(ct);
        var replacedIds = drafts.Where(d => d.ReplacesRangeId is not null).Select(d => d.ReplacesRangeId!.Value).ToList();
        var replaced = await db.ReferenceRanges.AsNoTracking().Where(r => replacedIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Code, ct);
        var names = await UserNamesAsync(drafts.Concat(replaced.Values), ct);

        return drafts.Select(d => new PendingRange(
            ToDto(d, names), d.Parameter!.TestId, d.Parameter.Test!.Code, d.Parameter.Test.NameEn, d.Parameter.NameEn,
            d.Parameter.ResultType, d.Parameter.UnitId is int u ? units.GetValueOrDefault(u) : null,
            d.ReplacesRangeId is int rid && replaced.TryGetValue(rid, out var old) ? ToDto(old, names) : null)).ToList();
    }

    public async Task<int> CreateAsync(int parameterId, RangeInput input, CancellationToken ct)
    {
        var parameter = await db.TestParameters.SingleOrDefaultAsync(p => p.Id == parameterId, ct) ?? throw BusinessRuleException.NotFound("Parameter");
        var range = new ReferenceRange { OrganizationId = parameter.OrganizationId, ParameterId = parameterId, Status = RangeStatus.Draft };
        await ApplyAsync(range, parameter, input, ct);
        db.ReferenceRanges.Add(range);
        await db.SaveChangesAsync(ct);
        return range.Id;
    }

    public async Task UpdateAsync(int id, RangeUpdate input, CancellationToken ct)
    {
        var range = await db.ReferenceRanges.Include(r => r.Parameter).SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw BusinessRuleException.NotFound("Range");
        if (range.Status != RangeStatus.Draft)
            throw BusinessRuleException.Conflict("range_not_draft", "Approved ranges can't be edited. Create a new draft that replaces it.");
        db.Entry(range).Property(r => r.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        await ApplyAsync(range, range.Parameter!, input, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteDraftAsync(int id, CancellationToken ct)
    {
        var range = await db.ReferenceRanges.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw BusinessRuleException.NotFound("Range");
        if (range.Status != RangeStatus.Draft)
            throw BusinessRuleException.Conflict("range_not_draft", "Only draft ranges can be deleted. Retire an approved range instead.");
        db.ReferenceRanges.Remove(range);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Approves a draft: retires the range it replaces (if any) and checks that no other approved range
    /// for the parameter covers the same sex and ages, all in one transaction.
    /// </summary>
    public async Task ApproveAsync(int id, CancellationToken ct)
    {
        var range = await db.ReferenceRanges.Include(r => r.Parameter).SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw BusinessRuleException.NotFound("Range");
        if (range.Status != RangeStatus.Draft)
            throw BusinessRuleException.Conflict("range_not_draft", "Only draft ranges can be approved.");
        if (string.IsNullOrWhiteSpace(range.Source))
            throw new BusinessRuleException("range_source_required", "Record the source of this range (e.g. kit insert or guideline) before approving.");
        var error = ReferenceRules.Validate(range, range.Parameter!.ResultType);
        if (error is not null) throw new BusinessRuleException(error, "The range limits are not consistent.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = clock.UtcNow;
        if (range.ReplacesRangeId is int oldId)
        {
            var old = await db.ReferenceRanges.SingleAsync(r => r.Id == oldId, ct);
            if (old.Status == RangeStatus.Approved)
            {
                old.Status = RangeStatus.Retired;
                old.RetiredAtUtc = now;
                old.RetiredByUserId = db.Request.UserId;
            }
        }

        var approved = await db.ReferenceRanges
            .Where(r => r.ParameterId == range.ParameterId && r.Status == RangeStatus.Approved && r.Id != range.ReplacesRangeId)
            .ToListAsync(ct);
        var clash = approved.FirstOrDefault(a => ReferenceRules.Overlaps(a, range));
        if (clash is not null)
            throw BusinessRuleException.Conflict("range_overlap",
                $"An approved range (#{clash.Id}) already covers this sex and age group. Mark the new range as replacing it, or retire it first.");

        range.Status = RangeStatus.Approved;
        range.ApprovedByUserId = db.Request.UserId;
        range.ApprovedAtUtc = now;
        db.AddBusinessAudit("ReferenceRange.Approved", nameof(ReferenceRange), range.Id,
            new { range.ParameterId, range.Sex, range.AgeFromDays, range.AgeToDays, range.Low, range.High, range.CriticalLow, range.CriticalHigh, range.DisplayText, range.Source, range.ReplacesRangeId });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task RetireAsync(int id, string? reason, CancellationToken ct)
    {
        var range = await db.ReferenceRanges.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw BusinessRuleException.NotFound("Range");
        if (range.Status != RangeStatus.Approved)
            throw BusinessRuleException.Conflict("range_not_approved", "Only approved ranges can be retired.");
        range.Status = RangeStatus.Retired;
        range.RetiredAtUtc = clock.UtcNow;
        range.RetiredByUserId = db.Request.UserId;
        db.AddBusinessAudit("ReferenceRange.Retired", nameof(ReferenceRange), range.Id, reason: reason);
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(ReferenceRange r, TestParameter parameter, RangeInput input, CancellationToken ct)
    {
        if (!Enum.IsDefined(input.Sex)) throw new BusinessRuleException("range_sex_invalid", "Choose a sex.");
        r.Sex = input.Sex;
        r.AgeFromDays = input.AgeFromDays;
        r.AgeToDays = input.AgeToDays;
        var numeric = parameter.ResultType == ResultType.Numeric;
        r.Low = numeric ? input.Low : null;
        r.High = numeric ? input.High : null;
        r.CriticalLow = numeric ? input.CriticalLow : null;
        r.CriticalHigh = numeric ? input.CriticalHigh : null;
        r.DisplayText = Blank(input.DisplayText);
        r.Notes = Blank(input.Notes);
        r.Source = Blank(input.Source);

        if (input.ReplacesRangeId is int rid)
        {
            var ok = await db.ReferenceRanges.AnyAsync(x => x.Id == rid && x.ParameterId == r.ParameterId && x.Status == RangeStatus.Approved, ct);
            if (!ok) throw new BusinessRuleException("range_replaces_invalid", "The range to replace must be an approved range of the same parameter.");
        }
        r.ReplacesRangeId = input.ReplacesRangeId;

        var error = ReferenceRules.Validate(r, parameter.ResultType);
        if (error is not null) throw new BusinessRuleException(error, "The range limits are not consistent.");
    }

    private async Task<Dictionary<int, string>> UserNamesAsync(IEnumerable<ReferenceRange> ranges, CancellationToken ct)
    {
        var ids = ranges.SelectMany(r => new[] { r.CreatedByUserId, r.ApprovedByUserId }).OfType<int>().Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);
    }

    private static RangeDto ToDto(ReferenceRange r, Dictionary<int, string> names) => new(
        r.Id, r.ParameterId, r.Sex, r.AgeFromDays, r.AgeToDays, r.Low, r.High, r.CriticalLow, r.CriticalHigh, r.DisplayText,
        r.Notes, r.Source, r.Status, r.ReplacesRangeId,
        r.CreatedByUserId is int c ? names.GetValueOrDefault(c) : null, r.CreatedAtUtc,
        r.ApprovedByUserId is int a ? names.GetValueOrDefault(a) : null, r.ApprovedAtUtc, r.RetiredAtUtc,
        Convert.ToBase64String(r.RowVersion));

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
