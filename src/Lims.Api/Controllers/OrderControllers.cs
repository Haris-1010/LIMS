using Lims.Api.Security;
using Lims.Application.Orders;
using Lims.Domain.Security;
using Lims.Infrastructure.Orders;
using Microsoft.AspNetCore.Mvc;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(OrderService orders) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.OrdersView)]
    public async Task<IActionResult> List([FromQuery] OrderQuery query, CancellationToken ct) => Ok(await orders.ListAsync(query, ct));

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.OrdersView)]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await orders.GetAsync(id, ct));

    /// <summary>Prices and totals for the order form, calculated by the server from the catalog.</summary>
    [HttpPost("quote")]
    [HasPermission(Permissions.OrdersCreate)]
    public async Task<IActionResult> Quote(QuoteRequest request, CancellationToken ct) => Ok(await orders.QuoteAsync(request, ct));

    /// <summary>Creates order + invoice (+ first payment). Resubmitting the same idempotency key returns the same order.</summary>
    [HttpPost]
    [HasPermission(Permissions.OrdersCreate)]
    public async Task<IActionResult> Create(OrderCreate input, CancellationToken ct)
    {
        var id = await orders.CreateAsync(input, ct);
        return Ok(await orders.GetAsync(id, ct));
    }

    [HttpPost("{id:long}/lines/{lineId:long}/cancel")]
    [HasPermission(Permissions.OrdersCancel)]
    public async Task<IActionResult> CancelLine(long id, long lineId, CancelRequest input, CancellationToken ct)
    {
        await orders.CancelLineAsync(id, lineId, input.Reason, ct);
        return Ok(await orders.GetAsync(id, ct));
    }

    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.OrdersCancel)]
    public async Task<IActionResult> Cancel(long id, CancelRequest input, CancellationToken ct)
    {
        await orders.CancelOrderAsync(id, input.Reason, ct);
        return Ok(await orders.GetAsync(id, ct));
    }
}

[ApiController]
[Route("api/billing")]
public sealed class BillingController(BillingService billing, OrderService orders) : ControllerBase
{
    [HttpPost("invoices/{id:long}/payments")]
    [HasPermission(Permissions.PaymentsCollect)]
    public async Task<IActionResult> Pay(long id, PaymentCreate input, CancellationToken ct)
    {
        await billing.AddPaymentAsync(id, input, ct);
        return Ok(await orders.GetAsync(await billing.OrderIdOfInvoiceAsync(id, ct), ct));
    }

    [HttpPost("invoices/{id:long}/refunds")]
    [HasPermission(Permissions.BillingRefund)]
    public async Task<IActionResult> Refund(long id, RefundCreate input, CancellationToken ct)
    {
        await billing.RefundAsync(id, input, ct);
        return Ok(await orders.GetAsync(await billing.OrderIdOfInvoiceAsync(id, ct), ct));
    }

    [HttpGet("payments")]
    [HasPermission(Permissions.BillingView)]
    public async Task<IActionResult> Payments([FromQuery] PaymentQuery query, CancellationToken ct) => Ok(await billing.PaymentsAsync(query, ct));

    [HttpGet("day-summary")]
    [HasPermission(Permissions.BillingView)]
    public async Task<IActionResult> DaySummary([FromQuery] DateOnly? date, CancellationToken ct) => Ok(await billing.DaySummaryAsync(date, ct));

    [HttpGet("drawer")]
    [HasPermission(Permissions.PaymentsCollect)]
    public async Task<IActionResult> CurrentDrawer(CancellationToken ct) => Ok(new { drawer = await billing.CurrentDrawerAsync(ct) });

    [HttpPost("drawer/open")]
    [HasPermission(Permissions.PaymentsCollect)]
    public async Task<IActionResult> OpenDrawer(DrawerOpen input, CancellationToken ct) => Ok(await billing.OpenDrawerAsync(input, ct));

    [HttpPost("drawer/close")]
    [HasPermission(Permissions.PaymentsCollect)]
    public async Task<IActionResult> CloseDrawer(DrawerClose input, CancellationToken ct) => Ok(await billing.CloseDrawerAsync(input, ct));

    [HttpGet("drawers")]
    [HasPermission(Permissions.PaymentsCollect, Permissions.CashClose)]
    public async Task<IActionResult> Drawers(CancellationToken ct) => Ok(await billing.DrawersAsync(ct));
}
