using Lims.Application.Abstractions;
using Lims.Domain.Core;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Services;

/// <summary>
/// Issues numbers with a single atomic UPDATE … OUTPUT, so two terminals can never receive the same
/// number. The row lock is held until the surrounding transaction ends: if an order fails and rolls
/// back, its number is returned and the sequence has no gap.
/// </summary>
public sealed class NumberSequenceService(LimsDbContext db, ILabClock clock) : INumberSequenceService
{
    public async Task<string> NextAsync(string sequenceType, int? branchId = null, CancellationToken ct = default)
    {
        var orgId = db.Request.OrganizationId
            ?? throw new TenantViolationException("Number sequences require an organisation in the request context.");

        // Explicit organisation check as well as the tenant filter (system contexts bypass the filter).
        // A branch-specific sequence wins over the organisation-wide one (NULLs sort first, so descending).
        var seq = await Find(orgId, sequenceType, branchId, ct);
        if (seq is null)
        {
            // A sequence type introduced after this organisation was set up: create it with defaults.
            if (!SequenceTypes.Defaults.TryGetValue(sequenceType, out var d))
                throw new InvalidOperationException($"No number sequence '{sequenceType}' is configured.");
            await db.Database.ExecuteSqlAsync($"""
                IF NOT EXISTS (SELECT 1 FROM core.NumberSequences WHERE OrganizationId = {orgId} AND BranchId IS NULL AND SequenceType = {sequenceType})
                INSERT INTO core.NumberSequences (OrganizationId, BranchId, SequenceType, Prefix, Padding, ResetPolicy, CurrentPeriod, NextValue, CreatedAtUtc)
                VALUES ({orgId}, NULL, {sequenceType}, {d.Prefix}, {d.Padding}, {d.Reset.ToString()}, '', 1, {clock.UtcNow})
                """, ct);
            seq = await Find(orgId, sequenceType, branchId, ct)
                  ?? throw new InvalidOperationException($"No number sequence '{sequenceType}' is configured.");
        }

        var period = NumberSequence.PeriodFor(seq.ResetPolicy, clock.LocalNow);

        var issued = await db.Database.SqlQuery<long>($"""
            UPDATE core.NumberSequences
               SET NextValue = CASE WHEN CurrentPeriod = {period} THEN NextValue + 1 ELSE 2 END,
                   CurrentPeriod = {period}
            OUTPUT CASE WHEN deleted.CurrentPeriod = {period} THEN deleted.NextValue ELSE 1 END AS [Value]
             WHERE Id = {seq.Id}
            """).ToListAsync(ct);

        return NumberSequence.Format(seq.Prefix, period, issued.Single(), seq.Padding);
    }

    private sealed record SeqInfo(int Id, string Prefix, int Padding, SequenceResetPolicy ResetPolicy);

    private Task<SeqInfo?> Find(int orgId, string type, int? branchId, CancellationToken ct) =>
        db.NumberSequences.AsNoTracking()
            .Where(s => s.OrganizationId == orgId && s.SequenceType == type && (s.BranchId == branchId || s.BranchId == null))
            .OrderByDescending(s => s.BranchId)
            .Select(s => new SeqInfo(s.Id, s.Prefix, s.Padding, s.ResetPolicy))
            .FirstOrDefaultAsync(ct);
}
