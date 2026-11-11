using Lims.Api.Security;
using Lims.Application.Audit;
using Lims.Application.Backup;
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

/// <summary>
/// Backups (Phase 13): status, the automatic schedule, and a manual full backup. The backups themselves are made by the
/// application (BackupScheduler / BackupEngine). Restores are deliberately not offered here (docs/BACKUP_RESTORE.md).
/// </summary>
[ApiController]
[Route("api/backup")]
public sealed class BackupController(
    Lims.Infrastructure.Backup.BackupStatusService backups,
    Lims.Infrastructure.Backup.BackupSetupService setup) : ControllerBase
{
    [HttpGet("status")]
    [HasPermission(Permissions.BackupManage)]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await backups.GetAsync(ct));

    [HttpGet("settings")]
    [HasPermission(Permissions.BackupManage)]
    public IActionResult Settings() => Ok(setup.Get());

    [HttpPut("settings")]
    [HasPermission(Permissions.BackupManage)]
    public async Task<IActionResult> SaveSettings(BackupSettingsRequest request, CancellationToken ct) =>
        Ok(await setup.SaveAsync(request, ct));

    [HttpGet("folders")]
    [HasPermission(Permissions.BackupManage)]
    public IActionResult Folders([FromQuery] string? path) => Ok(setup.BrowseFolders(path));

    [HttpPost("run")]
    [HasPermission(Permissions.BackupManage)]
    public async Task<IActionResult> Run(CancellationToken ct) => Ok(await setup.RunFullAsync(ct));
}
