using System.ComponentModel.DataAnnotations;
using Lims.Application.Common;
using Lims.Domain.Reports;

namespace Lims.Application.Reports;

public sealed record ReportListQuery : PageQuery
{
    /// <summary>"ready" = orders with released results that need a report (none yet, or outdated); "issued" = current issued reports.</summary>
    public string Tab { get; init; } = "ready";
    /// <summary>How far back to look for newly released results ("ready" tab), 1–365 days.</summary>
    [Range(1, 365)] public int Days { get; init; } = 60;
}

public sealed record PreviewRequest { public ReportLanguage Language { get; init; } }

public sealed record IssueRequest
{
    public ReportLanguage Language { get; init; }
    /// <summary>Required for every version after the first (why a new version is being issued).</summary>
    [MaxLength(500)] public string? Reason { get; init; }
}

public sealed record ReportListItem(
    long OrderId, string OrderNo, int PatientId, string PatientNo, string PatientName, string? PatientNameUr, string BranchName,
    int ReleasedCount, int ActiveTestCount, DateTime? LatestApprovedAtUtc,
    long? ReportId, string? ReportNo, int? Version, ReportStatus? Status, ReportLanguage? Language, DateTime? IssuedAtUtc, bool IsOutdated, int PrintCount);

public sealed record ReportMeta(
    long? Id, string ReportNo, int Version, ReportStatus Status, ReportLanguage Language, DateTime IssuedAtUtc, string IssuedByName,
    int PrintCount, bool IsDuplicate, bool IsOutdated, string Fingerprint, int? SupersededByVersion);

public sealed record ReportView(ReportMeta Meta, ReportSnapshot Snapshot, bool IsPreview);

public sealed record ReportVersionDto(
    long Id, string ReportNo, int Version, ReportStatus Status, ReportLanguage Language, DateTime IssuedAtUtc, string IssuedByName,
    string? AmendmentReason, int PrintCount, bool IsOutdated);

public sealed record VerifyResult(bool Intact, string Fingerprint);

public sealed record TemplateTextInput
{
    [MaxLength(500)] public string? HeaderNote { get; init; }
    [MaxLength(500)] public string? FooterNote { get; init; }
    [MaxLength(1000)] public string? Disclaimer { get; init; }
}

public sealed record TemplateUpdate
{
    public string? RowVersion { get; init; }
    [Required] public TemplateTextInput En { get; init; } = new();
    [Required] public TemplateTextInput Ur { get; init; } = new();
}

public sealed record TemplateDto(TemplateTextInput En, TemplateTextInput Ur, bool HasLogo, string? LogoContentType, string? RowVersion,
    bool HasHeaderImage = false, bool HasFooterImage = false);

/// <summary>A signatory printed on every new report from the template.</summary>
public sealed record ReportSignatoryDto(int Id, string NameEn, string? NameUr, string? Qualification, string? Designation, string? RegistrationNo,
    bool HasSignature, string RowVersion);

public record ReportSignatoryInput
{
    [Required, MaxLength(150)] public string NameEn { get; init; } = "";
    [MaxLength(150)] public string? NameUr { get; init; }
    [MaxLength(200)] public string? Qualification { get; init; }
    [MaxLength(150)] public string? Designation { get; init; }
    [MaxLength(50)] public string? RegistrationNo { get; init; }
}

public sealed record ReportSignatoryUpdate : ReportSignatoryInput
{
    [Required] public string RowVersion { get; init; } = "";
}

/// <summary>The three images of the report template.</summary>
public enum TemplateImage { Logo, Header, Footer }
