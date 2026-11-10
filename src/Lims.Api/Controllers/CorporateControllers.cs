using Lims.Api.Security;
using Lims.Application.Corporate;
using Lims.Domain.Security;
using Lims.Infrastructure.Corporate;
using Microsoft.AspNetCore.Mvc;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/corporate/clients")]
public sealed class CorporateClientsController(CorporateService corporate) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.CorporateView, Permissions.CorporateInvoice)]
    public async Task<IActionResult> List([FromQuery] ClientQuery query, CancellationToken ct) => Ok(await corporate.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.CorporateView)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await corporate.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.CorporateManage)]
    public async Task<IActionResult> Create(ClientInput input, CancellationToken ct)
    {
        var id = await corporate.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id }, await corporate.GetAsync(id, ct));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.CorporateManage)]
    public async Task<IActionResult> Update(int id, ClientUpdate input, CancellationToken ct)
    {
        await corporate.UpdateAsync(id, input, ct);
        return Ok(await corporate.GetAsync(id, ct));
    }

    [HttpPost("{id:int}/contracts")]
    [HasPermission(Permissions.CorporateManage)]
    public async Task<IActionResult> CreateContract(int id, ContractInput input, CancellationToken ct)
    {
        await corporate.CreateContractAsync(id, input, ct);
        return Ok(await corporate.GetAsync(id, ct));
    }

    [HttpPut("contracts/{contractId:int}")]
    [HasPermission(Permissions.CorporateManage)]
    public async Task<IActionResult> UpdateContract(int contractId, ContractUpdate input, CancellationToken ct)
    {
        await corporate.UpdateContractAsync(contractId, input, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/account")]
    [HasPermission(Permissions.CorporateView)]
    public async Task<IActionResult> Account(int id, CancellationToken ct) => Ok(await corporate.AccountAsync(id, ct));

    [HttpGet("{id:int}/statement")]
    [HasPermission(Permissions.CorporateView)]
    public async Task<IActionResult> Statement(int id, [FromQuery] StatementQuery query, CancellationToken ct) => Ok(await corporate.StatementAsync(id, query, ct));
}

[ApiController]
[Route("api/corporate")]
public sealed class CorporateBillingController(CorporateService corporate) : ControllerBase
{
    [HttpGet("invoices")]
    [HasPermission(Permissions.CorporateView, Permissions.CorporateInvoice)]
    public async Task<IActionResult> Invoices([FromQuery] CorporateInvoiceQuery query, CancellationToken ct) => Ok(await corporate.InvoicesAsync(query, ct));

    [HttpGet("receipts")]
    [HasPermission(Permissions.CorporateView, Permissions.CorporateReceive)]
    public async Task<IActionResult> Receipts([FromQuery] ReceiptQuery query, CancellationToken ct) => Ok(await corporate.ReceiptsAsync(query, ct));

    [HttpGet("receipts/{id:long}")]
    [HasPermission(Permissions.CorporateView, Permissions.CorporateReceive)]
    public async Task<IActionResult> Receipt(long id, CancellationToken ct) => Ok(await corporate.GetReceiptAsync(id, ct));

    /// <summary>A receipt needs corporate.receive; a credit note needs corporate.adjust (checked for the kind in the request).</summary>
    [HttpPost("receipts")]
    [HasPermission(Permissions.CorporateReceive, Permissions.CorporateAdjust)]
    public async Task<IActionResult> CreateReceipt(ReceiptCreate input, CancellationToken ct)
    {
        var id = await corporate.CreateReceiptAsync(input, ct);
        return Ok(await corporate.GetReceiptAsync(id, ct));
    }

    [HttpPost("receipts/{id:long}/allocate")]
    [HasPermission(Permissions.CorporateReceive, Permissions.CorporateAdjust)]
    public async Task<IActionResult> Allocate(long id, AllocateRequest input, CancellationToken ct) => Ok(await corporate.AllocateAsync(id, input, ct));

    [HttpGet("reconcile")]
    [HasPermission(Permissions.CorporateView)]
    public async Task<IActionResult> Reconcile([FromQuery] int? clientId, CancellationToken ct) => Ok(await corporate.ReconcileAsync(clientId, ct));
}
