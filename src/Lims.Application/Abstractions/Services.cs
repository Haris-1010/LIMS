namespace Lims.Application.Abstractions;

/// <summary>Laboratory clock: UTC for storage, local (configured time zone) for display and numbering.</summary>
public interface ILabClock
{
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
    DateTime ToLocal(DateTime utc);
}

/// <summary>Issues document numbers (patient, order, invoice…) atomically in the database.</summary>
public interface INumberSequenceService
{
    /// <summary>
    /// Returns the next number for the caller's organisation. A branch-specific sequence is used if one
    /// exists for <paramref name="branchId"/>, otherwise the organisation-wide one. When called inside a
    /// transaction the number is only consumed if the transaction commits.
    /// </summary>
    Task<string> NextAsync(string sequenceType, int? branchId = null, CancellationToken ct = default);
}

public sealed record OrganizationSetup(
    string Code,
    string NameEn,
    string? NameUr,
    string MainBranchCode,
    string MainBranchNameEn,
    string? MainBranchNameUr);

/// <summary>Creates a new organisation with its main branch, default departments, roles, sequences and settings.</summary>
public interface IOrganizationProvisioner
{
    /// <returns>The new organisation's id.</returns>
    Task<int> ProvisionAsync(OrganizationSetup setup, CancellationToken ct = default);
}

/// <summary>Raised when code tries to read or write another organisation's data.</summary>
public sealed class TenantViolationException(string message) : InvalidOperationException(message);
