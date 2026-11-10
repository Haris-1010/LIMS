using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Inventory;
using Lims.Domain.Core;
using Lims.Domain.Inventory;
using Lims.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Inventory;

/// <summary>
/// Reagents and consumables: items, suppliers, purchase receipts, batches with expiry, manual stock changes, transfers between branches,
/// automatic consumption from test profiles, alerts and the stock ledger. Every change to a batch is one atomic conditional SQL update
/// (so two people can never take the same stock, and stock never goes below zero) followed by an append-only ledger line.
/// </summary>
public sealed class InventoryService(LimsDbContext db, INumberSequenceService numbers, ILabClock clock)
{
    public const string SettingAllowNegative = "Inventory.AllowNegativeStock";
    public const string SettingExpiryDays = "Inventory.ExpiryAlertDays";

    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");
    private int UserId => db.Request.UserId ?? throw new BusinessRuleException("forbidden", "Sign in required.", 403);
    private int ActiveBranch => db.Request.ActiveBranchId ?? throw new BusinessRuleException("branch_required", "Select the branch you are working at (top bar) first.");
    private DateOnly Today => DateOnly.FromDateTime(clock.LocalNow);

    private async Task<string?> SettingAsync(string key, CancellationToken ct) =>
        await db.BusinessSettings.AsNoTracking().Where(s => s.Key == key && s.BranchId == null).Select(s => s.Value).FirstOrDefaultAsync(ct);

    private async Task<int> ExpiryDaysAsync(CancellationToken ct) => int.TryParse(await SettingAsync(SettingExpiryDays, ct), out var d) && d is > 0 and <= 365 ? d : 30;

    // ================= Items =================

    public async Task<PagedResult<ItemListItem>> ItemsAsync(ItemQuery q, CancellationToken ct)
    {
        var query = db.StockItems.AsNoTracking().AsQueryable();
        if (q.Kind is StockItemKind k) query = query.Where(i => i.Kind == k);
        if (q.IsActive is bool a) query = query.Where(i => i.IsActive == a);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(i => i.Code.Contains(term) || i.NameEn.Contains(term) || (i.NameUr != null && i.NameUr.Contains(term)));
        }
        var all = await query.OrderBy(i => i.NameEn).ToListAsync(ct);
        var stock = await StockByItemAsync(all.Select(i => i.Id).ToList(), ct);
        var rows = all.Select(i => { var s = stock.GetValueOrDefault(i.Id) ?? new StockSummary(0, 0, 0); return new ItemListItem(i.Id, i.Code, i.NameEn, i.NameUr, i.Kind, i.UnitName, i.ReorderLevel, i.TracksExpiry, i.IsActive, s.OnHand, i.IsActive && InventoryRules.IsLow(s.OnHand, i.ReorderLevel), s.Expiring, s.Expired); });
        if (q.LowOnly) rows = rows.Where(r => r.IsLow);
        var list = rows.ToList();
        return new PagedResult<ItemListItem>(list.Skip(q.Skip).Take(q.PageSize).ToList(), list.Count, q.Page, q.PageSize);
    }

    private sealed record StockSummary(decimal OnHand, decimal Expiring, decimal Expired);

    /// <summary>On hand per item at the working branch (or across the branches the caller may see when none is selected), with what is expiring or expired.</summary>
    private async Task<Dictionary<int, StockSummary>> StockByItemAsync(List<int> itemIds, CancellationToken ct)
    {
        var branch = db.Request.ActiveBranchId;
        var batches = db.StockBatches.AsNoTracking().Where(b => itemIds.Contains(b.ItemId) && b.QuantityOnHand > 0);
        if (branch is int br) batches = batches.Where(b => b.BranchId == br);
        var rows = await batches.Select(b => new { b.ItemId, b.QuantityOnHand, b.ExpiryDate }).ToListAsync(ct);
        var days = await ExpiryDaysAsync(ct);
        var today = Today;
        return rows.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => new StockSummary(
            g.Sum(r => r.QuantityOnHand),
            g.Where(r => InventoryRules.ExpiryOf(r.ExpiryDate, today, days) == ExpiryState.Expiring).Sum(r => r.QuantityOnHand),
            g.Where(r => InventoryRules.ExpiryOf(r.ExpiryDate, today, days) == ExpiryState.Expired).Sum(r => r.QuantityOnHand)));
    }

    public async Task<ItemDetail> ItemAsync(int id, CancellationToken ct)
    {
        var orgId = OrgId;
        var i = await db.StockItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Item");
        var days = await ExpiryDaysAsync(ct);
        var today = Today;
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.NameEn, ct);
        var batches = (await db.StockBatches.AsNoTracking().Where(b => b.ItemId == id && (b.QuantityOnHand != 0)).OrderBy(b => b.BranchId).ThenBy(b => b.ExpiryDate == null).ThenBy(b => b.ExpiryDate).ToListAsync(ct))
            .Select(b => new BatchDto(b.Id, b.BranchId, branches.GetValueOrDefault(b.BranchId) ?? "", b.BatchNo, b.ExpiryDate, b.QuantityOnHand, b.UnitCost, InventoryRules.ExpiryOf(b.ExpiryDate, today, days))).ToList();
        var profile = await ProfileOfItemAsync(id, ct);
        return new ItemDetail(i.Id, i.Code, i.NameEn, i.NameUr, i.Kind, i.UnitName, i.ReorderLevel, i.TracksExpiry, i.DefaultSupplierId, i.IsActive, Convert.ToBase64String(i.RowVersion), batches, profile);
    }

    public async Task<int> CreateItemAsync(ItemInput input, CancellationToken ct)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.StockItems.AnyAsync(i => i.Code == code, ct)) throw BusinessRuleException.Conflict("item_code_taken", "An item with this code already exists.");
        await RequireSupplierAsync(input.DefaultSupplierId, ct);
        var item = new StockItem { OrganizationId = OrgId, Code = code };
        ApplyItem(item, input);
        db.StockItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }

    public async Task UpdateItemAsync(int id, ItemUpdate input, CancellationToken ct)
    {
        var item = await db.StockItems.SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw BusinessRuleException.NotFound("Item");
        db.Entry(item).Property(i => i.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        await RequireSupplierAsync(input.DefaultSupplierId, ct);
        ApplyItem(item, input);
        item.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    private static void ApplyItem(StockItem i, ItemInput x)
    {
        i.NameEn = x.NameEn.Trim(); i.NameUr = Blank(x.NameUr); i.Kind = Enum.IsDefined(x.Kind) ? x.Kind : StockItemKind.Reagent; i.UnitName = x.UnitName.Trim();
        i.ReorderLevel = InventoryRules.Round(x.ReorderLevel); i.TracksExpiry = x.TracksExpiry; i.DefaultSupplierId = x.DefaultSupplierId;
    }

    private async Task RequireSupplierAsync(int? id, CancellationToken ct)
    {
        if (id is int s && !await db.Suppliers.AnyAsync(x => x.Id == s, ct)) throw new BusinessRuleException("supplier_invalid", "Choose a supplier from the list.");
    }

    // ================= Suppliers =================

    public async Task<List<SupplierDto>> SuppliersAsync(bool includeInactive, CancellationToken ct) =>
        (await db.Suppliers.AsNoTracking().Where(s => includeInactive || s.IsActive).OrderBy(s => s.NameEn).ToListAsync(ct)).Select(ToDto).ToList();

    private static SupplierDto ToDto(Supplier s) => new(s.Id, s.Code, s.NameEn, s.NameUr, s.ContactName, s.Phone, s.Email, s.Address, s.IsActive, Convert.ToBase64String(s.RowVersion));

    public async Task<int> CreateSupplierAsync(SupplierInput input, CancellationToken ct)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.Suppliers.AnyAsync(s => s.Code == code, ct)) throw BusinessRuleException.Conflict("supplier_code_taken", "A supplier with this code already exists.");
        var s = new Supplier { OrganizationId = OrgId, Code = code };
        ApplySupplier(s, input);
        db.Suppliers.Add(s);
        await db.SaveChangesAsync(ct);
        return s.Id;
    }

    public async Task UpdateSupplierAsync(int id, SupplierUpdate input, CancellationToken ct)
    {
        var s = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Supplier");
        db.Entry(s).Property(x => x.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        ApplySupplier(s, input);
        s.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    private static void ApplySupplier(Supplier s, SupplierInput i)
    {
        s.NameEn = i.NameEn.Trim(); s.NameUr = Blank(i.NameUr); s.ContactName = Blank(i.ContactName); s.Phone = Blank(i.Phone); s.Email = Blank(i.Email); s.Address = Blank(i.Address);
    }

    // ================= Atomic stock updates =================

    /// <summary>Adds (or removes) stock in one conditional UPDATE. Returns the new balance, or null when it would take the batch below zero.</summary>
    private async Task<decimal?> ApplyAsync(long batchId, decimal delta, bool allowBelowZero, CancellationToken ct)
    {
        var org = OrgId;
        var rows = await db.Database.SqlQuery<decimal>($@"
UPDATE inventory.StockBatches SET QuantityOnHand = QuantityOnHand + {delta}
OUTPUT inserted.QuantityOnHand AS [Value]
WHERE Id = {batchId} AND OrganizationId = {org} AND ({allowBelowZero} = 1 OR QuantityOnHand + {delta} >= 0)").ToListAsync(ct);
        return rows.Count == 0 ? null : rows[0];
    }

    private StockMovement Move(StockBatch batch, MovementType type, decimal signedQty, decimal balance, string? reason, string? reference, string? key = null) => new()
    {
        OrganizationId = batch.OrganizationId, BranchId = batch.BranchId, ItemId = batch.ItemId, BatchId = batch.Id, Type = type, Quantity = signedQty, BalanceAfter = balance,
        Reason = Blank(reason), Reference = reference, ConsumptionKey = key, UserId = UserId, AtUtc = clock.UtcNow,
    };

    // ================= Purchases (stock in) =================

    public async Task<long> CreatePurchaseAsync(PurchaseCreate input, CancellationToken ct)
    {
        if (input.IdempotencyKey == Guid.Empty) throw new BusinessRuleException("idempotency_key_required", "The form is missing its submission key. Reload the page.");
        if (await db.PurchaseReceipts.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey).Select(r => (long?)r.Id).FirstOrDefaultAsync(ct) is long existing) return existing;
        var branch = ActiveBranch;
        var orgId = OrgId;
        if (!await db.Suppliers.AnyAsync(s => s.Id == input.SupplierId && s.IsActive, ct)) throw new BusinessRuleException("supplier_invalid", "Choose an active supplier.");
        var today = Today;
        var items = await db.StockItems.Where(i => input.Lines.Select(l => l.ItemId).Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);

        foreach (var l in input.Lines)
        {
            if (!items.TryGetValue(l.ItemId, out var it) || !it.IsActive) throw new BusinessRuleException("item_invalid", "Choose an active stock item.");
            if (it.TracksExpiry && l.ExpiryDate is null) throw new BusinessRuleException("expiry_required", $"{it.Code} needs an expiry date.");
            if (l.ExpiryDate is DateOnly e && e < today) throw new BusinessRuleException("expiry_in_past", $"{it.Code}: the expiry date is in the past.");
            if (string.IsNullOrWhiteSpace(l.BatchNo) || l.BatchNo.Trim().Equals(StockBatch.UnbatchedNo, StringComparison.OrdinalIgnoreCase)) throw new BusinessRuleException("batch_no_invalid", "Enter the batch number from the pack.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var receipt = new PurchaseReceipt
        {
            OrganizationId = orgId, BranchId = branch, SupplierId = input.SupplierId, SupplierInvoiceNo = Blank(input.SupplierInvoiceNo), Notes = Blank(input.Notes),
            ReceivedAtUtc = clock.UtcNow, IdempotencyKey = input.IdempotencyKey, ReceiptNo = await numbers.NextAsync(SequenceTypes.PurchaseReceipt, branch, ct),
        };
        db.PurchaseReceipts.Add(receipt);
        await db.SaveChangesAsync(ct);

        foreach (var l in input.Lines)
        {
            var batchNo = l.BatchNo.Trim();
            var qty = InventoryRules.Round(l.Quantity);
            var batch = await db.StockBatches.SingleOrDefaultAsync(b => b.ItemId == l.ItemId && b.BranchId == branch && b.BatchNo == batchNo, ct);
            if (batch is null)
            {
                batch = new StockBatch { OrganizationId = orgId, BranchId = branch, ItemId = l.ItemId, BatchNo = batchNo, ExpiryDate = l.ExpiryDate, UnitCost = l.UnitCost, SupplierId = input.SupplierId };
                db.StockBatches.Add(batch);
                await db.SaveChangesAsync(ct);
            }
            else if (batch.ExpiryDate != l.ExpiryDate)
                throw BusinessRuleException.Conflict("batch_expiry_mismatch", $"Batch {batchNo} is already in stock with a different expiry date.");
            var balance = await ApplyAsync(batch.Id, qty, true, ct) ?? throw BusinessRuleException.NotFound("Batch");
            db.StockMovements.Add(Move(batch, MovementType.PurchaseIn, qty, balance, null, receipt.ReceiptNo));
            receipt.Lines.Add(new PurchaseLine { OrganizationId = orgId, ItemId = l.ItemId, BatchNo = batchNo, ExpiryDate = l.ExpiryDate, Quantity = qty, UnitCost = l.UnitCost, BatchId = batch.Id });
        }
        db.AddBusinessAudit("Inventory.PurchaseReceived", nameof(PurchaseReceipt), receipt.ReceiptNo, new { lines = input.Lines.Length, supplier = input.SupplierId });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            if (await db.PurchaseReceipts.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey).Select(r => (long?)r.Id).FirstOrDefaultAsync(ct) is long winner) return winner;
            throw;
        }
        await tx.CommitAsync(ct);
        return receipt.Id;
    }

    public async Task<PagedResult<PurchaseDto>> PurchasesAsync(PurchaseQuery q, CancellationToken ct)
    {
        var query = db.PurchaseReceipts.AsNoTracking().Include(r => r.Lines).AsQueryable();
        if (q.SupplierId is int s) query = query.Where(r => r.SupplierId == s);
        if (!string.IsNullOrWhiteSpace(q.Search)) { var t = q.Search.Trim().ToUpperInvariant(); query = query.Where(r => r.ReceiptNo.Contains(t) || (r.SupplierInvoiceNo != null && r.SupplierInvoiceNo.Contains(q.Search.Trim()))); }
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(r => r.Id).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<PurchaseDto>(await HydratePurchasesAsync(page, ct), total, q.Page, q.PageSize);
    }

    public async Task<PurchaseDto> PurchaseAsync(long id, CancellationToken ct)
    {
        var r = await db.PurchaseReceipts.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Receipt");
        return (await HydratePurchasesAsync([r], ct))[0];
    }

    private async Task<List<PurchaseDto>> HydratePurchasesAsync(List<PurchaseReceipt> rs, CancellationToken ct)
    {
        var supplierIds = rs.Select(r => r.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.NameEn, ct);
        var itemIds = rs.SelectMany(r => r.Lines.Select(l => l.ItemId)).Distinct().ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.NameEn, ct);
        return rs.Select(r => new PurchaseDto(r.Id, r.ReceiptNo, r.SupplierId, suppliers.GetValueOrDefault(r.SupplierId) ?? "", r.SupplierInvoiceNo, r.ReceivedAtUtc, branches.GetValueOrDefault(r.BranchId) ?? "", r.Notes,
            r.Lines.Sum(l => l.Quantity * (l.UnitCost ?? 0)),
            r.Lines.Select(l => new PurchaseLineDto(l.ItemId, items.GetValueOrDefault(l.ItemId)?.Code ?? "", items.GetValueOrDefault(l.ItemId)?.NameEn ?? "", l.BatchNo, l.ExpiryDate, l.Quantity, l.UnitCost)).ToList())).ToList();
    }

    // ================= Manual stock changes =================

    public async Task<MovementDto> MoveAsync(MovementRequest req, CancellationToken ct)
    {
        if (!Enum.IsDefined(req.Kind)) throw new BusinessRuleException("movement_kind_invalid", "Choose what is being done to the stock.");
        if (req.Kind != ManualMovement.Issue && string.IsNullOrWhiteSpace(req.Reason)) throw new BusinessRuleException("reason_required", "A reason is required.");
        var batch = await db.StockBatches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == req.BatchId, ct) ?? throw BusinessRuleException.NotFound("Batch");
        var type = req.Kind switch { ManualMovement.Issue => MovementType.Issue, ManualMovement.AdjustIn => MovementType.AdjustIn, ManualMovement.AdjustOut => MovementType.AdjustOut, _ => MovementType.WriteOff };
        var qty = InventoryRules.Round(req.Quantity);
        var signed = InventoryRules.Signed(type, qty);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var balance = await ApplyAsync(batch.Id, signed, signed > 0, ct) ?? throw new BusinessRuleException("insufficient_stock", "There is not enough stock in this batch.", 409, new { batch.QuantityOnHand });
        var m = Move(batch, type, signed, balance, req.Reason, null);
        db.StockMovements.Add(m);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await HydrateMovementsAsync([m], ct))[0];
    }

    /// <summary>Moves stock to another branch: out of the source batch and into a matching batch there, as two linked ledger lines.</summary>
    public async Task<(MovementDto Out, MovementDto In)> TransferAsync(StockTransferRequest req, CancellationToken ct)
    {
        var orgId = OrgId;
        var source = await db.StockBatches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == req.BatchId, ct) ?? throw BusinessRuleException.NotFound("Batch");
        if (req.ToBranchId == source.BranchId) throw new BusinessRuleException("transfer_same_branch", "Choose a different branch as the destination.");
        if (!await db.Branches.AnyAsync(b => b.Id == req.ToBranchId && b.IsActive, ct)) throw new BusinessRuleException("transfer_branch_invalid", "The destination branch was not found.");
        var qty = InventoryRules.Round(req.Quantity);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var balanceOut = await ApplyAsync(source.Id, -qty, false, ct) ?? throw new BusinessRuleException("insufficient_stock", "There is not enough stock in this batch.", 409, new { source.QuantityOnHand });
        // The destination branch may be outside the caller's own branches, so its batch is looked up with the organisation scoped by hand.
        var dest = await db.StockBatches.IgnoreQueryFilters().SingleOrDefaultAsync(b => b.OrganizationId == orgId && b.ItemId == source.ItemId && b.BranchId == req.ToBranchId && b.BatchNo == source.BatchNo, ct);
        if (dest is null)
        {
            dest = new StockBatch { OrganizationId = orgId, BranchId = req.ToBranchId, ItemId = source.ItemId, BatchNo = source.BatchNo, ExpiryDate = source.ExpiryDate, UnitCost = source.UnitCost, SupplierId = source.SupplierId };
            db.StockBatches.Add(dest);
            await db.SaveChangesAsync(ct);
        }
        else if (dest.ExpiryDate != source.ExpiryDate) throw BusinessRuleException.Conflict("batch_expiry_mismatch", $"Batch {source.BatchNo} already exists at that branch with a different expiry date.");
        var balanceIn = await ApplyAsync(dest.Id, qty, true, ct) ?? throw BusinessRuleException.NotFound("Batch");

        var reference = await numbers.NextAsync(SequenceTypes.StockTransfer, source.BranchId, ct);
        var outM = Move(source, MovementType.TransferOut, -qty, balanceOut, req.Notes, reference);
        var inM = Move(dest, MovementType.TransferIn, qty, balanceIn, req.Notes, reference);
        db.StockMovements.AddRange(outM, inM);
        await db.SaveChangesAsync(ct);
        outM.PairedMovementId = inM.Id; inM.PairedMovementId = outM.Id;
        db.AddBusinessAudit("Inventory.Transfer", nameof(StockBatch), reference, new { source.ItemId, from = source.BranchId, to = req.ToBranchId, quantity = qty });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var dtos = await HydrateMovementsAsync([outM, inM], ct);
        return (dtos[0], dtos[1]);
    }

    // ================= Automatic consumption =================

    /// <summary>
    /// Charges one run of a test to stock, from the profile, earliest expiry first. It never blocks a result: what stock cannot cover is either
    /// taken into an "unbatched" balance (when negative stock is allowed) or recorded as a shortfall for someone to resolve. Charged once per run.
    /// </summary>
    public async Task ConsumeForTestAsync(long orderItemId, int testId, int branchId, string reference, CancellationToken ct)
    {
        var orgId = OrgId;
        var lines = await db.ConsumptionProfileLines.AsNoTracking().Where(p => p.TestId == testId).ToListAsync(ct);
        if (lines.Count == 0) return;
        var allowNegative = string.Equals(await SettingAsync(SettingAllowNegative, ct), "true", StringComparison.OrdinalIgnoreCase);
        var today = Today;

        foreach (var line in lines)
        {
            var key = $"OI{orderItemId}-I{line.ItemId}";
            if (await db.StockMovements.IgnoreQueryFilters().AnyAsync(m => m.OrganizationId == orgId && m.ConsumptionKey != null && m.ConsumptionKey.StartsWith(key + "-"), ct)) continue;
            if (await db.StockShortfalls.IgnoreQueryFilters().AnyAsync(s => s.OrganizationId == orgId && s.Reference == key, ct)) continue;

            var batches = await db.StockBatches.IgnoreQueryFilters().AsNoTracking().Where(b => b.OrganizationId == orgId && b.BranchId == branchId && b.ItemId == line.ItemId && b.QuantityOnHand > 0).ToListAsync(ct);
            var (takes, shortfall) = InventoryRules.PlanConsumption(batches.Select(b => new BatchStock(b.Id, b.QuantityOnHand, b.ExpiryDate)), line.QuantityPerTest, today);
            foreach (var take in takes)
            {
                var batch = batches.Single(b => b.Id == take.BatchId);
                if (await ApplyAsync(batch.Id, -take.Quantity, false, ct) is decimal balance)
                    db.StockMovements.Add(Move(batch, MovementType.Consumption, -take.Quantity, balance, null, reference, $"{key}-B{batch.Id}"));
                else shortfall = InventoryRules.Round(shortfall + take.Quantity);   // someone else took it a moment ago
            }
            if (shortfall > 0)
            {
                if (allowNegative)
                {
                    var open = await db.StockBatches.IgnoreQueryFilters().SingleOrDefaultAsync(b => b.OrganizationId == orgId && b.BranchId == branchId && b.ItemId == line.ItemId && b.BatchNo == StockBatch.UnbatchedNo, ct);
                    if (open is null) { open = new StockBatch { OrganizationId = orgId, BranchId = branchId, ItemId = line.ItemId, BatchNo = StockBatch.UnbatchedNo }; db.StockBatches.Add(open); await db.SaveChangesAsync(ct); }
                    var balance = await ApplyAsync(open.Id, -shortfall, true, ct) ?? 0;
                    db.StockMovements.Add(Move(open, MovementType.Consumption, -shortfall, balance, "Stock was short", reference, $"{key}-B{open.Id}"));
                }
                else db.StockShortfalls.Add(new StockShortfall { OrganizationId = orgId, BranchId = branchId, ItemId = line.ItemId, Quantity = shortfall, Reference = key, AtUtc = clock.UtcNow });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ProfileLineDto>> ProfileOfItemAsync(int itemId, CancellationToken ct)
    {
        var orgId = OrgId;
        var lines = await db.ConsumptionProfileLines.AsNoTracking().Where(p => p.ItemId == itemId).ToListAsync(ct);
        var tests = await db.Tests.IgnoreQueryFilters().AsNoTracking().Where(t => t.OrganizationId == orgId && lines.Select(l => l.TestId).Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        return lines.Select(l => new ProfileLineDto(l.TestId, tests.GetValueOrDefault(l.TestId)?.Code ?? "", tests.GetValueOrDefault(l.TestId)?.NameEn ?? "", tests.GetValueOrDefault(l.TestId)?.NameUr, l.QuantityPerTest))
            .OrderBy(l => l.TestCode).ToList();
    }

    /// <summary>Replaces which tests use this item and how much of it each uses.</summary>
    public async Task SetProfileAsync(int itemId, IReadOnlyList<ProfileTest> tests, CancellationToken ct)
    {
        var orgId = OrgId;
        if (!await db.StockItems.AnyAsync(i => i.Id == itemId, ct)) throw BusinessRuleException.NotFound("Item");
        if (tests.Select(t => t.TestId).Distinct().Count() != tests.Count) throw new BusinessRuleException("profile_duplicate_test", "A test appears twice.");
        var known = await db.Tests.IgnoreQueryFilters().Where(t => t.OrganizationId == orgId && tests.Select(x => x.TestId).Contains(t.Id)).Select(t => t.Id).ToListAsync(ct);
        if (tests.Any(t => !known.Contains(t.TestId))) throw new BusinessRuleException("profile_test_invalid", "A test was not found.");
        var existing = await db.ConsumptionProfileLines.Where(p => p.ItemId == itemId).ToListAsync(ct);
        db.ConsumptionProfileLines.RemoveRange(existing);
        foreach (var t in tests) db.ConsumptionProfileLines.Add(new ConsumptionProfileLine { OrganizationId = orgId, TestId = t.TestId, ItemId = itemId, QuantityPerTest = InventoryRules.Round(t.Quantity) });
        db.AddBusinessAudit("Inventory.ProfileChanged", nameof(StockItem), itemId, new { tests = tests.Count });
        await db.SaveChangesAsync(ct);
    }

    public sealed record ProfileTest(int TestId, decimal Quantity);

    // ================= Ledger, alerts, shortfalls =================

    public async Task<PagedResult<MovementDto>> LedgerAsync(LedgerQuery q, CancellationToken ct)
    {
        var query = db.StockMovements.AsNoTracking().AsQueryable();
        if (q.ItemId is int i) query = query.Where(m => m.ItemId == i);
        if (q.BranchId is int b) query = query.Where(m => m.BranchId == b);
        if (q.Type is MovementType t) query = query.Where(m => m.Type == t);
        if (q.From is DateOnly f) { var s = clock.LocalDateStartUtc(f); query = query.Where(m => m.AtUtc >= s); }
        if (q.To is DateOnly to) { var e = clock.LocalDateStartUtc(to.AddDays(1)); query = query.Where(m => m.AtUtc < e); }
        if (!string.IsNullOrWhiteSpace(q.Search)) { var term = q.Search.Trim(); query = query.Where(m => (m.Reference != null && m.Reference.Contains(term)) || (m.Reason != null && m.Reason.Contains(term))); }
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(m => m.Id).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<MovementDto>(await HydrateMovementsAsync(page, ct), total, q.Page, q.PageSize);
    }

    private async Task<List<MovementDto>> HydrateMovementsAsync(List<StockMovement> ms, CancellationToken ct)
    {
        var orgId = OrgId;
        var itemIds = ms.Select(m => m.ItemId).Distinct().ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var batchIds = ms.Select(m => m.BatchId).Distinct().ToList();
        var batches = await db.StockBatches.IgnoreQueryFilters().AsNoTracking().Where(b => b.OrganizationId == orgId && batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BatchNo, ct);
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.NameEn, ct);
        var userIds = ms.Select(m => m.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);
        return ms.Select(m => new MovementDto(m.Id, m.AtUtc, m.ItemId, items.GetValueOrDefault(m.ItemId)?.Code ?? "", items.GetValueOrDefault(m.ItemId)?.NameEn ?? "", items.GetValueOrDefault(m.ItemId)?.NameUr,
            m.BatchId, batches.GetValueOrDefault(m.BatchId) ?? "", m.BranchId, branches.GetValueOrDefault(m.BranchId) ?? "", m.Type, m.Quantity, m.BalanceAfter, m.Reason, m.Reference, users.GetValueOrDefault(m.UserId))).ToList();
    }

    public async Task<InventoryAlerts> AlertsAsync(CancellationToken ct)
    {
        var days = await ExpiryDaysAsync(ct);
        var today = Today;
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.NameEn, ct);
        var items = await db.StockItems.AsNoTracking().Where(i => i.IsActive).ToListAsync(ct);
        var stock = await StockByItemAsync(items.Select(i => i.Id).ToList(), ct);
        var low = items.Where(i => InventoryRules.IsLow(stock.GetValueOrDefault(i.Id)?.OnHand ?? 0, i.ReorderLevel))
            .Select(i => new LowStockDto(i.Id, i.Code, i.NameEn, i.NameUr, i.UnitName, stock.GetValueOrDefault(i.Id)?.OnHand ?? 0, i.ReorderLevel)).OrderBy(x => x.Name).ToList();

        var branch = db.Request.ActiveBranchId;
        var batches = db.StockBatches.AsNoTracking().Where(b => b.QuantityOnHand > 0 && b.ExpiryDate != null);
        if (branch is int br) batches = batches.Where(b => b.BranchId == br);
        var withExpiry = (await batches.ToListAsync(ct)).Where(b => InventoryRules.ExpiryOf(b.ExpiryDate, today, days) != ExpiryState.Ok).ToList();
        var byId = items.ToDictionary(i => i.Id);
        ExpiryAlertDto Dto(StockBatch b) { var i = byId.GetValueOrDefault(b.ItemId); return new ExpiryAlertDto(b.Id, b.ItemId, i?.Code ?? "", i?.NameEn ?? "", i?.NameUr, b.BatchNo, branches.GetValueOrDefault(b.BranchId) ?? "", b.ExpiryDate!.Value, b.QuantityOnHand, b.ExpiryDate.Value.DayNumber - today.DayNumber); }
        var expiring = withExpiry.Where(b => InventoryRules.ExpiryOf(b.ExpiryDate, today, days) == ExpiryState.Expiring).OrderBy(b => b.ExpiryDate).Select(Dto).ToList();
        var expired = withExpiry.Where(b => InventoryRules.ExpiryOf(b.ExpiryDate, today, days) == ExpiryState.Expired).OrderBy(b => b.ExpiryDate).Select(Dto).ToList();
        var shortfalls = await db.StockShortfalls.AsNoTracking().CountAsync(s => s.ResolvedAtUtc == null, ct);
        return new InventoryAlerts(low, expiring, expired, shortfalls, days);
    }

    public async Task<PagedResult<ShortfallDto>> ShortfallsAsync(bool openOnly, PageQuery q, CancellationToken ct)
    {
        var query = db.StockShortfalls.AsNoTracking().AsQueryable();
        if (openOnly) query = query.Where(s => s.ResolvedAtUtc == null);
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(s => s.Id).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        var items = await db.StockItems.AsNoTracking().Where(i => page.Select(s => s.ItemId).Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.NameEn, ct);
        return new PagedResult<ShortfallDto>(page.Select(s => new ShortfallDto(s.Id, s.ItemId, items.GetValueOrDefault(s.ItemId)?.Code ?? "", items.GetValueOrDefault(s.ItemId)?.NameEn ?? "", items.GetValueOrDefault(s.ItemId)?.UnitName ?? "",
            s.BranchId, branches.GetValueOrDefault(s.BranchId) ?? "", s.Quantity, s.Reference, s.AtUtc, s.ResolvedAtUtc, s.ResolutionNote)).ToList(), total, q.Page, q.PageSize);
    }

    public async Task ResolveShortfallAsync(long id, ShortfallResolve req, CancellationToken ct)
    {
        var s = await db.StockShortfalls.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Shortfall");
        if (s.ResolvedAtUtc is not null) throw BusinessRuleException.Conflict("shortfall_resolved", "This shortfall is already resolved.");
        s.ResolvedAtUtc = clock.UtcNow; s.ResolvedByUserId = UserId; s.ResolutionNote = Blank(req.Note);
        await db.SaveChangesAsync(ct);
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
