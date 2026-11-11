using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Reports;
using Lims.Domain.Core;
using Lims.Domain.Reports;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Reports;

/// <summary>
/// Signatories printed on every new report from the report template (see <see cref="ReportSignatory"/>). At most
/// <see cref="MaxActive"/> at a time. Removing one hides it from new reports; issued reports keep it and its signature image.
/// </summary>
public sealed class ReportSignatoryService(LimsDbContext db)
{
    public const int MaxActive = 4;
    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");

    public async Task<List<ReportSignatoryDto>> ListAsync(CancellationToken ct) =>
        (await db.ReportSignatories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync(ct)).Select(ToDto).ToList();

    /// <summary>What is copied into a report when it is issued.</summary>
    public async Task<List<SnapFixedSignatory>> ForSnapshotAsync(CancellationToken ct) =>
        await db.ReportSignatories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id)
            .Select(x => new SnapFixedSignatory(x.Id, x.NameEn, x.NameUr, x.Qualification, x.Designation, x.RegistrationNo, x.SignatureImage != null))
            .ToListAsync(ct);

    public async Task<int> CreateAsync(ReportSignatoryInput input, CancellationToken ct)
    {
        if (await db.ReportSignatories.CountAsync(x => x.IsActive, ct) >= MaxActive)
            throw new BusinessRuleException("report_signatories_full", $"A report can carry at most {MaxActive} template signatories.", 409, new { max = MaxActive });
        var s = new ReportSignatory { OrganizationId = OrgId };
        Apply(s, input);
        db.ReportSignatories.Add(s);
        await db.SaveChangesAsync(ct);
        return s.Id;
    }

    public async Task UpdateAsync(int id, ReportSignatoryUpdate input, CancellationToken ct)
    {
        var s = await LoadAsync(id, ct);
        db.Entry(s).Property(x => x.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        Apply(s, input);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(int id, CancellationToken ct)
    {
        var s = await LoadAsync(id, ct);
        s.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetSignatureAsync(int id, byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length == 0) throw new BusinessRuleException("image_type_invalid", "Upload a PNG or JPEG image.");
        if (bytes.Length > ImageSniffer.MaxSignatureBytes) throw new BusinessRuleException("image_too_large", "The image is too large.", 400, new { maxBytes = ImageSniffer.MaxSignatureBytes });
        var type = ImageSniffer.Detect(bytes) ?? throw new BusinessRuleException("image_type_invalid", "Upload a PNG or JPEG image.");
        var s = await LoadAsync(id, ct);
        s.SignatureImage = bytes;
        s.SignatureContentType = type;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteSignatureAsync(int id, CancellationToken ct)
    {
        var s = await LoadAsync(id, ct);
        if (s.SignatureImage is null) return;
        s.SignatureImage = null;
        s.SignatureContentType = null;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The signature image, also of a removed signatory (issued reports still show it).</summary>
    public async Task<(byte[] Bytes, string ContentType)?> SignatureAsync(int id, CancellationToken ct)
    {
        var s = await db.ReportSignatories.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.SignatureImage, x.SignatureContentType }).SingleOrDefaultAsync(ct);
        return s?.SignatureImage is { Length: > 0 } b && s.SignatureContentType is string t ? (b, t) : null;
    }

    private async Task<ReportSignatory> LoadAsync(int id, CancellationToken ct) =>
        await db.ReportSignatories.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct) ?? throw BusinessRuleException.NotFound("Report signatory");

    private static void Apply(ReportSignatory s, ReportSignatoryInput input)
    {
        if (string.IsNullOrWhiteSpace(input.NameEn)) throw new BusinessRuleException("report_signatory_name_required", "Enter the signatory's name.");
        s.NameEn = input.NameEn.Trim();
        s.NameUr = Blank(input.NameUr);
        s.Qualification = Blank(input.Qualification);
        s.Designation = Blank(input.Designation);
        s.RegistrationNo = Blank(input.RegistrationNo);
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static ReportSignatoryDto ToDto(ReportSignatory x) =>
        new(x.Id, x.NameEn, x.NameUr, x.Qualification, x.Designation, x.RegistrationNo, x.SignatureImage is { Length: > 0 }, RowVersion.ToToken(x.RowVersion));
}
