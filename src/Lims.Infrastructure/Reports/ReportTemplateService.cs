using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Reports;
using Lims.Domain.Core;
using Lims.Domain.Reports;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Reports;

/// <summary>The organisation's report letterhead extras: header, footer and disclaimer text in English and Urdu, and the logo.</summary>
public sealed class ReportTemplateService(LimsDbContext db)
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
        return new TemplateDto(Text("en"), Text("ur"), t?.LogoImage is { Length: > 0 }, t?.LogoContentType, t is null ? null : RowVersion.ToToken(t.RowVersion));
    }

    /// <summary>The text frozen into a report when it is issued.</summary>
    public async Task<ReportTemplateText> TextAsync(CancellationToken ct)
    {
        var t = await LoadAsync(false, ct);
        var en = t?.Translations.FirstOrDefault(x => x.Language == "en");
        var ur = t?.Translations.FirstOrDefault(x => x.Language == "ur");
        return new ReportTemplateText(en?.HeaderNote, ur?.HeaderNote, en?.FooterNote, ur?.FooterNote, en?.Disclaimer, ur?.Disclaimer);
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

    public async Task SetLogoAsync(byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length == 0) throw new BusinessRuleException("image_type_invalid", "Upload a PNG or JPEG image.");
        if (bytes.Length > ImageSniffer.MaxLogoBytes) throw new BusinessRuleException("image_too_large", "The image is too large.", 400, new { maxBytes = ImageSniffer.MaxLogoBytes });
        var type = ImageSniffer.Detect(bytes) ?? throw new BusinessRuleException("image_type_invalid", "Upload a PNG or JPEG image.");
        var t = await LoadAsync(true, ct);
        if (t is null) { t = new ReportTemplate { OrganizationId = OrgId }; db.ReportTemplates.Add(t); }
        t.LogoImage = bytes;
        t.LogoContentType = type;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteLogoAsync(CancellationToken ct)
    {
        var t = await LoadAsync(true, ct);
        if (t?.LogoImage is null) return;
        t.LogoImage = null;
        t.LogoContentType = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Bytes, string ContentType)?> LogoAsync(CancellationToken ct)
    {
        var t = await db.ReportTemplates.AsNoTracking().Select(x => new { x.LogoImage, x.LogoContentType }).SingleOrDefaultAsync(ct);
        return t?.LogoImage is { Length: > 0 } b && t.LogoContentType is string ct2 ? (b, ct2) : null;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
