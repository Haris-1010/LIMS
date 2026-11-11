namespace Lims.Application.Licensing;

/// <summary>
/// Licence state of this server, verified from the signed offline file. <see cref="Reason"/> is a short stable code the
/// activation screen translates (ok, not_enforced, missing, identity, folder, too_large, format, signature, unsupported,
/// product, server, issued, expired, save_failed, no_file); <see cref="Message"/> is the same in plain English.
/// </summary>
public sealed record LicenseStatus(
    bool IsValid,
    string Reason,
    string InstallationId,
    string? CustomerName,
    string? LicenseId,
    DateTime? ExpiresOnUtc,
    string Message);
