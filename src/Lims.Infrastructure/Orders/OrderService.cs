using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Orders;
using Lims.Domain.Billing;
using Lims.Domain.Core;
using Lims.Domain.Orders;
using Lims.Domain.Security;
using Lims.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Orders;

/// <summary>
/// Order registration and cancellation. Prices always come from the catalog; totals are computed here.
/// Order, invoice, lines and the first payment are written in one transaction.
/// </summary>
public sealed class OrderService(LimsDbContext db, INumberSequenceService numbers, ILabClock clock, BillingService billing)
{
    private sealed record PlannedTest(int Id, string Code, string NameEn, string? NameUr);
    private sealed record PlannedLine(InvoiceLineKind Kind, int? TestId, int? PackageId, string Code, string NameEn, string? NameUr, decimal Price, List<PlannedTest> Tests);

    private int BranchId => db.Request.ActiveBranchId
        ?? throw new BusinessRuleException("branch_required", "Select the branch you are working at before registering orders.");

    // ---------- Quote ----------

    /// <summary>Server-side price calculation for the order form (the form never computes totals itself).</summary>
    public async Task<Quote> QuoteAsync(QuoteRequest request, CancellationToken ct)
    {
        var lines = await PlanAsync(request.Items, BranchId, ct);
        var gross = InvoiceCalculator.Round(lines.Sum(l => l.Price));
        var discount = DiscountOf(request.Discount, gross, enforcePermission: false);
        return new Quote(lines.Select(ToQuote).ToList(), gross, discount.Amount, gross - discount.Amount);
    }

    // ---------- Create ----------

    public async Task<long> CreateAsync(OrderCreate input, CancellationToken ct)
    {
        if (input.IdempotencyKey == Guid.Empty)
            throw new BusinessRuleException("idempotency_key_required", "The order form is missing its submission key. Reload the page.");
        if (await ExistingByKeyAsync(input.IdempotencyKey, ct) is long existing) return existing;

        if (!Enum.IsDefined(input.Priority)) throw new BusinessRuleException("priority_invalid", "Choose a valid priority.");
        var branchId = BranchId;
        var orgId = db.Request.OrganizationId!.Value;
        if (!await db.Patients.AnyAsync(p => p.Id == input.PatientId, ct))
            throw new BusinessRuleException("patient_invalid", "Choose a registered patient.");
        if (input.ReferringDoctorId is int docId && !await db.Doctors.AnyAsync(d => d.Id == docId && d.IsActive, ct))
            throw new BusinessRuleException("doctor_invalid", "The referring doctor is not active.");

        var planned = await PlanAsync(input.Items, branchId, ct);
        var gross = InvoiceCalculator.Round(planned.Sum(l => l.Price));
        var discount = DiscountOf(input.Discount, gross, enforcePermission: true);

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var order = new LabOrder
            {
                OrganizationId = orgId, BranchId = branchId, PatientId = input.PatientId, ReferringDoctorId = input.ReferringDoctorId,
                Priority = input.Priority, ClinicalNotes = Blank(input.ClinicalNotes), IdempotencyKey = input.IdempotencyKey,
                OrderNo = await numbers.NextAsync(SequenceTypes.Order, branchId, ct),
            };
            var invoice = new Invoice
            {
                OrganizationId = orgId, BranchId = branchId, PatientId = input.PatientId,
                InvoiceNo = await numbers.NextAsync(SequenceTypes.Invoice, branchId, ct),
                DiscountPercent = discount.Percent, DiscountAmount = discount.Amount,
                DiscountReason = discount.Amount > 0 ? discount.Reason : null,
                DiscountByUserId = discount.Amount > 0 ? db.Request.UserId : null,
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
            invoice.OrderId = order.Id;
            db.Invoices.Add(invoice);

            // The invoice is complete and consistent before its first save (the database checks the amounts).
            var lineOf = new List<(PlannedLine Plan, InvoiceLine Line)>();
            foreach (var p in planned)
            {
                var line = new InvoiceLine
                {
                    OrganizationId = orgId, Kind = p.Kind, TestId = p.TestId, PackageId = p.PackageId, Code = p.Code,
                    NameEn = p.NameEn, NameUr = p.NameUr, Price = p.Price,
                };
                invoice.Lines.Add(line);
                lineOf.Add((p, line));
            }
            InvoiceCalculator.Recalculate(invoice);
            await db.SaveChangesAsync(ct);

            // Order items reference their invoice line, now that the lines have ids.
            foreach (var (p, line) in lineOf)
                foreach (var t in p.Tests)
                    order.Items.Add(new LabOrderItem
                    {
                        OrganizationId = orgId, InvoiceLineId = line.Id, TestId = t.Id, TestCode = t.Code, TestNameEn = t.NameEn, TestNameUr = t.NameUr,
                    });
            await db.SaveChangesAsync(ct);

            if (input.Payment is { } pay)
                await billing.ReceiveAsync(invoice, new PaymentCreate(Guid.NewGuid(), pay.Method, pay.Amount, pay.Reference), ct);

            await tx.CommitAsync(ct);
            return order.Id;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "IdempotencyKey"))
        {
            // Two identical submissions raced; the other one won. Return its order.
            db.ChangeTracker.Clear();
            if (await ExistingByKeyAsync(input.IdempotencyKey, ct) is long winner) return winner;
            throw;
        }
    }

    // ---------- Cancel ----------

    public async Task CancelLineAsync(long orderId, long lineId, string reason, CancellationToken ct)
    {
        var (order, invoice) = await LoadForChangeAsync(orderId, ct);
        var line = invoice.Lines.SingleOrDefault(l => l.Id == lineId) ?? throw BusinessRuleException.NotFound("Invoice line");
        await CancelLinesAsync(order, invoice, [line], reason, ct);
    }

    public async Task CancelOrderAsync(long orderId, string reason, CancellationToken ct)
    {
        var (order, invoice) = await LoadForChangeAsync(orderId, ct);
        var active = invoice.Lines.Where(l => !l.IsCancelled).ToList();
        if (active.Count == 0) throw BusinessRuleException.Conflict("order_already_cancelled", "This order is already cancelled.");
        await CancelLinesAsync(order, invoice, active, reason, ct);
    }

    private async Task CancelLinesAsync(LabOrder order, Invoice invoice, List<InvoiceLine> lines, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("cancel_reason_required", "Give a reason for the cancellation.");
        var now = clock.UtcNow;
        foreach (var line in lines)
        {
            if (line.IsCancelled) throw BusinessRuleException.Conflict("line_already_cancelled", "This test is already cancelled.");
            var items = order.Items.Where(i => i.InvoiceLineId == line.Id).ToList();
            // Once a sample is collected the test is in the laboratory's hands and can't simply be cancelled.
            if (items.Any(i => i.Status != OrderItemStatus.Ordered))
                throw BusinessRuleException.Conflict("line_in_progress", $"{line.Code} has already been collected or processed and can't be cancelled.");

            line.IsCancelled = true;
            line.CancelledAtUtc = now;
            line.CancelledByUserId = db.Request.UserId;
            line.CancelReason = reason.Trim();
            foreach (var i in items)
            {
                i.Status = OrderItemStatus.Cancelled;
                i.CancelledAtUtc = now;
                i.CancelledByUserId = db.Request.UserId;
            }
            db.AddBusinessAudit("Order.TestCancelled", nameof(LabOrder), order.Id, new { line.Code, line.Price }, reason.Trim());
        }

        if (order.Items.All(i => i.Status == OrderItemStatus.Cancelled))
        {
            order.Status = OrderStatus.Cancelled;
            order.CancelledAtUtc = now;
            order.CancelledByUserId = db.Request.UserId;
            order.CancelReason = reason.Trim();
        }
        InvoiceCalculator.Recalculate(invoice);
        await db.SaveChangesAsync(ct);
    }

    private async Task<(LabOrder, Invoice)> LoadForChangeAsync(long orderId, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId, ct) ?? throw BusinessRuleException.NotFound("Order");
        var invoice = await db.Invoices.Include(i => i.Lines).Include(i => i.Payments).Include(i => i.Refunds)
            .AsSplitQuery().SingleAsync(i => i.OrderId == orderId, ct);
        return (order, invoice);
    }

    // ---------- Read ----------

    public async Task<PagedResult<OrderListItem>> ListAsync(OrderQuery q, CancellationToken ct)
    {
        var query = from o in db.Orders.AsNoTracking()
                    join inv in db.Invoices.AsNoTracking() on o.Id equals inv.OrderId
                    join p in db.Patients on new { o.OrganizationId, Id = o.PatientId } equals new { p.OrganizationId, p.Id }
                    select new { o, inv, p };

        if (q.PatientId is int pid) query = query.Where(x => x.o.PatientId == pid);
        if (q.Status is OrderStatus st) query = query.Where(x => x.o.Status == st);
        if (q.Outstanding == true) query = query.Where(x => x.inv.Balance != 0);
        if (q.From is DateOnly from) { var f = clock.LocalDateStartUtc(from); query = query.Where(x => x.o.CreatedAtUtc >= f); }
        if (q.To is DateOnly to) { var t = clock.LocalDateStartUtc(to.AddDays(1)); query = query.Where(x => x.o.CreatedAtUtc < t); }
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(x => x.o.OrderNo == s.ToUpper() || x.inv.InvoiceNo == s.ToUpper() || x.p.PatientNo == s.ToUpper() ||
                                     x.p.FullName.Contains(s) || (x.p.Mobile != null && x.p.Mobile.Contains(s)));
        }

        query = (q.Sort, q.Desc) switch
        {
            ("balance", true) => query.OrderByDescending(x => x.inv.Balance),
            ("balance", false) => query.OrderBy(x => x.inv.Balance),
            (_, false) when q.Sort == "date" => query.OrderBy(x => x.o.Id),
            _ => query.OrderByDescending(x => x.o.Id),
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip(q.Skip).Take(q.PageSize)
            .Select(x => new OrderListItem(
                x.o.Id, x.o.OrderNo, x.o.CreatedAtUtc, x.p.Id, x.p.PatientNo, x.p.FullName, x.p.FullNameUr,
                db.Doctors.Where(d => d.Id == x.o.ReferringDoctorId).Select(d => d.FullName).FirstOrDefault(),
                x.o.Items.Count(i => i.Status != OrderItemStatus.Cancelled), x.inv.Net, x.inv.Balance, x.inv.Status, x.o.Status, x.o.Priority,
                db.Branches.Where(b => b.Id == x.o.BranchId).Select(b => b.NameEn).First()))
            .ToListAsync(ct);
        return new PagedResult<OrderListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<OrderDetail> GetAsync(long id, CancellationToken ct)
    {
        var o = await db.Orders.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Order");
        var invoice = await billing.InvoiceDtoAsync(o.Id, o.Items.ToList(), ct);
        var p = await db.Patients.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.OrganizationId == o.OrganizationId && x.Id == o.PatientId, ct);
        var branch = await db.Branches.AsNoTracking().Where(b => b.Id == o.BranchId).Select(b => new { b.NameEn, b.NameUr }).SingleAsync(ct);
        var doctor = o.ReferringDoctorId is int d ? await db.Doctors.IgnoreQueryFilters().Where(x => x.OrganizationId == o.OrganizationId && x.Id == d).Select(x => x.FullName).SingleOrDefaultAsync(ct) : null;
        var createdBy = o.CreatedByUserId is int u ? await db.Users.Where(x => x.Id == u).Select(x => x.DisplayNameEn).SingleOrDefaultAsync(ct) : null;

        return new OrderDetail(o.Id, o.OrderNo, o.CreatedAtUtc, createdBy, o.BranchId, branch.NameEn, branch.NameUr,
            new OrderPatient(p.Id, p.PatientNo, p.FullName, p.FullNameUr, p.Title, p.Sex, p.DateOfBirth, p.Mobile),
            o.ReferringDoctorId, doctor, o.Priority, o.ClinicalNotes, o.Status, o.CancelReason,
            o.Items.OrderBy(i => i.Id).Select(i => new OrderItemDto(i.Id, i.InvoiceLineId, i.TestCode, i.TestNameEn, i.TestNameUr, i.Status)).ToList(),
            invoice);
    }

    // ---------- Helpers ----------

    private async Task<List<PlannedLine>> PlanAsync(OrderLineRequest[] items, int branchId, CancellationToken ct)
    {
        if (items.Length == 0) throw new BusinessRuleException("items_required", "Add at least one test or package.");
        if (items.Any(i => (i.TestId is null) == (i.PackageId is null)))
            throw new BusinessRuleException("item_invalid", "Each item must be either a test or a package.");

        var testIds = items.Where(i => i.TestId is not null).Select(i => i.TestId!.Value).ToList();
        var packageIds = items.Where(i => i.PackageId is not null).Select(i => i.PackageId!.Value).ToList();

        var packages = await db.TestPackages.AsNoTracking().Include(p => p.Tests)
            .Where(p => packageIds.Contains(p.Id) && p.IsActive).ToDictionaryAsync(p => p.Id, ct);
        if (packages.Count != packageIds.Distinct().Count())
            throw new BusinessRuleException("package_invalid", "One or more packages are not available.");

        var allTestIds = testIds.Concat(packages.Values.SelectMany(p => p.Tests.Select(t => t.TestId))).Distinct().ToList();
        var tests = await db.Tests.AsNoTracking().Include(t => t.Branches)
            .Where(t => allTestIds.Contains(t.Id) && t.IsActive).ToDictionaryAsync(t => t.Id, ct);

        PlannedTest Test(int id)
        {
            if (!tests.TryGetValue(id, out var t)) throw new BusinessRuleException("test_invalid", "One or more tests are not available.");
            if (!t.AvailableAllBranches && t.Branches.All(b => b.BranchId != branchId))
                throw new BusinessRuleException("test_not_at_branch", $"{t.Code} is not offered at this branch.", details: new { t.Code });
            return new PlannedTest(t.Id, t.Code, t.NameEn, t.NameUr);
        }

        var lines = new List<PlannedLine>();
        foreach (var item in items)
        {
            if (item.TestId is int tid)
            {
                var t = Test(tid);
                lines.Add(new PlannedLine(InvoiceLineKind.Test, tid, null, t.Code, t.NameEn, t.NameUr, tests[tid].Price, [t]));
            }
            else
            {
                var p = packages[item.PackageId!.Value];
                lines.Add(new PlannedLine(InvoiceLineKind.Package, null, p.Id, p.Code, p.NameEn, p.NameUr, p.Price,
                    p.Tests.Select(pt => Test(pt.TestId)).ToList()));
            }
        }

        // The same test twice (e.g. alone and inside a package) would be billed and run twice.
        var duplicate = lines.SelectMany(l => l.Tests).GroupBy(t => t.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new BusinessRuleException("duplicate_test", $"{duplicate.First().Code} is included more than once.", details: new { duplicate.First().Code });
        return lines;
    }

    private sealed record DiscountResult(decimal? Percent, decimal Amount, string? Reason);

    private DiscountResult DiscountOf(DiscountRequest? d, decimal gross, bool enforcePermission)
    {
        if (d is null || (d.Percent is null && d.Amount is null)) return new DiscountResult(null, 0, null);
        if (enforcePermission && !db.Request.Can(Permissions.BillingDiscount))
            throw new BusinessRuleException("discount_forbidden", "You don't have permission to give discounts.", 403);
        if (d.Percent is not null && d.Amount is not null)
            throw new BusinessRuleException("discount_invalid", "Give the discount as a percentage or an amount, not both.");
        if (enforcePermission && string.IsNullOrWhiteSpace(d.Reason))
            throw new BusinessRuleException("discount_reason_required", "Give a reason for the discount.");
        if (d.Percent is decimal pct)
        {
            if (pct <= 0 || pct > 100) throw new BusinessRuleException("discount_invalid", "The discount percentage must be between 0 and 100.");
            return new DiscountResult(pct, InvoiceCalculator.Round(gross * pct / 100m), d.Reason?.Trim());
        }
        var amount = InvoiceCalculator.Round(d.Amount!.Value);
        if (amount <= 0 || amount > gross) throw new BusinessRuleException("discount_invalid", "The discount can't be more than the total.");
        return new DiscountResult(null, amount, d.Reason?.Trim());
    }

    private static QuoteLine ToQuote(PlannedLine l) =>
        new(l.Kind, l.TestId, l.PackageId, l.Code, l.NameEn, l.NameUr, l.Price, l.Tests.Select(t => t.Code).ToList());

    private async Task<long?> ExistingByKeyAsync(Guid key, CancellationToken ct) =>
        await db.Orders.IgnoreQueryFilters()
            .Where(o => o.OrganizationId == db.Request.OrganizationId && o.IdempotencyKey == key)
            .Select(o => (long?)o.Id).FirstOrDefaultAsync(ct);

    internal static bool IsUniqueViolation(DbUpdateException ex, string indexHint) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 } sql && sql.Message.Contains(indexHint, StringComparison.OrdinalIgnoreCase);

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
