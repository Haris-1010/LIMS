using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Reports;
using Lims.Domain.Core;
using Lims.Domain.Reports;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Reports;

/// <summary>The organisation's report letterhead extras: header, footer and disclaimer text in English and Urdu, the logo, and the
/// optional full-width header and footer images.</summary>
public sealed class ReportTemplateService(LimsDbContext db, ReportSignatoryService signatories)
{
    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");

    private Task<ReportTemplate?> LoadAsync(bool track, CancellationToken ct)
    {
        var q = db.ReportTemplates.Include(t => t.Translations).AsQueryable();
        return (track ? q : q.AsNoTracking()).SingleOrDefaultAsync(ct);
    }

    public async Task<TemplateDto> GetAsync(CancellationToken ct)
    {
        var t = await LoadAsync(false, ct);
        TemplateTextInput Text(string lang)
        {
            var x = t?.Translations.FirstOrDefault(tr => tr.Language == lang);
            return new TemplateTextInput { HeaderNote = x?.HeaderNote, FooterNote = x?.FooterNote, Disclaimer = x?.Disclaimer };
        }
        return new TemplateDto(Text("en"), Text("ur"), t?.LogoImage is { Length: > 0 }, t?.LogoContentType, t is null ? null : RowVersion.ToToken(t.RowVersion),
            t?.HeaderImage is { Length: > 0 }, t?.FooterImage is { Length: > 0 });
    }

    /// <summary>The text and the template signatories frozen into a report when it is issued.</summary>
    public async Task<ReportTemplateText> TextAsync(CancellationToken ct)
    {
        var t = await LoadAsync(false, ct);
        var en = t?.Translations.FirstOrDefault(x => x.Language == "en");
        var ur = t?.Translations.FirstOrDefault(x => x.Language == "ur");
        return new ReportTemplateText(en?.HeaderNote, ur?.HeaderNote, en?.FooterNote, ur?.FooterNote, en?.Disclaimer, ur?.Disclaimer,
            await signatories.ForSnapshotAsync(ct));
    }

    public async Task UpdateAsync(TemplateUpdate input, CancellationToken ct)
    {
        var t = await LoadAsync(true, ct);
        if (t is null)
        {
            t = new ReportTemplate { OrganizationId = OrgId };
            db.ReportTemplates.Add(t);
        }
        else
        {
            db.Entry(t).Property(x => x.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
            db.Entry(t).Property(x => x.LogoContentType).IsModified = true;   // forces the version check even when only a translation changes
        }
        foreach (var (lang, text) in new[] { ("en", input.En), ("ur", input.Ur) })
        {
            var tr = t.Translations.FirstOrDefault(x => x.Language == lang);
            if (tr is null) { tr = new ReportTemplateTranslation { OrganizationId = t.OrganizationId, Language = lang }; t.Translations.Add(tr); }
            tr.HeaderNote = Blank(text.HeaderNote);
            tr.FooterNote = Blank(text.FooterNote);
            tr.Disclaimer = Blank(text.Disclaimer);
        }
        await db.SaveChangesAsync(ct);
    }

    public Task SetLogoAsync(byte[] bytes, CancellationToken ct) => SetImageAsync(TemplateImage.Logo, bytes, ct);
    public Task DeleteLogoAsync(CancellationToken ct) => DeleteImageAsync(TemplateImage.Logo, ct);
    public Task<(byte[] Bytes, string ContentType)?> LogoAsync(CancellationToken ct) => ImageAsync(TemplateImage.Logo, ct);

    public static int MaxBytes(TemplateImage kind) => kind == TemplateImage.Logo ? ImageSniffer.MaxLogoBytes : ImageSniffer.MaxBannerBytes;

    public async Task SetImageAsync(TemplateImage kind, byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length == 0) throw new BusinessRuleException("image_type_invalid", "Upload a PNG or JPEG image.");
        if (bytes.Length > MaxBytes(kind)) throw new BusinessRuleException("image_too_large", "The image is too large.", 400, new { maxBytes = MaxBytes(kind) });
        var type = ImageSniffer.Detect(bytes) ?? throw new BusinessRuleException("image_type_invalid", "Upload a PNG or JPEG image.");
        var t = await LoadAsync(true, ct);
        if (t is null) { t = new ReportTemplate { OrganizationId = OrgId }; db.ReportTemplates.Add(t); }
        Put(t, kind, bytes, type);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteImageAsync(TemplateImage kind, CancellationToken ct)
    {
        var t = await LoadAsync(true, ct);
        if (t is null || Get(t, kind).Bytes is null) return;
        Put(t, kind, null, null);
        await db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Bytes, string ContentType)?> ImageAsync(TemplateImage kind, CancellationToken ct)
    {
        var q = db.ReportTemplates.AsNoTracking();
        var t = kind switch
        {
            TemplateImage.Header => await q.Select(x => new { B = x.HeaderImage, T = x.HeaderImageContentType }).SingleOrDefaultAsync(ct),
            TemplateImage.Footer => await q.Select(x => new { B = x.FooterImage, T = x.FooterImageContentType }).SingleOrDefaultAsync(ct),
            _ => await q.Select(x => new { B = x.LogoImage, T = x.LogoContentType }).SingleOrDefaultAsync(ct),
        };
        return t?.B is { Length: > 0 } b && t.T is string type ? (b, type) : null;
    }

    private static (byte[]? Bytes, string? Type) Get(ReportTemplate t, TemplateImage kind) => kind switch
    {
        TemplateImage.Header => (t.HeaderImage, t.HeaderImageContentType),
        TemplateImage.Footer => (t.FooterImage, t.FooterImageContentType),
        _ => (t.LogoImage, t.LogoContentType),
    };

    private static void Put(ReportTemplate t, TemplateImage kind, byte[]? bytes, string? type)
    {
        switch (kind)
        {
            case TemplateImage.Header: t.HeaderImage = bytes; t.HeaderImageContentType = type; break;
            case TemplateImage.Footer: t.FooterImage = bytes; t.FooterImageContentType = type; break;
            default: t.LogoImage = bytes; t.LogoContentType = type; break;
        }
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
