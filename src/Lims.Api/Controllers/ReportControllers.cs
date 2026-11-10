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
    public async Task<IActionResult> Printed(long id, CancellationToken ct) => Ok(new { printCount = await reports.PrintedAsync(id, ct) });

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
[Route("api/report-template")]
public sealed class ReportTemplateController(ReportTemplateService template) : ControllerBase
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
}
