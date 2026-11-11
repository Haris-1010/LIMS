using Lims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/health")]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public sealed class HealthController(LimsDbContext db) : ControllerBase
{
    /// <summary>Reports API status and whether the database is reachable.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dbOk = await db.Database.CanConnectAsync(ct);
        // True when every migration in this build has been applied; false after an update that skipped `db migrate`.
        bool? schemaUpToDate = null;
        if (dbOk)
        {
            try { schemaUpToDate = !(await db.Database.GetPendingMigrationsAsync(ct)).Any(); }
            catch (Exception) { schemaUpToDate = false; }
        }
        var healthy = dbOk && schemaUpToDate == true;
        return Ok(new { status = healthy ? "healthy" : "degraded", database = dbOk, schemaUpToDate, serverTimeUtc = DateTime.UtcNow });
    }
}
