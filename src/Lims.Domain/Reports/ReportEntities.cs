using System.Security.Cryptography;
using System.Text;
using Lims.Domain.Common;

namespace Lims.Domain.Reports;

public enum ReportStatus
{
    /// <summary>Some tests are not released yet; the report lists them as pending.</summary>
    Interim = 0,
    /// <summary>Every non-cancelled test of the order is released.</summary>
    Final = 1,
    /// <summary>Replaced by a later version. Kept and viewable, always printed with a SUPERSEDED mark.</summary>
    Superseded = 90,
}

public enum ReportLanguage { English = 0, Urdu = 1, Both = 2 }

/// <summary>
/// An issued report: one version of the report of one order. The content is a frozen snapshot of what was released at issue
/// time, so catalogue, patient or range changes made later can never alter an issued report. Not branch-filtered by the global
/// query filter (collection centres must see their patients' reports); visibility is applied explicitly in ReportService.
/// </summary>
public class LabReport : AuditableEntity<long>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public long OrderId { get; set; }
    public int PatientId { get; set; }
    /// <summary>Branch that registered the order.</summary>
    public int OrderBranchId { get; set; }
    /// <summary>Branch whose laboratory processed the tests.</summary>
    public int LabBranchId { get; set; }
    /// <summary>Shared by every version of the order's report, e.g. REP-2611-0001.</summary>
    public string ReportNo { get; set; } = "";
    public int Version { get; set; } = 1;
    public ReportStatus Status { get; set; }
    public ReportLanguage Language { get; set; }
    public int IssuedByUserId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public string? AmendmentReason { get; set; }
    public long? ReplacesReportId { get; set; }

    /// <summary>What the report contained when it was issued (tests that were released, and when the last one was).</summary>
    public int ReleasedCount { get; set; }
    public int ActiveTestCount { get; set; }
    public DateTime? LatestApprovedAtUtc { get; set; }

    /// <summary>The frozen content. Large and patient-identifying, so it is kept out of the audit log.</summary>
    [Sensitive] public string SnapshotJson { get; set; } = "";
    /// <summary>SHA-256 of <see cref="SnapshotJson"/> (lower-case hex).</summary>
    public string SnapshotHash { get; set; } = "";

    public int PrintCount { get; set; }
    public DateTime? LastPrintedAtUtc { get; set; }
}

/// <summary>Letterhead extras for the organisation's reports (names and addresses come from the organisation and branch).</summary>
public class ReportTemplate : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    [Sensitive] public byte[]? LogoImage { get; set; }
    public string? LogoContentType { get; set; }
    public ICollection<ReportTemplateTranslation> Translations { get; } = [];
}

public class ReportTemplateTranslation : ITenantEntity
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int TemplateId { get; set; }
    /// <summary>"en" or "ur".</summary>
    public string Language { get; set; } = "en";
    public string? HeaderNote { get; set; }
    public string? FooterNote { get; set; }
    public string? Disclaimer { get; set; }
}

/// <summary>What is released for an order right now, compared with what an issued report contained.</summary>
public readonly record struct ReleaseState(int ReleasedCount, int ActiveTestCount, DateTime? LatestApprovedAtUtc);

public static class ReportRules
{
    /// <summary>A report can be issued as soon as one test is released.</summary>
    public static bool CanIssue(ReleaseState s) => s.ReleasedCount > 0;

    /// <summary>Final only when every non-cancelled test is released.</summary>
    public static ReportStatus StatusFor(ReleaseState s) => s.ReleasedCount > 0 && s.ReleasedCount >= s.ActiveTestCount ? ReportStatus.Final : ReportStatus.Interim;

    /// <summary>A newly released test, an amendment, or a cancelled test makes an issued report outdated.</summary>
    public static bool IsOutdated(LabReport issued, ReleaseState now) =>
        issued.ReleasedCount != now.ReleasedCount || issued.ActiveTestCount != now.ActiveTestCount || issued.LatestApprovedAtUtc != now.LatestApprovedAtUtc;

    /// <summary>The first print is the original; every later print is marked as a duplicate copy.</summary>
    public static bool IsDuplicate(int printCountBeforeThisPrint) => printCountBeforeThisPrint > 0;

    public static string Fingerprint(string snapshotJson) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson))).ToLowerInvariant();

    /// <summary>The short form printed on the report.</summary>
    public static string ShortFingerprint(string hash) => hash.Length >= 12 ? hash[..12].ToUpperInvariant() : hash.ToUpperInvariant();

    public static bool Verify(string snapshotJson, string storedHash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Fingerprint(snapshotJson)), Encoding.ASCII.GetBytes(storedHash.ToLowerInvariant()));

    // ---- allowed moves
    public static bool CanSupersede(ReportStatus s) => s is ReportStatus.Interim or ReportStatus.Final;
}

/// <summary>Recognises the image types allowed for logos and signatures by their first bytes, never by file name or declared type.</summary>
public static class ImageSniffer
{
    public const int MaxLogoBytes = 300 * 1024;
    public const int MaxSignatureBytes = 200 * 1024;

    /// <summary>"image/png", "image/jpeg", or null when the bytes are anything else (SVG, GIF, HTML, executables…).</summary>
    public static string? Detect(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        return null;
    }
}
