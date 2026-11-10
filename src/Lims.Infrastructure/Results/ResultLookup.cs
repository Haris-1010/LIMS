using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Results;

/// <summary>
/// Existence checks other modules need without reading any result data (e.g. a sample with saved results cannot be rejected).
/// Kept separate from <see cref="ResultService"/> so the sample service does not depend on it (which itself uses samples).
/// </summary>
public sealed class ResultLookup(LimsDbContext db)
{
    public Task<bool> AnyForItemsAsync(IReadOnlyCollection<long> orderItemIds, CancellationToken ct) =>
        db.TestResults.AnyAsync(r => orderItemIds.Contains(r.OrderItemId), ct); // VISIBILITY-GATE (existence only, nothing is returned)
}
