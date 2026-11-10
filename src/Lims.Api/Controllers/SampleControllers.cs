using Lims.Api.Security;
using Lims.Application.Samples;
using Lims.Domain.Security;
using Lims.Infrastructure.Samples;
using Microsoft.AspNetCore.Mvc;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/samples")]
public sealed class SamplesController(SampleService samples) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.SamplesView)]
    public async Task<IActionResult> List([FromQuery] SampleQuery query, CancellationToken ct) => Ok(await samples.ListAsync(query, ct));

    /// <summary>Planned samples waiting at the working branch (urgent first).</summary>
    [HttpGet("collection-queue")]
    [HasPermission(Permissions.SamplesCollect)]
    public async Task<IActionResult> Queue(CancellationToken ct) => Ok(await samples.CollectionQueueAsync(ct));

    /// <summary>Scanner lookup. Unknown codes and other organisations' codes both return 404.</summary>
    [HttpGet("by-barcode/{barcode}")]
    [HasPermission(Permissions.SamplesView)]
    public async Task<IActionResult> ByBarcode(string barcode, CancellationToken ct) => Ok(await samples.GetByBarcodeAsync(barcode, ct));

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.SamplesView)]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await samples.GetAsync(id, ct));

    [HttpPost("{id:long}/collect")]
    [HasPermission(Permissions.SamplesCollect)]
    public async Task<IActionResult> Collect(long id, CollectRequest request, CancellationToken ct) => Ok(await samples.CollectAsync(id, request, ct));

    [HttpPost("{id:long}/receive")]
    [HasPermission(Permissions.SamplesReceive)]
    public async Task<IActionResult> Receive(long id, CancellationToken ct) => Ok(await samples.ReceiveAsync(id, ct));

    [HttpPost("{id:long}/reject")]
    [HasPermission(Permissions.SamplesReject)]
    public async Task<IActionResult> Reject(long id, RejectRequest request, CancellationToken ct) => Ok(await samples.RejectAsync(id, request, ct));

    [HttpPost("{id:long}/recollect")]
    [HasPermission(Permissions.SamplesReject, Permissions.SamplesCollect)]
    public async Task<IActionResult> Recollect(long id, CancellationToken ct) => Ok(await samples.RecollectAsync(id, ct));

    [HttpPost("{id:long}/labels/printed")]
    [HasPermission(Permissions.SamplesCollect, Permissions.SamplesReceive)]
    public async Task<IActionResult> LabelPrinted(long id, LabelPrintedRequest request, CancellationToken ct)
    {
        await samples.RecordLabelPrintedAsync(id, request, ct);
        return NoContent();
    }

    // ---- rejection reasons
    [HttpGet("rejection-reasons")]
    [HasPermission(Permissions.SamplesView)]
    public async Task<IActionResult> Reasons([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await samples.ReasonsAsync(includeInactive && User.HasClaim(LimsClaims.Permission, Permissions.CatalogManage), ct));

    [HttpPost("rejection-reasons")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> CreateReason(ReasonInput input, CancellationToken ct) => Ok(new { id = await samples.CreateReasonAsync(input, ct) });

    [HttpPut("rejection-reasons/{id:int}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> UpdateReason(int id, ReasonUpdate input, CancellationToken ct)
    {
        await samples.UpdateReasonAsync(id, input, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/orders/{orderId:long}/samples")]
public sealed class OrderSamplesController(SampleService samples) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.SamplesView)]
    [HasPermission(Permissions.OrdersView)]
    public async Task<IActionResult> ForOrder(long orderId, CancellationToken ct) => Ok(await samples.ForOrderAsync(orderId, ct));

    /// <summary>Collect every planned sample of the order at the working branch.</summary>
    [HttpPost("collect")]
    [HasPermission(Permissions.SamplesCollect)]
    public async Task<IActionResult> CollectAll(long orderId, CollectRequest request, CancellationToken ct) =>
        Ok(await samples.CollectOrderAsync(orderId, request, ct));
}

[ApiController]
[Route("api/sample-transfers")]
public sealed class SampleTransfersController(TransferService transfers) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.SamplesView)]
    public async Task<IActionResult> List([FromQuery] TransferQuery query, CancellationToken ct) => Ok(await transfers.ListAsync(query, ct));

    [HttpGet("destinations")]
    [HasPermission(Permissions.SamplesTransfer)]
    public async Task<IActionResult> Destinations(CancellationToken ct) => Ok(await transfers.DestinationsAsync(ct));

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.SamplesView)]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await transfers.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.SamplesTransfer)]
    public async Task<IActionResult> Create(TransferCreate input, CancellationToken ct) => Ok(await transfers.CreateAsync(input, ct));

    [HttpPost("{id:long}/complete")]
    [HasPermission(Permissions.SamplesReceive)]
    public async Task<IActionResult> Complete(long id, CancellationToken ct) => Ok(await transfers.CompleteAsync(id, ct));

    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.SamplesTransfer)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct) => Ok(await transfers.CancelAsync(id, ct));
}
