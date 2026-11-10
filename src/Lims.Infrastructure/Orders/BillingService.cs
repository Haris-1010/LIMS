using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Orders;
using Lims.Domain.Billing;
using Lims.Domain.Core;
using Lims.Domain.Orders;
using Lims.Domain.Security;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Orders;

/// <summary>Payments, refunds and cash drawers. Every money movement recalculates the invoice in the same transaction.</summary>
public sealed class BillingService(LimsDbContext db, INumberSequenceService numbers, ILabClock clock)
{
    private int BranchId => db.Request.ActiveBranchId
        ?? throw new BusinessRuleException("branch_required", "Select the branch you are working at before taking payments.");
    private int UserId => db.Request.UserId ?? throw new BusinessRuleException("forbidden", "Sign in required.", 403);

    // ---------- Payments ----------

    public async Task<long> AddPaymentAsync(long invoiceId, PaymentCreate input, CancellationToken ct)
    {
        if (await ExistingPaymentAsync(input.IdempotencyKey, invoiceId, ct) is long done) return done;
        var invoice = await LoadAsync(invoiceId, ct);
        // Credit invoices are paid by the client through receipts and allocation, never at the counter.
        if (invoice.CorporateClientId is not null) throw BusinessRuleException.Conflict("invoice_is_credit", "This invoice is billed to a corporate client and is paid through corporate receipts.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var id = await ReceiveAsync(invoice, input, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>Records a payment on a loaded invoice (caller owns the transaction).</summary>
    internal async Task<long> ReceiveAsync(Invoice invoice, PaymentCreate input, CancellationToken ct, int? branchOverride = null)
    {
        if (input.IdempotencyKey == Guid.Empty)
            throw new BusinessRuleException("idempotency_key_required", "The payment form is missing its submission key. Reload the page.");
        if (!Enum.IsDefined(input.Method)) throw new BusinessRuleException("payment_method_invalid", "Choose a payment method.");
        var amount = InvoiceCalculator.Round(input.Amount);
        if (amount <= 0) throw new BusinessRuleException("payment_amount_invalid", "Enter an amount greater than zero.");
        if (invoice.Status == InvoiceStatus.Cancelled) throw BusinessRuleException.Conflict("invoice_cancelled", "This invoice is cancelled.");
        if (amount > invoice.Balance)
            throw new BusinessRuleException("payment_exceeds_balance", $"The amount is more than the balance due ({invoice.Balance:N2}).", details: new { invoice.Balance });

        var branchId = branchOverride ?? BranchId;
        long? drawer = input.Method == PaymentMethod.Cash ? await OpenDrawerIdAsync(branchId, ct) : null;
        var payment = new Payment
        {
            OrganizationId = invoice.OrganizationId, BranchId = branchId, InvoiceId = invoice.Id, PatientId = invoice.PatientId,
            Method = input.Method, Amount = amount, Reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim(),
            ReceivedAtUtc = clock.UtcNow, ReceivedByUserId = UserId, DrawerSessionId = drawer, IdempotencyKey = input.IdempotencyKey,
            ReceiptNo = await numbers.NextAsync(SequenceTypes.Receipt, branchId, ct),
        };
        invoice.Payments.Add(payment);
        InvoiceCalculator.Recalculate(invoice);
        await db.SaveChangesAsync(ct); // invoice RowVersion stops two cashiers over-collecting at once
        return payment.Id;
    }

    // ---------- Refunds ----------

    public async Task<long> RefundAsync(long invoiceId, RefundCreate input, CancellationToken ct)
    {
        if (input.IdempotencyKey == Guid.Empty)
            throw new BusinessRuleException("idempotency_key_required", "The refund form is missing its submission key. Reload the page.");
        if (!Enum.IsDefined(input.Method)) throw new BusinessRuleException("payment_method_invalid", "Choose a payment method.");
        var earlier = await db.Refunds.IgnoreQueryFilters()
            .Where(r => r.OrganizationId == db.Request.OrganizationId && r.IdempotencyKey == input.IdempotencyKey)
            .Select(r => new { r.Id, r.InvoiceId }).FirstOrDefaultAsync(ct);
        if (earlier is not null)
        {
            // Same key, same invoice: a retry — return the original. Same key on another invoice is a client error, never a silent success.
            if (earlier.InvoiceId != invoiceId)
                throw BusinessRuleException.Conflict("idempotency_key_reused", "This submission key was already used for a different invoice. Reload the page.");
            return earlier.Id;
        }
        if (string.IsNullOrWhiteSpace(input.Reason)) throw new BusinessRuleException("refund_reason_required", "Give a reason for the refund.");
        var amount = InvoiceCalculator.Round(input.Amount);
        var invoice = await LoadAsync(invoiceId, ct);
        // Only money actually owed back can be refunded (Balance < 0 after cancellations).
        var due = -invoice.Balance;
        if (amount <= 0 || amount > due)
            throw new BusinessRuleException("refund_exceeds_due", due > 0 ? $"At most {due:N2} can be refunded." : "Nothing is due for refund on this invoice.", details: new { due = Math.Max(due, 0) });

        var branchId = BranchId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        long? drawer = input.Method == PaymentMethod.Cash ? await OpenDrawerIdAsync(branchId, ct) : null;
        var refund = new Refund
        {
            OrganizationId = invoice.OrganizationId, BranchId = branchId, InvoiceId = invoice.Id, Method = input.Method, Amount = amount,
            Reason = input.Reason.Trim(), RefundedAtUtc = clock.UtcNow, RefundedByUserId = UserId, DrawerSessionId = drawer,
            IdempotencyKey = input.IdempotencyKey, RefundNo = await numbers.NextAsync(SequenceTypes.Refund, branchId, ct),
        };
        invoice.Refunds.Add(refund);
        InvoiceCalculator.Recalculate(invoice);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return refund.Id;
    }

    // ---------- Invoice view ----------

    internal async Task<InvoiceDto> InvoiceDtoAsync(long orderId, IReadOnlyList<LabOrderItem> items, CancellationToken ct)
    {
        var inv = await db.Invoices.AsNoTracking().Include(i => i.Lines).Include(i => i.Payments).Include(i => i.Refunds)
            .AsSplitQuery().SingleAsync(i => i.OrderId == orderId, ct);
        var userIds = inv.Payments.Select(p => p.ReceivedByUserId).Concat(inv.Refunds.Select(r => r.RefundedByUserId)).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);

        return new InvoiceDto(inv.Id, inv.InvoiceNo, inv.Gross, inv.DiscountPercent, inv.DiscountAmount, inv.DiscountReason, inv.Net,
            inv.Paid, inv.Refunded, inv.Balance, inv.Status,
            inv.Lines.OrderBy(l => l.Id).Select(l =>
            {
                var lineItems = items.Where(i => i.InvoiceLineId == l.Id).ToList();
                return new InvoiceLineDto(l.Id, l.Kind, l.Code, l.NameEn, l.NameUr, l.Price, l.IsCancelled, l.CancelReason,
                    lineItems.Select(i => i.TestCode).ToList(),
                    !l.IsCancelled && lineItems.All(i => i.Status == OrderItemStatus.Ordered));
            }).ToList(),
            inv.Payments.OrderBy(p => p.Id).Select(p => new PaymentDto(p.Id, p.ReceiptNo, p.Method, p.Amount, p.Reference, p.ReceivedAtUtc, names.GetValueOrDefault(p.ReceivedByUserId))).ToList(),
            inv.Refunds.OrderBy(r => r.Id).Select(r => new RefundDto(r.Id, r.RefundNo, r.Method, r.Amount, r.Reason, r.RefundedAtUtc, names.GetValueOrDefault(r.RefundedByUserId))).ToList(),
            Convert.ToBase64String(inv.RowVersion));
    }

    public async Task<long> OrderIdOfInvoiceAsync(long invoiceId, CancellationToken ct) =>
        await db.Invoices.Where(i => i.Id == invoiceId).Select(i => (long?)i.OrderId).SingleOrDefaultAsync(ct)
        ?? throw BusinessRuleException.NotFound("Invoice");

    // ---------- Payment list & day summary ----------

    public async Task<PagedResult<PaymentListItem>> PaymentsAsync(PaymentQuery q, CancellationToken ct)
    {
        var query = from p in db.Payments.AsNoTracking()
                    join inv in db.Invoices.AsNoTracking() on p.InvoiceId equals inv.Id
                    join o in db.Orders on inv.OrderId equals o.Id
                    join pt in db.Patients on new { p.OrganizationId, Id = p.PatientId } equals new { pt.OrganizationId, pt.Id }
                    select new { p, inv, o, pt };
        if (q.Method is PaymentMethod m) query = query.Where(x => x.p.Method == m);
        if (q.From is DateOnly from) { var f = clock.LocalDateStartUtc(from); query = query.Where(x => x.p.ReceivedAtUtc >= f); }
        if (q.To is DateOnly to) { var t = clock.LocalDateStartUtc(to.AddDays(1)); query = query.Where(x => x.p.ReceivedAtUtc < t); }
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToUpper();
            query = query.Where(x => x.p.ReceiptNo == s || x.o.OrderNo == s || x.inv.InvoiceNo == s || x.pt.PatientNo == s || x.pt.FullName.Contains(s));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.p.Id).Skip(q.Skip).Take(q.PageSize)
            .Select(x => new PaymentListItem(x.p.Id, x.p.ReceiptNo, x.p.ReceivedAtUtc, x.o.Id, x.o.OrderNo, x.inv.InvoiceNo, x.pt.PatientNo,
                x.pt.FullName, x.p.Method, x.p.Amount, x.p.Reference,
                db.Users.Where(u => u.Id == x.p.ReceivedByUserId).Select(u => u.DisplayNameEn).FirstOrDefault()))
            .ToListAsync(ct);
        return new PagedResult<PaymentListItem>(items, total, q.Page, q.PageSize);
    }

    /// <summary>Money received and refunded on a local business day, by method (branch-filtered for the caller).</summary>
    public async Task<DaySummary> DaySummaryAsync(DateOnly? date, CancellationToken ct)
    {
        var day = date ?? DateOnly.FromDateTime(clock.LocalNow);
        var (from, to) = (clock.LocalDateStartUtc(day), clock.LocalDateStartUtc(day.AddDays(1)));
        // The summary is for the branch the user is working at; the all-branches view (null) covers every branch they may access.
        var branch = db.Request.ActiveBranchId;
        var received = await db.Payments.AsNoTracking().Where(p => p.ReceivedAtUtc >= from && p.ReceivedAtUtc < to && (branch == null || p.BranchId == branch))
            .GroupBy(p => p.Method).Select(g => new { Method = g.Key, Sum = g.Sum(p => p.Amount), Count = g.Count() }).ToListAsync(ct);
        var refunded = await db.Refunds.AsNoTracking().Where(r => r.RefundedAtUtc >= from && r.RefundedAtUtc < to && (branch == null || r.BranchId == branch))
            .GroupBy(r => r.Method).Select(g => new { Method = g.Key, Sum = g.Sum(r => r.Amount) }).ToListAsync(ct);
        var orders = await db.Orders.AsNoTracking().CountAsync(o => o.CreatedAtUtc >= from && o.CreatedAtUtc < to && o.Status == OrderStatus.Active && (branch == null || o.BranchId == branch), ct);

        var byMethod = Enum.GetValues<PaymentMethod>()
            .Select(m => new MethodTotal(m, received.Where(r => r.Method == m).Sum(r => r.Sum), refunded.Where(r => r.Method == m).Sum(r => r.Sum),
                received.Where(r => r.Method == m).Sum(r => r.Count)))
            .Where(m => m.Received != 0 || m.Refunded != 0).ToList();
        return new DaySummary(day, db.Request.ActiveBranchId, byMethod, byMethod.Sum(m => m.Received), byMethod.Sum(m => m.Refunded), orders);
    }

    // ---------- Cash drawer ----------

    public async Task<DrawerDto?> CurrentDrawerAsync(CancellationToken ct)
    {
        if (db.Request.ActiveBranchId is not int branchId) return null;
        var s = await db.CashDrawerSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.BranchId == branchId && x.UserId == UserId && x.Status == DrawerStatus.Open, ct);
        return s is null ? null : await ToDtoAsync(s, ct);
    }

    public async Task<DrawerDto> OpenDrawerAsync(DrawerOpen input, CancellationToken ct)
    {
        var branchId = BranchId;
        if (await db.CashDrawerSessions.AnyAsync(x => x.BranchId == branchId && x.UserId == UserId && x.Status == DrawerStatus.Open, ct))
            throw BusinessRuleException.Conflict("drawer_already_open", "Your cash drawer is already open at this branch.");
        var s = new CashDrawerSession
        {
            OrganizationId = db.Request.OrganizationId!.Value, BranchId = branchId, UserId = UserId, Status = DrawerStatus.Open,
            OpenedAtUtc = clock.UtcNow, OpeningFloat = InvoiceCalculator.Round(input.OpeningFloat),
        };
        db.CashDrawerSessions.Add(s);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(s, ct);
    }

    /// <summary>Closes the caller's drawer: expected = float + cash received − cash refunded; variance = counted − expected.</summary>
    public async Task<DrawerDto> CloseDrawerAsync(DrawerClose input, CancellationToken ct)
    {
        var branchId = BranchId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Lock first: waits for any payment already in progress, and makes later ones fail with "drawer not open".
        var drawerId = await LockOpenDrawerAsync(branchId, ct)
                       ?? throw BusinessRuleException.Conflict("drawer_not_open", "Your cash drawer is not open at this branch.");
        var s = await db.CashDrawerSessions.SingleAsync(x => x.Id == drawerId, ct);
        var (cashIn, cashOut) = await CashMovementsAsync(s.Id, ct);
        s.CashIn = cashIn;
        s.CashOut = cashOut;
        s.ExpectedCash = s.OpeningFloat + cashIn - cashOut;
        s.CountedCash = InvoiceCalculator.Round(input.CountedCash);
        s.Variance = s.CountedCash - s.ExpectedCash;
        s.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        s.ClosedAtUtc = clock.UtcNow;
        s.Status = DrawerStatus.Closed;
        if (s.Variance != 0 && s.Notes is null)
            throw new BusinessRuleException("drawer_variance_note_required", "The counted cash differs from the expected amount. Add a note explaining the difference.",
                details: new { expected = s.ExpectedCash, variance = s.Variance });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ToDtoAsync(s, ct);
    }
    /// <summary>Recent drawer sessions: supervisors (cash.close) see the whole branch, cashiers see their own.</summary>
    public async Task<List<DrawerDto>> DrawersAsync(CancellationToken ct)
    {
        var query = db.CashDrawerSessions.AsNoTracking();
        if (!db.Request.Can(Permissions.CashClose)) query = query.Where(x => x.UserId == UserId);
        var sessions = await query.OrderByDescending(x => x.Id).Take(50).ToListAsync(ct);
        var result = new List<DrawerDto>();
        foreach (var s in sessions) result.Add(await ToDtoAsync(s, ct));
        return result;
    }

    /// <summary>
    /// Finds the caller's open drawer AND locks its row until the surrounding transaction ends, in one statement.
    /// Cash payments, cash refunds and closing the drawer all take this lock, so a payment can never slip into
    /// a drawer after its totals were counted (the closing waits for in-flight payments, and later ones see it closed).
    /// Must be called inside a transaction.
    /// </summary>
    private async Task<long?> LockOpenDrawerAsync(int branchId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("The cash drawer can only be locked inside a transaction.");
        var orgId = db.Request.OrganizationId!.Value;
        var userId = UserId;
        var now = clock.UtcNow;
        var ids = await db.Database.SqlQuery<long>($"""
            UPDATE billing.CashDrawerSessions
               SET UpdatedAtUtc = {now}
            OUTPUT inserted.Id AS [Value]
             WHERE OrganizationId = {orgId} AND BranchId = {branchId} AND UserId = {userId} AND Status = 0
            """).ToListAsync(ct);
        return ids.Count == 0 ? null : ids[0];
    }

    private async Task<long> OpenDrawerIdAsync(int branchId, CancellationToken ct) =>
        await LockOpenDrawerAsync(branchId, ct)
        ?? throw BusinessRuleException.Conflict("drawer_not_open", "Open your cash drawer before taking or returning cash.");
    private async Task<(decimal In, decimal Out)> CashMovementsAsync(long sessionId, CancellationToken ct)
    {
        var cashIn = await db.Payments.Where(p => p.DrawerSessionId == sessionId).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var cashOut = await db.Refunds.Where(r => r.DrawerSessionId == sessionId).SumAsync(r => (decimal?)r.Amount, ct) ?? 0;
        return (cashIn, cashOut);
    }

    private async Task<DrawerDto> ToDtoAsync(CashDrawerSession s, CancellationToken ct)
    {
        var (cashIn, cashOut) = s.Status == DrawerStatus.Closed ? (s.CashIn ?? 0, s.CashOut ?? 0) : await CashMovementsAsync(s.Id, ct);
        var name = await db.Users.Where(u => u.Id == s.UserId).Select(u => u.DisplayNameEn).SingleOrDefaultAsync(ct);
        return new DrawerDto(s.Id, s.BranchId, s.UserId, name, s.Status, s.OpenedAtUtc, s.OpeningFloat, cashIn, cashOut,
            s.ExpectedCash ?? s.OpeningFloat + cashIn - cashOut, s.ClosedAtUtc, s.CountedCash, s.Variance, s.Notes);
    }

    private async Task<Invoice> LoadAsync(long invoiceId, CancellationToken ct) =>
        await db.Invoices.Include(i => i.Lines).Include(i => i.Payments).Include(i => i.Refunds).AsSplitQuery()
            .SingleOrDefaultAsync(i => i.Id == invoiceId, ct) ?? throw BusinessRuleException.NotFound("Invoice");

    private async Task<long?> ExistingPaymentAsync(Guid key, long invoiceId, CancellationToken ct)
    {
        if (key == Guid.Empty) return null;
        var earlier = await db.Payments.IgnoreQueryFilters()
            .Where(p => p.OrganizationId == db.Request.OrganizationId && p.IdempotencyKey == key)
            .Select(p => new { p.Id, p.InvoiceId }).FirstOrDefaultAsync(ct);
        if (earlier is null) return null;
        if (earlier.InvoiceId != invoiceId)
            throw BusinessRuleException.Conflict("idempotency_key_reused", "This submission key was already used for a different invoice. Reload the page.");
        return earlier.Id;
    }
}
