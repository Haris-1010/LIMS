using Lims.Application.Abstractions;
using Lims.Application.Barcodes;
using Lims.Application.Common;
using Lims.Application.Corporate;
using Lims.Application.Orders;
using Lims.Domain.Billing;
using Lims.Domain.Core;
using Lims.Domain.Security;
using Lims.Infrastructure.Orders;
using Lims.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Corporate;

/// <summary>What an order for a corporate client is priced with.</summary>
public sealed record CorporatePricing(int ClientId, string ClientName, int? ContractId, string? ContractName, decimal DiscountPercent, IReadOnlyDictionary<int, decimal> Prices, int TermsDays, decimal? CreditLimit);

/// <summary>
/// Corporate clients, contracts, credit invoices, receipts and their allocation. A credit invoice is an ordinary invoice that carries a
/// corporate client; the client pays it later by a receipt that is allocated to invoices. Each allocation is recorded as a payment on the invoice
/// (method "Corporate"), so the invoice arithmetic stays the one the rest of billing uses. These are organisation-wide accounts: everything here is
/// scoped by organisation and gated by the corporate.* permissions, not by branch.
/// </summary>
public sealed class CorporateService(LimsDbContext db, INumberSequenceService numbers, ILabClock clock, BillingService billing)
{
    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");
    private int UserId => db.Request.UserId ?? throw new BusinessRuleException("forbidden", "Sign in required.", 403);
    private DateOnly Today => DateOnly.FromDateTime(clock.LocalNow);

    // ================= Clients =================

    public async Task<PagedResult<ClientListItem>> ListAsync(ClientQuery q, CancellationToken ct)
    {
        var query = db.CorporateClients.AsNoTracking().AsQueryable();
        if (q.IsActive is bool a) query = query.Where(c => c.IsActive == a);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(c => c.Code.Contains(term) || c.NameEn.Contains(term) || (c.NameUr != null && c.NameUr.Contains(term)) || (c.ContactName != null && c.ContactName.Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var page = await query.OrderBy(c => c.NameEn).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        var accounts = await AccountsAsync(page, ct);
        return new PagedResult<ClientListItem>(page.Select(c => ToItem(c, accounts[c.Id])).ToList(), total, q.Page, q.PageSize);
    }

    private static ClientListItem ToItem(CorporateClient c, AccountSummary a) =>
        new(c.Id, c.Code, c.NameEn, c.NameUr, c.ContactName, c.Phone, c.CreditLimit, c.PaymentTermsDays, c.IsActive, a.Balance, a.Aging.Overdue);

    public async Task<ClientDetail> GetAsync(int id, CancellationToken ct)
    {
        var c = await db.CorporateClients.AsNoTracking().Include(x => x.Contracts).ThenInclude(k => k.Prices).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw BusinessRuleException.NotFound("Client");
        var account = (await AccountsAsync([c], ct))[c.Id];
        var testIds = c.Contracts.SelectMany(k => k.Prices.Select(p => p.TestId)).Distinct().ToList();
        var tests = await TestInfoAsync(testIds, ct);
        var contracts = c.Contracts.OrderByDescending(k => k.ValidFrom).Select(k => new ContractDto(k.Id, k.Name, k.ValidFrom, k.ValidTo, k.DiscountPercent, k.IsActive,
            k.Prices.Select(p => ToPrice(p, tests)).OrderBy(p => p.TestCode).ToList(), Convert.ToBase64String(k.RowVersion))).ToList();
        return new ClientDetail(c.Id, c.Code, c.NameEn, c.NameUr, c.ContactName, c.Phone, c.Email, c.AddressEn, c.AddressUr, c.CreditLimit, c.PaymentTermsDays, c.IsActive,
            Convert.ToBase64String(c.RowVersion), contracts, account);
    }

    private static ContractPriceDto ToPrice(CorporateContractPrice p, Dictionary<int, (string Code, string NameEn, string? NameUr, decimal Price)> tests)
    {
        tests.TryGetValue(p.TestId, out var t);
        return new ContractPriceDto(p.TestId, t.Code ?? "", t.NameEn ?? "", t.NameUr, p.Price, t.Price);
    }

    private async Task<Dictionary<int, (string Code, string NameEn, string? NameUr, decimal Price)>> TestInfoAsync(List<int> ids, CancellationToken ct)
    {
        var orgId = OrgId;
        return await db.Tests.IgnoreQueryFilters().AsNoTracking().Where(t => t.OrganizationId == orgId && ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => (t.Code, t.NameEn, t.NameUr, t.Price), ct);
    }

    public async Task<int> CreateAsync(ClientInput input, CancellationToken ct)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.CorporateClients.AnyAsync(c => c.Code == code, ct)) throw BusinessRuleException.Conflict("client_code_taken", "A client with this code already exists.");
        var c = new CorporateClient { OrganizationId = OrgId, Code = code };
        Apply(c, input);
        db.CorporateClients.Add(c);
        await db.SaveChangesAsync(ct);
        return c.Id;
    }

    public async Task UpdateAsync(int id, ClientUpdate input, CancellationToken ct)
    {
        var c = await db.CorporateClients.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Client");
        db.Entry(c).Property(x => x.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        Apply(c, input);
        c.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(CorporateClient c, ClientInput i)
    {
        c.NameEn = i.NameEn.Trim(); c.NameUr = Blank(i.NameUr); c.ContactName = Blank(i.ContactName); c.Phone = Blank(i.Phone); c.Email = Blank(i.Email);
        c.AddressEn = Blank(i.AddressEn); c.AddressUr = Blank(i.AddressUr); c.CreditLimit = i.CreditLimit is decimal l ? CorporateRules.Round(l) : null; c.PaymentTermsDays = i.PaymentTermsDays;
    }

    // ================= Contracts =================

    public async Task<int> CreateContractAsync(int clientId, ContractInput input, CancellationToken ct)
    {
        if (!await db.CorporateClients.AnyAsync(c => c.Id == clientId, ct)) throw BusinessRuleException.NotFound("Client");
        var k = new CorporateContract { OrganizationId = OrgId, ClientId = clientId };
        await ApplyContractAsync(k, input, null, ct);
        db.CorporateContracts.Add(k);
        await db.SaveChangesAsync(ct);
        return k.Id;
    }

    public async Task UpdateContractAsync(int id, ContractUpdate input, CancellationToken ct)
    {
        var k = await db.CorporateContracts.Include(x => x.Prices).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Contract");
        db.Entry(k).Property(x => x.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        k.IsActive = input.IsActive;
        await ApplyContractAsync(k, input, id, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyContractAsync(CorporateContract k, ContractInput input, int? selfId, CancellationToken ct)
    {
        if (input.ValidTo is DateOnly to && to < input.ValidFrom) throw new BusinessRuleException("contract_dates_invalid", "The end date is before the start date.");
        var testIds = input.Prices.Select(p => p.TestId).ToList();
        if (testIds.Distinct().Count() != testIds.Count) throw new BusinessRuleException("contract_duplicate_test", "A test appears twice in the price list.");
        var known = (await TestInfoAsync(testIds, ct)).Keys.ToHashSet();
        if (testIds.Any(t => !known.Contains(t))) throw new BusinessRuleException("contract_test_invalid", "A test in the price list was not found.");
        k.Name = input.Name.Trim();
        k.ValidFrom = input.ValidFrom;
        k.ValidTo = input.ValidTo;
        k.DiscountPercent = CorporateRules.Round(input.DiscountPercent);
        // Two active contracts must not cover the same day, or the price of an order would be ambiguous.
        if (k.IsActive)
        {
            var others = await db.CorporateContracts.AsNoTracking().Where(c => c.ClientId == k.ClientId && c.IsActive && c.Id != (selfId ?? 0)).ToListAsync(ct);
            var end = input.ValidTo ?? DateOnly.MaxValue;
            if (others.Any(o => o.ValidFrom <= end && (o.ValidTo ?? DateOnly.MaxValue) >= input.ValidFrom))
                throw BusinessRuleException.Conflict("contract_overlap", "Another active contract of this client covers part of these dates.");
        }
        k.Prices.Clear();
        foreach (var p in input.Prices) k.Prices.Add(new CorporateContractPrice { OrganizationId = OrgId, TestId = p.TestId, Price = CorporateRules.Round(p.Price) });
    }

    // ================= Pricing for orders =================

    /// <summary>The prices, discount, terms and credit limit that apply to an order for this client today.</summary>
    public async Task<CorporatePricing> PricingAsync(int clientId, CancellationToken ct)
    {
        var c = await db.CorporateClients.AsNoTracking().Include(x => x.Contracts).ThenInclude(k => k.Prices).SingleOrDefaultAsync(x => x.Id == clientId && x.IsActive, ct)
                ?? throw new BusinessRuleException("corporate_client_invalid", "Choose an active corporate client.");
        var today = Today;
        var k = c.Contracts.FirstOrDefault(x => x.Covers(today));
        return new CorporatePricing(c.Id, c.NameEn, k?.Id, k?.Name, k?.DiscountPercent ?? 0, k?.Prices.ToDictionary(p => p.TestId, p => p.Price) ?? [], c.PaymentTermsDays, c.CreditLimit);
    }

    public async Task<decimal> BalanceAsync(int clientId, CancellationToken ct) =>
        (await AccountsAsync([new CorporateClient { Id = clientId, PaymentTermsDays = 0 }], ct))[clientId].Balance;

    // ================= Account, aging =================

    private sealed record InvoiceRow(long Id, DateTime CreatedAtUtc, DateOnly? DueDate, decimal Net, decimal Balance, InvoiceStatus Status, int ClientId);

    private async Task<List<InvoiceRow>> InvoiceRowsAsync(IReadOnlyCollection<int> clientIds, CancellationToken ct)
    {
        var orgId = OrgId;
        return await db.Invoices.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.OrganizationId == orgId && i.CorporateClientId != null && clientIds.Contains(i.CorporateClientId.Value))
            .Select(i => new InvoiceRow(i.Id, i.CreatedAtUtc, i.DueDate, i.Net, i.Balance, i.Status, i.CorporateClientId!.Value)).ToListAsync(ct);
    }

    private async Task<Dictionary<int, AccountSummary>> AccountsAsync(IReadOnlyCollection<CorporateClient> clients, CancellationToken ct)
    {
        var ids = clients.Select(c => c.Id).ToList();
        var rows = await InvoiceRowsAsync(ids, ct);
        var unallocated = await db.CorporateReceipts.AsNoTracking().Where(r => ids.Contains(r.ClientId)).GroupBy(r => r.ClientId)
            .Select(g => new { g.Key, Sum = g.Sum(r => r.Amount - r.Allocated) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        var today = Today;
        var result = new Dictionary<int, AccountSummary>();
        foreach (var c in clients)
        {
            var open = rows.Where(r => r.ClientId == c.Id && r.Status != InvoiceStatus.Cancelled && r.Balance > 0).ToList();
            decimal Sum(AgingBucket b) => open.Where(r => CorporateRules.BucketOf(r.DueDate ?? DateOnly.FromDateTime(clock.ToLocal(r.CreatedAtUtc)), today) == b).Sum(r => r.Balance);
            var aging = new AgingDto(Sum(AgingBucket.NotDue), Sum(AgingBucket.Days1To30), Sum(AgingBucket.Days31To60), Sum(AgingBucket.Days61To90), Sum(AgingBucket.Over90));
            var outstanding = open.Sum(r => r.Balance);
            var unalloc = unallocated.GetValueOrDefault(c.Id);
            var balance = CorporateRules.AccountBalance(outstanding, unalloc);
            var limit = clients.First(x => x.Id == c.Id).CreditLimit;
            result[c.Id] = new AccountSummary(c.Id, outstanding, unalloc, balance, limit, limit is decimal l ? Math.Max(0, l - balance) : null, aging);
        }
        return result;
    }

    public async Task<AccountSummary> AccountAsync(int clientId, CancellationToken ct)
    {
        var c = await db.CorporateClients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clientId, ct) ?? throw BusinessRuleException.NotFound("Client");
        return (await AccountsAsync([c], ct))[c.Id];
    }

    // ================= Credit invoices =================

    public async Task<PagedResult<CorporateInvoiceItem>> InvoicesAsync(CorporateInvoiceQuery q, CancellationToken ct)
    {
        var orgId = OrgId;
        var query = db.Invoices.IgnoreQueryFilters().AsNoTracking().Where(i => i.OrganizationId == orgId && i.CorporateClientId != null);
        if (q.ClientId is int cid) query = query.Where(i => i.CorporateClientId == cid);
        if (q.OutstandingOnly) query = query.Where(i => i.Balance > 0 && i.Status != InvoiceStatus.Cancelled);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            var norm = Code128.NormalizeScan(term);
            var orderIds = await db.Orders.IgnoreQueryFilters().Where(o => o.OrganizationId == orgId && o.OrderNo == norm).Select(o => o.Id).ToListAsync(ct);
            var patientIds = await db.Patients.IgnoreQueryFilters().Where(p => p.OrganizationId == orgId && (p.PatientNo == norm || p.FullName.Contains(term))).Select(p => p.Id).Take(500).ToListAsync(ct);
            query = query.Where(i => i.InvoiceNo == norm || orderIds.Contains(i.OrderId) || patientIds.Contains(i.PatientId));
        }
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(i => i.Id).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        var orders = await db.Orders.IgnoreQueryFilters().AsNoTracking().Where(o => o.OrganizationId == orgId && page.Select(i => i.OrderId).Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNo, ct);
        var patients = await db.Patients.IgnoreQueryFilters().AsNoTracking().Where(p => p.OrganizationId == orgId && page.Select(i => i.PatientId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var clients = await db.CorporateClients.AsNoTracking().Where(c => page.Select(i => i.CorporateClientId!.Value).Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.NameEn, ct);
        var today = Today;
        var items = page.Select(i => new CorporateInvoiceItem(
            i.Id, i.InvoiceNo, i.OrderId, orders.GetValueOrDefault(i.OrderId) ?? "", i.CorporateClientId!.Value, clients.GetValueOrDefault(i.CorporateClientId!.Value) ?? "",
            patients.GetValueOrDefault(i.PatientId)?.FullName ?? "", patients.GetValueOrDefault(i.PatientId)?.FullNameUr, i.CreatedAtUtc, i.DueDate, i.Net, i.Paid, i.Balance, i.Status,
            i.Balance > 0 && i.Status != InvoiceStatus.Cancelled ? CorporateRules.BucketOf(i.DueDate ?? DateOnly.FromDateTime(clock.ToLocal(i.CreatedAtUtc)), today) : null)).ToList();
        return new PagedResult<CorporateInvoiceItem>(items, total, q.Page, q.PageSize);
    }

    // ================= Receipts and allocation =================

    public async Task<long> CreateReceiptAsync(ReceiptCreate input, CancellationToken ct)
    {
        if (input.IdempotencyKey == Guid.Empty) throw new BusinessRuleException("idempotency_key_required", "The form is missing its submission key. Reload the page.");
        if (await db.CorporateReceipts.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey).Select(r => (long?)r.Id).FirstOrDefaultAsync(ct) is long existing) return existing;
        if (!Enum.IsDefined(input.Kind)) throw new BusinessRuleException("receipt_kind_invalid", "Choose receipt or credit note.");
        var need = input.Kind == CorporateReceiptKind.CreditNote ? Permissions.CorporateAdjust : Permissions.CorporateReceive;
        if (!db.Request.Can(need)) throw new BusinessRuleException("forbidden", "You do not have permission for this.", 403);
        if (!await db.CorporateClients.AnyAsync(c => c.Id == input.ClientId, ct)) throw BusinessRuleException.NotFound("Client");
        var amount = CorporateRules.Round(input.Amount);
        if (amount <= 0) throw new BusinessRuleException("payment_amount_invalid", "Enter an amount greater than zero.");
        if (input.Kind == CorporateReceiptKind.Receipt)
        {
            if (input.Method is not PaymentMethod m || !Enum.IsDefined(m)) throw new BusinessRuleException("payment_method_invalid", "Choose a payment method.");
            if (m is PaymentMethod.Cash or PaymentMethod.Corporate) throw new BusinessRuleException("corporate_method_invalid", "Corporate receipts are by card, bank transfer, wallet or cheque.");
        }
        else if (string.IsNullOrWhiteSpace(input.Reason)) throw new BusinessRuleException("reason_required", "A reason is required.");

        var r = new CorporateReceipt
        {
            OrganizationId = OrgId, ClientId = input.ClientId, IdempotencyKey = input.IdempotencyKey, Kind = input.Kind,
            Method = input.Kind == CorporateReceiptKind.Receipt ? input.Method : null, Amount = amount, Reference = Blank(input.Reference),
            Reason = input.Kind == CorporateReceiptKind.CreditNote ? input.Reason!.Trim() : Blank(input.Reason), ReceivedAtUtc = clock.UtcNow, ReceivedByUserId = UserId,
            ReceiptNo = await numbers.NextAsync(SequenceTypes.CorporateReceipt, db.Request.ActiveBranchId, ct),
        };
        db.CorporateReceipts.Add(r);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            if (await db.CorporateReceipts.AsNoTracking().Where(x => x.IdempotencyKey == input.IdempotencyKey).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct) is long winner) return winner;
            throw;
        }
        return r.Id;
    }

    /// <summary>Matches part of a receipt to invoices. Each match is a payment on the invoice; a receipt cannot be allocated beyond its amount, nor an invoice beyond what it owes.</summary>
    public async Task<ReceiptDto> AllocateAsync(long receiptId, AllocateRequest req, CancellationToken ct)
    {
        var orgId = OrgId;
        if (req.Items.GroupBy(i => i.InvoiceId).Any(g => g.Count() > 1)) throw new BusinessRuleException("allocation_duplicate_invoice", "An invoice appears twice.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var receipt = await db.CorporateReceipts.SingleOrDefaultAsync(r => r.Id == receiptId, ct) ?? throw BusinessRuleException.NotFound("Receipt");
        // The caller must have seen the receipt as it is now: two people allocating the same receipt cannot both win.
        db.Entry(receipt).Property(r => r.RowVersion).OriginalValue = RowVersion.FromToken(req.RowVersion);
        db.Entry(receipt).Property(r => r.Reference).IsModified = true;
        var need = receipt.Kind == CorporateReceiptKind.CreditNote ? Permissions.CorporateAdjust : Permissions.CorporateReceive;
        if (!db.Request.Can(need)) throw new BusinessRuleException("forbidden", "You do not have permission for this.", 403);
        var total = CorporateRules.Round(req.Items.Sum(i => i.Amount));
        if (total > CorporateRules.Round(receipt.Unallocated))
            throw new BusinessRuleException("allocation_exceeds_receipt", $"Only {receipt.Unallocated:N2} of this receipt is left to allocate.", 400, new { unallocated = receipt.Unallocated });

        foreach (var item in req.Items)
        {
            var invoice = await db.Invoices.IgnoreQueryFilters().Include(i => i.Lines).Include(i => i.Payments).Include(i => i.Refunds).AsSplitQuery()
                .SingleOrDefaultAsync(i => i.OrganizationId == orgId && i.Id == item.InvoiceId, ct) ?? throw BusinessRuleException.NotFound("Invoice");
            if (invoice.CorporateClientId != receipt.ClientId) throw new BusinessRuleException("allocation_wrong_client", "This invoice belongs to a different client.");
            var amount = CorporateRules.Round(item.Amount);
            if (amount > CorporateRules.MaxAllocation(receipt.Unallocated, invoice.Balance))
                throw new BusinessRuleException("allocation_exceeds_invoice", $"Invoice {invoice.InvoiceNo} only owes {invoice.Balance:N2}.", 400, new { invoice.Balance });
            var paymentId = await billing.ReceiveAsync(invoice, new PaymentCreate(Guid.NewGuid(), PaymentMethod.Corporate, amount, receipt.ReceiptNo), ct, invoice.BranchId);
            db.CorporateAllocations.Add(new CorporateAllocation
            {
                OrganizationId = orgId, ReceiptId = receipt.Id, InvoiceId = invoice.Id, PaymentId = paymentId, Amount = amount, AllocatedAtUtc = clock.UtcNow, AllocatedByUserId = UserId,
            });
            receipt.Allocated = CorporateRules.Round(receipt.Allocated + amount);
        }
        db.AddBusinessAudit("Corporate.Allocate", nameof(CorporateReceipt), receipt.ReceiptNo, new { total, invoices = req.Items.Length });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetReceiptAsync(receipt.Id, ct);
    }

    public async Task<PagedResult<ReceiptDto>> ReceiptsAsync(ReceiptQuery q, CancellationToken ct)
    {
        var query = db.CorporateReceipts.AsNoTracking().AsQueryable();
        if (q.ClientId is int c) query = query.Where(r => r.ClientId == c);
        if (q.UnallocatedOnly) query = query.Where(r => r.Allocated < r.Amount);
        if (!string.IsNullOrWhiteSpace(q.Search)) { var norm = Code128.NormalizeScan(q.Search); query = query.Where(r => r.ReceiptNo == norm || (r.Reference != null && r.Reference.Contains(q.Search.Trim()))); }
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(r => r.Id).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<ReceiptDto>(await HydrateReceiptsAsync(page, ct), total, q.Page, q.PageSize);
    }

    public async Task<ReceiptDto> GetReceiptAsync(long id, CancellationToken ct)
    {
        var r = await db.CorporateReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Receipt");
        return (await HydrateReceiptsAsync([r], ct))[0];
    }

    private async Task<List<ReceiptDto>> HydrateReceiptsAsync(List<CorporateReceipt> rs, CancellationToken ct)
    {
        var orgId = OrgId;
        var ids = rs.Select(r => r.Id).ToList();
        var allocations = await db.CorporateAllocations.AsNoTracking().Where(a => ids.Contains(a.ReceiptId)).OrderBy(a => a.Id).ToListAsync(ct);
        var invoiceIds = allocations.Select(a => a.InvoiceId).Distinct().ToList();
        var invoiceNos = await db.Invoices.IgnoreQueryFilters().AsNoTracking().Where(i => i.OrganizationId == orgId && invoiceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.InvoiceNo, ct);
        var clients = await db.CorporateClients.AsNoTracking().Where(c => rs.Select(r => r.ClientId).Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.NameEn, ct);
        var userIds = rs.Select(r => r.ReceivedByUserId).Concat(allocations.Select(a => a.AllocatedByUserId)).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);
        return rs.Select(r => new ReceiptDto(r.Id, r.ReceiptNo, r.ClientId, clients.GetValueOrDefault(r.ClientId) ?? "", r.Kind, r.Method, r.Amount, r.Allocated, r.Unallocated,
            r.Reference, r.Reason, r.ReceivedAtUtc, users.GetValueOrDefault(r.ReceivedByUserId), Convert.ToBase64String(r.RowVersion),
            allocations.Where(a => a.ReceiptId == r.Id).Select(a => new AllocationDto(a.Id, a.InvoiceId, invoiceNos.GetValueOrDefault(a.InvoiceId) ?? "", a.Amount, a.AllocatedAtUtc, users.GetValueOrDefault(a.AllocatedByUserId))).ToList())).ToList();
    }

    // ================= Statement =================

    public async Task<Statement> StatementAsync(int clientId, StatementQuery q, CancellationToken ct)
    {
        var c = await db.CorporateClients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clientId, ct) ?? throw BusinessRuleException.NotFound("Client");
        var to = q.To ?? Today;
        var from = q.From ?? new DateOnly(to.Year, to.Month, 1);
        if (from > to) throw new BusinessRuleException("statement_dates_invalid", "The start date is after the end date.");
        var orgId = OrgId;
        var invoices = await db.Invoices.IgnoreQueryFilters().AsNoTracking().Where(i => i.OrganizationId == orgId && i.CorporateClientId == clientId && i.Net > 0)
            .Select(i => new { i.Id, i.InvoiceNo, i.OrderId, i.CreatedAtUtc, i.Net }).ToListAsync(ct);
        var orderNos = await db.Orders.IgnoreQueryFilters().AsNoTracking().Where(o => o.OrganizationId == orgId && invoices.Select(i => i.OrderId).Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNo, ct);
        var receipts = await db.CorporateReceipts.AsNoTracking().Where(r => r.ClientId == clientId).ToListAsync(ct);

        var entries = invoices.Select(i => (Date: DateOnly.FromDateTime(clock.ToLocal(i.CreatedAtUtc)), Order: 0, Type: "Invoice", Ref: i.InvoiceNo, Desc: (string?)orderNos.GetValueOrDefault(i.OrderId), Debit: i.Net, Credit: 0m, At: i.CreatedAtUtc))
            .Concat(receipts.Select(r => (Date: DateOnly.FromDateTime(clock.ToLocal(r.ReceivedAtUtc)), Order: 1, Type: r.Kind == CorporateReceiptKind.CreditNote ? "CreditNote" : "Receipt", Ref: r.ReceiptNo, Desc: r.Reason ?? r.Reference, Debit: 0m, Credit: r.Amount, At: r.ReceivedAtUtc)))
            .OrderBy(e => e.Date).ThenBy(e => e.Order).ThenBy(e => e.At).ToList();
        var opening = CorporateRules.Round(entries.Where(e => e.Date < from).Sum(e => e.Debit - e.Credit));
        var running = opening;
        var lines = new List<StatementLine>();
        foreach (var e in entries.Where(e => e.Date >= from && e.Date <= to))
        {
            running = CorporateRules.Round(running + e.Debit - e.Credit);
            lines.Add(new StatementLine(e.Date, e.Type, e.Ref, e.Desc, e.Debit, e.Credit, running));
        }
        var account = (await AccountsAsync([c], ct))[c.Id];
        return new Statement(ToItem(c, account), from, to, opening, lines, running, account);
    }

    // ================= Reconciliation =================

    /// <summary>Checks that the books agree: receipts against their allocations, allocations against payments, payments against invoices, and the client balance against totals.</summary>
    public async Task<ReconcileResult> ReconcileAsync(int? clientId, CancellationToken ct)
    {
        var orgId = OrgId;
        var issues = new List<string>();
        var clients = await db.CorporateClients.AsNoTracking().Where(c => clientId == null || c.Id == clientId).ToListAsync(ct);
        foreach (var c in clients)
        {
            var receipts = await db.CorporateReceipts.AsNoTracking().Where(r => r.ClientId == c.Id).ToListAsync(ct);
            var allocations = await db.CorporateAllocations.AsNoTracking().Where(a => receipts.Select(r => r.Id).Contains(a.ReceiptId)).ToListAsync(ct);
            foreach (var r in receipts)
            {
                var sum = allocations.Where(a => a.ReceiptId == r.Id).Sum(a => a.Amount);
                if (sum != r.Allocated) issues.Add($"{c.Code}: receipt {r.ReceiptNo} says {r.Allocated:N2} allocated but its allocations add up to {sum:N2}.");
            }
            var payIds = allocations.Select(a => a.PaymentId).ToList();
            var payments = await db.Payments.IgnoreQueryFilters().AsNoTracking().Where(p => p.OrganizationId == orgId && payIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            foreach (var a in allocations)
                if (!payments.TryGetValue(a.PaymentId, out var p) || p.Amount != a.Amount || p.Method != PaymentMethod.Corporate || p.InvoiceId != a.InvoiceId)
                    issues.Add($"{c.Code}: allocation {a.Id} does not match its payment.");
            var invoices = await db.Invoices.IgnoreQueryFilters().AsNoTracking().Include(i => i.Payments).Where(i => i.OrganizationId == orgId && i.CorporateClientId == c.Id).ToListAsync(ct);
            foreach (var i in invoices)
            {
                var allocated = allocations.Where(a => a.InvoiceId == i.Id).Sum(a => a.Amount);
                var corp = i.Payments.Where(p => p.Method == PaymentMethod.Corporate).Sum(p => p.Amount);
                if (allocated != corp) issues.Add($"{c.Code}: invoice {i.InvoiceNo} has {corp:N2} in corporate payments but {allocated:N2} allocated.");
                if (i.Payments.Any(p => p.Method != PaymentMethod.Corporate)) issues.Add($"{c.Code}: invoice {i.InvoiceNo} has a counter payment on a credit invoice.");
            }
            var account = (await AccountsAsync([c], ct))[c.Id];
            var invoiced = invoices.Where(i => i.Status != InvoiceStatus.Cancelled).Sum(i => i.Net);
            var expected = CorporateRules.Round(invoiced - receipts.Sum(r => r.Amount));
            if (expected != account.Balance) issues.Add($"{c.Code}: balance {account.Balance:N2} differs from invoiced less received ({expected:N2}).");
        }
        return new ReconcileResult(issues.Count == 0, issues);
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
