using Lims.Api.Security;
using Lims.Application.Audit;
using Lims.Application.Dashboard;
using Lims.Application.Settings;
using Lims.Domain.Security;
using Lims.Infrastructure.Audit;
using Lims.Infrastructure.Dashboard;
using Lims.Infrastructure.Settings;
using Microsoft.AspNetCore.Mvc;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <summary>The home page figures. Parts the caller has no permission for come back empty and are flagged in <c>sections</c>.</summary>
    [HttpGet("summary")]
    [HasPermission(Permissions.DashboardView)]
    public async Task<IActionResult> Summary([FromQuery] DashboardQuery query, CancellationToken ct) => Ok(await dashboard.SummaryAsync(query, ct));
}

[ApiController]
[Route("api/audit")]
public sealed class AuditController(AuditService audit) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.AuditView)]
    public async Task<IActionResult> List([FromQuery] AuditQuery query, CancellationToken ct) => Ok(await audit.ListAsync(query, ct));

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.AuditView)]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await audit.GetAsync(id, ct));

    [HttpGet("filters")]
    [HasPermission(Permissions.AuditView)]
    public async Task<IActionResult> Filters(CancellationToken ct) => Ok(await audit.FiltersAsync(ct));
}

[ApiController]
[Route("api/settings")]
public sealed class SettingsController(SettingsService settings) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await settings.ListAsync(ct));

    [HttpPut("{key}")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> Update(string key, SettingUpdate input, CancellationToken ct) => Ok(await settings.UpdateAsync(key, input, ct));
}
