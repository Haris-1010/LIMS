using Lims.Api.Security;
using Lims.Application.Common;
using Lims.Application.Reports;
using Lims.Domain.Reports;
using Lims.Domain.Security;
using Lims.Infrastructure.Reports;
using Microsoft.AspNetCore.Mvc;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(ReportService reports, SignatureService signatures) : ControllerBase
{
    /// <summary>A signatory's signature image, as printed on reports.</summary>
    [HttpGet("signatures/{userId:int}")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> Signature(int userId, CancellationToken ct)
    {
        var sig = await signatures.GetAsync(userId, ct);
        if (sig is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, max-age=300";
        return File(sig.Value.Bytes, sig.Value.ContentType);
    }

    [HttpGet]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> List([FromQuery] ReportListQuery query, CancellationToken ct) => Ok(await reports.ListAsync(query, ct));

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await reports.GetAsync(id, ct));

    [HttpPost("{id:long}/printed")]
    [HasPermission(Permissions.ReportsPrint)]
    public async Task<IActionResult> Printed(long id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] PrintedRequest? request, CancellationToken ct) =>
        Ok(new { printCount = await reports.PrintedAsync(id, request, ct) });

    /// <summary>Checks the number and records the share; the browser then opens WhatsApp (nothing is sent from the server).</summary>
    [HttpPost("{id:long}/share")]
    [HasPermission(Permissions.ReportsPrint)]
    public async Task<IActionResult> Share(long id, ShareRequest request, CancellationToken ct) => Ok(await reports.ShareAsync(id, request, ct));

    [HttpGet("{id:long}/verify")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> Verify(long id, CancellationToken ct) => Ok(await reports.VerifyAsync(id, ct));
}

[ApiController]
[Route("api/orders/{orderId:long}/reports")]
public sealed class OrderReportsController(ReportService reports) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> Versions(long orderId, CancellationToken ct) => Ok(await reports.VersionsAsync(orderId, ct));

    [HttpPost("preview")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> Preview(long orderId, PreviewRequest request, CancellationToken ct) => Ok(await reports.PreviewAsync(orderId, request, ct));

    [HttpPost]
    [HasPermission(Permissions.ReportsIssue)]
    public async Task<IActionResult> Issue(long orderId, IssueRequest request, CancellationToken ct) => Ok(await reports.IssueAsync(orderId, request, ct));
}

[ApiController]
[Route("api/report-layouts")]
public sealed class ReportLayoutsController(ReportLayoutService layouts) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await layouts.ListAsync(ct));

    [HttpPost]
    [HasPermission(Permissions.ReportsTemplates)]
    public async Task<IActionResult> Create(ReportLayoutInput input, CancellationToken ct) => Ok(new { id = await layouts.CreateAsync(input, ct) });

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.ReportsTemplates)]
    public async Task<IActionResult> Update(int id, ReportLayoutUpdate input, CancellationToken ct)
    {
        await layouts.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ReportsTemplates)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await layouts.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/report-template")]
public sealed class ReportTemplateController(ReportTemplateService template, ReportSignatoryService signatories) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await template.GetAsync(ct));

    [HttpPut]
    [HasPermission(Permissions.ReportsTemplates)]
    public async Task<IActionResult> Update(TemplateUpdate input, CancellationToken ct)
    {
        await template.UpdateAsync(input, ct);
        return NoContent();
    }

    [HttpGet("logo")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> Logo(CancellationToken ct)
    {
        var logo = await template.LogoAsync(ct);
        if (logo is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, max-age=300";
        return File(logo.Value.Bytes, logo.Value.ContentType);
    }

    [HttpPut("logo")]
    [HasPermission(Permissions.ReportsTemplates)]
    [RequestSizeLimit(ImageSniffer.MaxLogoBytes + 100_000)]
    public async Task<IActionResult> SetLogo(IFormFile file, CancellationToken ct)
    {
        if (file.Length > ImageSniffer.MaxLogoBytes) throw new BusinessRuleException("image_too_large", "The image is too large.", 400, new { maxBytes = ImageSniffer.MaxLogoBytes });
        await using var s = file.OpenReadStream();
        var bytes = new byte[file.Length];
        await s.ReadExactlyAsync(bytes, ct);
        await template.SetLogoAsync(bytes, ct);
        return NoContent();
    }

    [HttpDelete("logo")]
    [HasPermission(Permissions.ReportsTemplates)]
    public async Task<IActionResult> DeleteLogo(CancellationToken ct)
    {
        await template.DeleteLogoAsync(ct);
        return NoContent();
    }

    /// <summary>Full-width header or footer image ("header" or "footer").</summary>
    [HttpGet("images/{kind:regex(^(header|footer)$)}")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> Image(string kind, CancellationToken ct)
    {
        var img = await template.ImageAsync(Kind(kind), ct);
        if (img is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, max-age=300";
        return File(img.Value.Bytes, img.Value.ContentType);
    }

    [HttpPut("images/{kind:regex(^(header|footer)$)}")]
    [HasPermission(Permissions.ReportsTemplates)]
    [RequestSizeLimit(ImageSniffer.MaxBannerBytes + 100_000)]
    public async Task<IActionResult> SetImage(string kind, IFormFile file, CancellationToken ct)
    {
        if (file.Length > ImageSniffer.MaxBannerBytes) throw new BusinessRuleException("image_too_large", "The image is too large.", 400, new { maxBytes = ImageSniffer.MaxBannerBytes });
        await using var s = file.OpenReadStream();
        var bytes = new byte[file.Length];
        await s.ReadExactlyAsync(bytes, ct);
        await template.SetImageAsync(Kind(kind), bytes, ct);
        return NoContent();
    }

    [HttpDelete("images/{kind:regex(^(header|footer)$)}")]
    [HasPermission(Permissions.ReportsTemplates)]
    public async Task<IActionResult> DeleteImage(string kind, CancellationToken ct)
    {
        await template.DeleteImageAsync(Kind(kind), ct);
        return NoContent();
    }

    // ---- signatories printed on every new report

    [HttpGet("signatories")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> Signatories(CancellationToken ct) => Ok(await signatories.ListAsync(ct));

    [HttpPost("signatories")]
    [HasPermission(Permissions.ReportsTemplates)]
    public async Task<IActionResult> AddSignatory(ReportSignatoryInput input, CancellationToken ct) => Ok(new { id = await signatories.CreateAsync(input, ct) });

    [HttpPut("signatories/{id:int}")]
    [HasPermission(Permissions.ReportsTemplates)]
    public async Task<IActionResult> UpdateSignatory(int id, ReportSignatoryUpdate input, CancellationToken ct)
    {
        await signatories.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("signatories/{id:int}")]
    [HasPermission(Permissions.ReportsTemplates)]
    public async Task<IActionResult> RemoveSignatory(int id, CancellationToken ct)
    {
        await signatories.RemoveAsync(id, ct);
        return NoContent();
    }

    [HttpGet("signatories/{id:int}/signature")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> SignatoryImage(int id, CancellationToken ct)
    {
        var img = await signatories.SignatureAsync(id, ct);
        if (img is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, max-age=300";
        return File(img.Value.Bytes, img.Value.ContentType);
    }

    [HttpPut("signatories/{id:int}/signature")]
    [HasPermission(Permissions.ReportsTemplates)]
    [RequestSizeLimit(ImageSniffer.MaxSignatureBytes + 100_000)]
    public async Task<IActionResult> SetSignatoryImage(int id, IFormFile file, CancellationToken ct)
    {
        if (file.Length > ImageSniffer.MaxSignatureBytes) throw new BusinessRuleException("image_too_large", "The image is too large.", 400, new { maxBytes = ImageSniffer.MaxSignatureBytes });
        await using var s = file.OpenReadStream();
        var bytes = new byte[file.Length];
        await s.ReadExactlyAsync(bytes, ct);
        await signatories.SetSignatureAsync(id, bytes, ct);
        return NoContent();
    }

    [HttpDelete("signatories/{id:int}/signature")]
    [HasPermission(Permissions.ReportsTemplates)]
    public async Task<IActionResult> DeleteSignatoryImage(int id, CancellationToken ct)
    {
        await signatories.DeleteSignatureAsync(id, ct);
        return NoContent();
    }

    private static TemplateImage Kind(string kind) => kind == "header" ? TemplateImage.Header : TemplateImage.Footer;
}
