using Lims.Api.Security;
using Lims.Application.Results;
using Lims.Domain.Security;
using Lims.Infrastructure.Results;
using Microsoft.AspNetCore.Mvc;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/results")]
public sealed class ResultsController(ResultService results) : ControllerBase
{
    [HttpGet("pending")]
    [HasPermission(Permissions.ResultsEnter)]
    public async Task<IActionResult> Pending([FromQuery] PendingQuery query, CancellationToken ct) => Ok(await results.PendingAsync(query, ct));

    [HttpGet("queue")]
    [HasPermission(Permissions.ResultsVerify, Permissions.ResultsApprove)]
    public async Task<IActionResult> Queue([FromQuery] QueueQuery query, CancellationToken ct) => Ok(await results.QueueAsync(query, ct));

    [HttpGet("items/{orderItemId:long}")]
    [HasPermission(Permissions.ResultsEnter, Permissions.ResultsView, Permissions.ResultsVerify, Permissions.ResultsApprove)]
    public async Task<IActionResult> Entry(long orderItemId, CancellationToken ct) => Ok(await results.GetEntryAsync(orderItemId, ct));

    [HttpPut("items/{orderItemId:long}/draft")]
    [HasPermission(Permissions.ResultsEnter)]
    public async Task<IActionResult> SaveDraft(long orderItemId, SaveDraftRequest request, CancellationToken ct) => Ok(await results.SaveDraftAsync(orderItemId, request, ct));

    [HttpDelete("items/{orderItemId:long}/draft")]
    [HasPermission(Permissions.ResultsEnter)]
    public async Task<IActionResult> Discard(long orderItemId, CancellationToken ct)
    {
        await results.DiscardDraftAsync(orderItemId, ct);
        return NoContent();
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.ResultsView, Permissions.ResultsEnter, Permissions.ResultsVerify, Permissions.ResultsApprove)]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await results.GetByResultAsync(id, ct));

    [HttpGet("{id:long}/history")]
    [HasPermission(Permissions.ResultsView, Permissions.ResultsEnter, Permissions.ResultsVerify, Permissions.ResultsApprove)]
    public async Task<IActionResult> History(long id, CancellationToken ct) => Ok(await results.HistoryAsync(id, ct));

    [HttpPost("{id:long}/submit")]
    [HasPermission(Permissions.ResultsEnter)]
    public async Task<IActionResult> Submit(long id, SubmitRequest request, CancellationToken ct) => Ok(await results.SubmitAsync(id, request, ct));

    [HttpPost("{id:long}/return")]
    [HasPermission(Permissions.ResultsVerify, Permissions.ResultsApprove)]
    public async Task<IActionResult> Return(long id, ReasonRequest request, CancellationToken ct) => Ok(await results.ReturnAsync(id, request, ct));

    [HttpPost("{id:long}/verify")]
    [HasPermission(Permissions.ResultsVerify)]
    public async Task<IActionResult> Verify(long id, StepRequest request, CancellationToken ct) => Ok(await results.VerifyAsync(id, request, ct));

    [HttpPost("{id:long}/approve")]
    [HasPermission(Permissions.ResultsApprove)]
    public async Task<IActionResult> Approve(long id, StepRequest request, CancellationToken ct) => Ok(await results.ApproveAsync(id, request, ct));

    [HttpPost("{id:long}/amend")]
    [HasPermission(Permissions.ResultsAmend)]
    public async Task<IActionResult> Amend(long id, ReasonRequest request, CancellationToken ct) => Ok(await results.AmendAsync(id, request, ct));
}

[ApiController]
[Route("api/orders/{orderId:long}/results")]
public sealed class OrderResultsController(ResultService results) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ResultsView)]
    [HasPermission(Permissions.OrdersView)]
    public async Task<IActionResult> Get(long orderId, CancellationToken ct) => Ok(await results.ForOrderAsync(orderId, ct));
}

[ApiController]
[Route("api/worklists")]
public sealed class WorklistsController(WorklistService worklists) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ResultsEnter)]
    public async Task<IActionResult> List([FromQuery] WorklistQuery query, CancellationToken ct) => Ok(await worklists.ListAsync(query, ct));

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.ResultsEnter)]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await worklists.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.ResultsEnter)]
    public async Task<IActionResult> Create(WorklistCreate input, CancellationToken ct) => Ok(await worklists.CreateAsync(input, ct));

    [HttpPost("{id:long}/close")]
    [HasPermission(Permissions.ResultsEnter)]
    public async Task<IActionResult> Close(long id, CancellationToken ct) => Ok(await worklists.CloseAsync(id, ct));
}

[ApiController]
[Route("api/critical-alerts")]
public sealed class CriticalAlertsController(CriticalAlertService alerts) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ResultsCritical)]
    public async Task<IActionResult> List([FromQuery] AlertQuery query, CancellationToken ct) => Ok(await alerts.ListAsync(query, ct));

    [HttpGet("open-count")]
    [HasPermission(Permissions.ResultsCritical)]
    public async Task<IActionResult> OpenCount(CancellationToken ct) => Ok(new { count = await alerts.OpenCountAsync(ct) });

    [HttpPost("{id:long}/notify")]
    [HasPermission(Permissions.ResultsCritical)]
    public async Task<IActionResult> Notify(long id, NotifyRequest request, CancellationToken ct) => Ok(await alerts.NotifyAsync(id, request, ct));
}
