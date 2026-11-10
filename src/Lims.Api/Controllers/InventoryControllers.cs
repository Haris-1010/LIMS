using Lims.Api.Security;
using Lims.Application.Common;
using Lims.Application.Inventory;
using Lims.Domain.Security;
using Lims.Infrastructure.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/inventory/items")]
public sealed class StockItemsController(InventoryService inventory) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> List([FromQuery] ItemQuery query, CancellationToken ct) => Ok(await inventory.ItemsAsync(query, ct));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await inventory.ItemAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> Create(ItemInput input, CancellationToken ct)
    {
        var id = await inventory.CreateItemAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id }, await inventory.ItemAsync(id, ct));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> Update(int id, ItemUpdate input, CancellationToken ct)
    {
        await inventory.UpdateItemAsync(id, input, ct);
        return Ok(await inventory.ItemAsync(id, ct));
    }

    /// <summary>Replaces which tests use this item and how much each test uses.</summary>
    [HttpPut("{id:int}/profile")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> SetProfile(int id, ItemProfileRequest input, CancellationToken ct)
    {
        await inventory.SetProfileAsync(id, input.Tests.Select(t => new InventoryService.ProfileTest(t.TestId, t.QuantityPerTest)).ToList(), ct);
        return Ok(await inventory.ItemAsync(id, ct));
    }
}

[ApiController]
[Route("api/inventory/suppliers")]
public sealed class SuppliersController(InventoryService inventory) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct) => Ok(await inventory.SuppliersAsync(includeInactive, ct));

    [HttpPost]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> Create(SupplierInput input, CancellationToken ct)
    {
        await inventory.CreateSupplierAsync(input, ct);
        return Ok(await inventory.SuppliersAsync(true, ct));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> Update(int id, SupplierUpdate input, CancellationToken ct)
    {
        await inventory.UpdateSupplierAsync(id, input, ct);
        return Ok(await inventory.SuppliersAsync(true, ct));
    }
}

[ApiController]
[Route("api/inventory")]
public sealed class StockController(InventoryService inventory) : ControllerBase
{
    [HttpGet("purchases")]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> Purchases([FromQuery] PurchaseQuery query, CancellationToken ct) => Ok(await inventory.PurchasesAsync(query, ct));

    [HttpGet("purchases/{id:long}")]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> Purchase(long id, CancellationToken ct) => Ok(await inventory.PurchaseAsync(id, ct));

    [HttpPost("purchases")]
    [HasPermission(Permissions.InventoryPurchase)]
    public async Task<IActionResult> CreatePurchase(PurchaseCreate input, CancellationToken ct)
    {
        var id = await inventory.CreatePurchaseAsync(input, ct);
        return Ok(await inventory.PurchaseAsync(id, ct));
    }

    /// <summary>Issue, adjust or write off stock from one batch.</summary>
    [HttpPost("movements")]
    [HasPermission(Permissions.InventoryAdjust)]
    public async Task<IActionResult> Move(MovementRequest input, CancellationToken ct) => Ok(await inventory.MoveAsync(input, ct));

    [HttpPost("transfers")]
    [HasPermission(Permissions.InventoryAdjust)]
    public async Task<IActionResult> Transfer(StockTransferRequest input, CancellationToken ct)
    {
        var (o, i) = await inventory.TransferAsync(input, ct);
        return Ok(new { @out = o, @in = i });
    }

    [HttpGet("ledger")]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> Ledger([FromQuery] LedgerQuery query, CancellationToken ct) => Ok(await inventory.LedgerAsync(query, ct));

    [HttpGet("alerts")]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> Alerts(CancellationToken ct) => Ok(await inventory.AlertsAsync(ct));

    [HttpGet("shortfalls")]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> Shortfalls([FromQuery] bool openOnly, [FromQuery] PageQuery query, CancellationToken ct) => Ok(await inventory.ShortfallsAsync(openOnly, query, ct));

    [HttpPost("shortfalls/{id:long}/resolve")]
    [HasPermission(Permissions.InventoryAdjust)]
    public async Task<IActionResult> ResolveShortfall(long id, ShortfallResolve input, CancellationToken ct)
    {
        await inventory.ResolveShortfallAsync(id, input, ct);
        return NoContent();
    }
}
