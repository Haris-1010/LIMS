using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Domain.Reports;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Reports;

/// <summary>
/// The signature image printed under a signatory's name. Only PNG or JPEG bytes within the size limit are accepted (checked by
/// content, never by file name or declared type). Users belong to one organisation, so another organisation's signature is never reachable.
/// </summary>
public sealed class SignatureService(LimsDbContext db)
{
    public async Task SetAsync(int userId, byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length == 0) throw new BusinessRuleException("image_type_invalid", "Upload a PNG or JPEG image.");
        if (bytes.Length > ImageSniffer.MaxSignatureBytes) throw new BusinessRuleException("image_too_large", "The image is too large.", 400, new { maxBytes = ImageSniffer.MaxSignatureBytes });
        var type = ImageSniffer.Detect(bytes) ?? throw new BusinessRuleException("image_type_invalid", "Upload a PNG or JPEG image.");
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct) ?? throw BusinessRuleException.NotFound("User");
        user.SignatureImage = bytes;
        user.SignatureContentType = type;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct) ?? throw BusinessRuleException.NotFound("User");
        if (user.SignatureImage is null) return;
        user.SignatureImage = null;
        user.SignatureContentType = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetAsync(int userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => new { x.SignatureImage, x.SignatureContentType }).SingleOrDefaultAsync(ct);
        return u?.SignatureImage is { Length: > 0 } b && u.SignatureContentType is string t ? (b, t) : null;
    }
}
