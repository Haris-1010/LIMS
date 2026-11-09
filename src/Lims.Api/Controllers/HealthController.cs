using Lims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

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
        return Ok(new { status = dbOk ? "healthy" : "degraded", database = dbOk, serverTimeUtc = DateTime.UtcNow });
    }
}
