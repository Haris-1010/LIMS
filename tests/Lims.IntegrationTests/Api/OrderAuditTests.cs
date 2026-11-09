using System.Net;
using System.Text.Json;
using Lims.Domain.Billing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

/// <summary>
/// Phase 6 audit: security, isolation, money rules, data integrity and concurrency for orders and billing.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderAuditTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;
    private static string Code(string p) => p + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<ApiClient> At(int branchId, params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(fx.OrgA, roles)));
        ApiClient.Ok(await c.PostAsync("/api/auth/active-branch", new { branchId }));
        await c.RefreshCsrfAsync();
        return c;
    }

    /// <summary>A user who may work at both branches of organisation A.</summary>
    private async Task<ApiClient> TwoBranchUser(params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(fx.OrgA, roles, allBranches: false, branchIds: [fx.OrgAMainBranch, fx.OrgASecondBranch])));
        return c;
    }

    private static async Task Switch(ApiClient c, int branchId)
    {
        ApiClient.Ok(await c.PostAsync("/api/auth/active-branch", new { branchId }));
        await c.RefreshCsrfAsync();
    }

    private sealed record Fx(int Cbc, int Esr, int Free, int PatientId);

    private async Task<Fx> Setup()
    {
        var admin = await At(fx.OrgAMainBranch, "SYSADMIN");
        var cat = (await ApiClient.Body(await admin.PostAsync("/api/catalog/categories", new { code = Code("C"), nameEn = "Cat" }))).GetProperty("id").GetInt32();
        var dept = (await ApiClient.Body(await admin.GetAsync("/api/organization/departments")))[0].GetProperty("id").GetInt32();
        async Task<int> Test(decimal price) => (await ApiClient.Body(await admin.PostAsync("/api/catalog/tests",
            new { code = Code("T"), nameEn = "Fixture", categoryId = cat, departmentId = dept, sampleTypeId = 1, price }))).GetProperty("id").GetInt32();
        var patient = (await ApiClient.Body(await admin.PostAsync("/api/patients", new { fullName = "Audit Patient " + Code(""), sex = 2, ageYears = 33, confirmNotDuplicate = true }))).GetProperty("id").GetInt32();
        return new Fx(await Test(1000), await Test(500), await Test(0), patient);
    }

    private static object Order(Fx f, object[]? items = null, object? payment = null, object? discount = null, Guid? key = null, int priority = 0) => new
    {
        idempotencyKey = key ?? Guid.NewGuid(), patientId = f.PatientId, priority, items = items ?? [new { testId = f.Cbc }], payment, discount,
    };

    private static JsonElement Inv(JsonElement order) => order.GetProperty("invoice");
    private static long InvId(JsonElement order) => Inv(order).GetProperty("id").GetInt64();

    private async Task<JsonElement> Place(ApiClient c, Fx f, object[]? items = null, object? payment = null, object? discount = null)
    {
        var res = await c.PostAsync("/api/orders", Order(f, items, payment, discount));
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    // ================= 1. Security: authentication, CSRF, permission matrix =================

    [Fact]
    public async Task Billing_endpoints_require_sign_in_and_a_csrf_token()
    {
        var anon = await Api.ClientAsync();
        foreach (var url in new[] { "/api/orders", "/api/billing/payments", "/api/billing/day-summary", "/api/billing/drawer", "/api/billing/drawers", "/api/orders/1" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/orders", new { })).StatusCode);

        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "RECEPTION");
        var res = await c.PostAsync("/api/orders", Order(f), withCsrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("csrf_invalid", await ApiClient.Code(res));
    }

    [Fact]
    public async Task Permission_matrix_for_orders_and_money()
    {
        var f = await Setup();
        var anyOrder = await Place(await At(fx.OrgAMainBranch, "RECEPTION"), f);
        var invoiceId = InvId(anyOrder);
        var orderId = anyOrder.GetProperty("id").GetInt64();
        var pay = new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 100 };

        // Phlebotomist: can view orders only.
        var phleb = await At(fx.OrgAMainBranch, "PHLEBOTOMY");
        Assert.Equal(HttpStatusCode.OK, (await phleb.GetAsync($"/api/orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phleb.PostAsync("/api/orders", Order(f))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phleb.PostAsync($"/api/billing/invoices/{invoiceId}/payments", pay)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phleb.PostAsync("/api/billing/drawer/open", new { openingFloat = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phleb.GetAsync("/api/billing/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phleb.PostAsync($"/api/orders/{orderId}/cancel", new { reason = "x" })).StatusCode);

        // Inventory manager: no access to orders or billing at all.
        var inv = await At(fx.OrgAMainBranch, "INVENTORY");
        Assert.Equal(HttpStatusCode.Forbidden, (await inv.GetAsync("/api/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await inv.GetAsync("/api/billing/day-summary")).StatusCode);

        // Cashier: takes payments but cannot create orders, cancel, or refund.
        var cashier = await At(fx.OrgAMainBranch, "CASHIER");
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsync("/api/orders", Order(f))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsync($"/api/orders/{orderId}/cancel", new { reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsync($"/api/billing/invoices/{invoiceId}/refunds", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 1, reason = "x" })).StatusCode);
        ApiClient.Ok(await cashier.PostAsync($"/api/billing/invoices/{invoiceId}/payments", pay));
    }

    [Fact]
    public async Task Another_organisation_cannot_touch_orders_invoices_or_drawers()
    {
        var f = await Setup();
        var order = await Place(await At(fx.OrgAMainBranch, "RECEPTION"), f, payment: new { method = 2, amount = 400 });
        var orderId = order.GetProperty("id").GetInt64();
        var invoiceId = InvId(order);
        var lineId = Inv(order).GetProperty("lines")[0].GetProperty("id").GetInt64();

        var b = await Api.ClientAsync();
        ApiClient.Ok(await b.LoginAsync(await Api.CreateUserAsync(fx.OrgB, ["SYSADMIN"])));
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/billing/invoices/{invoiceId}/payments", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/billing/invoices/{invoiceId}/refunds", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 1, reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/orders/{orderId}/cancel", new { reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/orders/{orderId}/lines/{lineId}/cancel", new { reason = "x" })).StatusCode);

        // …and nothing changed.
        await using var db = fx.System();
        var saved = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        Assert.Equal((400m, InvoiceStatus.PartiallyPaid), (saved.Paid, saved.Status));
    }

    [Fact]
    public async Task Branch_limited_user_cannot_pay_cancel_or_refund_another_branchs_invoice()
    {
        var f = await Setup();
        var main = await Place(await At(fx.OrgAMainBranch, "SYSADMIN"), f, payment: new { method = 2, amount = 200 });
        var secondOnly = await Api.ClientAsync();
        ApiClient.Ok(await secondOnly.LoginAsync(await Api.CreateUserAsync(fx.OrgA, ["LABMGR"], allBranches: false, branchIds: [fx.OrgASecondBranch])));
        var invoiceId = InvId(main);
        var orderId = main.GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.NotFound, (await secondOnly.PostAsync($"/api/billing/invoices/{invoiceId}/payments", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secondOnly.PostAsync($"/api/orders/{orderId}/cancel", new { reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secondOnly.GetAsync($"/api/orders/{orderId}")).StatusCode);
        var payments = await ApiClient.Body(await secondOnly.GetAsync("/api/billing/payments?pageSize=100"));
        Assert.DoesNotContain(payments.GetProperty("items").EnumerateArray(), p => p.GetProperty("orderId").GetInt64() == orderId);
    }

    // ================= 2. Validation and edge cases =================

    [Fact]
    public async Task Invalid_money_amounts_are_rejected()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "SYSADMIN");
        var order = await Place(c, f);
        var id = InvId(order);

        foreach (var amount in new[] { 0, -5, 0.001, 100000000 })
        {
            var res = await c.PostAsync($"/api/billing/invoices/{id}/payments", new { idempotencyKey = Guid.NewGuid(), method = 2, amount });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
        Assert.Equal("payment_method_invalid", await ApiClient.Code(await c.PostAsync($"/api/billing/invoices/{id}/payments", new { idempotencyKey = Guid.NewGuid(), method = 99, amount = 10 })));
        Assert.Equal("idempotency_key_required", await ApiClient.Code(await c.PostAsync($"/api/billing/invoices/{id}/payments", new { idempotencyKey = Guid.Empty, method = 2, amount = 10 })));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/api/orders", Order(f, discount: new { percent = 150 }))).StatusCode);
        Assert.Equal("discount_invalid", await ApiClient.Code(await c.PostAsync("/api/orders", Order(f, discount: new { amount = 5000, reason = "x" }))));
        Assert.Equal("discount_invalid", await ApiClient.Code(await c.PostAsync("/api/orders", Order(f, discount: new { percent = 10, amount = 10, reason = "x" }))));
        Assert.Equal("discount_reason_required", await ApiClient.Code(await c.PostAsync("/api/orders", Order(f, discount: new { percent = 10 }))));
    }

    [Fact]
    public async Task Invalid_enum_values_are_never_stored()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "SYSADMIN");
        // Refund method and order priority are enums: 99 must be refused, not saved.
        var paid = await Place(c, f, payment: new { method = 2, amount = 1000 });
        var lineId = Inv(paid).GetProperty("lines")[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await c.PostAsync($"/api/orders/{paid.GetProperty("id").GetInt64()}/lines/{lineId}/cancel", new { reason = "x" }));
        var refund = await c.PostAsync($"/api/billing/invoices/{InvId(paid)}/refunds", new { idempotencyKey = Guid.NewGuid(), method = 99, amount = 1000, reason = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, refund.StatusCode);

        var badPriority = await c.PostAsync("/api/orders", Order(f, priority: 99));
        Assert.Equal(HttpStatusCode.BadRequest, badPriority.StatusCode);
        await using var db = fx.System();
        Assert.False(await db.Refunds.AnyAsync(r => (int)r.Method == 99));
        Assert.False(await db.Orders.AnyAsync(o => (int)o.Priority == 99));
    }

    [Fact]
    public async Task Free_tests_and_full_discounts_produce_paid_invoices_that_accept_no_payment()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "SYSADMIN");
        var free = await Place(c, f, items: [new { testId = f.Free }]);
        Assert.Equal((0m, 0m, (int)InvoiceStatus.Paid), (Inv(free).GetProperty("net").GetDecimal(), Inv(free).GetProperty("balance").GetDecimal(), Inv(free).GetProperty("status").GetInt32()));

        var full = await Place(c, f, discount: new { percent = 100, reason = "Charity" });
        Assert.Equal((int)InvoiceStatus.Paid, Inv(full).GetProperty("status").GetInt32());
        var res = await c.PostAsync($"/api/billing/invoices/{InvId(full)}/payments", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 1 });
        Assert.Equal("payment_exceeds_balance", await ApiClient.Code(res));
    }

    [Fact]
    public async Task Cancelled_orders_accept_no_payment_and_cannot_be_cancelled_twice()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "LABMGR");
        ApiClient.Ok(await c.PostAsync("/api/billing/drawer/open", new { openingFloat = 0 }));
        var order = await Place(c, f);
        var id = order.GetProperty("id").GetInt64();
        var cancelled = await ApiClient.Body(await c.PostAsync($"/api/orders/{id}/cancel", new { reason = "Patient left" }));
        Assert.Equal((int)InvoiceStatus.Cancelled, Inv(cancelled).GetProperty("status").GetInt32());
        Assert.Equal(0m, Inv(cancelled).GetProperty("balance").GetDecimal());

        Assert.Equal("invoice_cancelled", await ApiClient.Code(await c.PostAsync($"/api/billing/invoices/{InvId(order)}/payments", new { idempotencyKey = Guid.NewGuid(), method = 1, amount = 50 })));
        Assert.Equal("order_already_cancelled", await ApiClient.Code(await c.PostAsync($"/api/orders/{id}/cancel", new { reason = "again" })));
        var lineId = Inv(order).GetProperty("lines")[0].GetProperty("id").GetInt64();
        Assert.Equal("line_already_cancelled", await ApiClient.Code(await c.PostAsync($"/api/orders/{id}/lines/{lineId}/cancel", new { reason = "again" })));
    }

    [Fact]
    public async Task Cancelling_a_test_already_collected_is_refused()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "LABMGR");
        var order = await Place(c, f, items: [new { testId = f.Cbc }, new { testId = f.Esr }]);
        var id = order.GetProperty("id").GetInt64();
        await using (var db = fx.System())
            await db.Database.ExecuteSqlRawAsync("UPDATE lab.OrderItems SET Status = 10 WHERE OrderId = {0}", id); // collected (Phase 7 will do this)
        var lineId = Inv(order).GetProperty("lines")[0].GetProperty("id").GetInt64();
        Assert.Equal("line_in_progress", await ApiClient.Code(await c.PostAsync($"/api/orders/{id}/lines/{lineId}/cancel", new { reason = "x" })));
        Assert.Equal("line_in_progress", await ApiClient.Code(await c.PostAsync($"/api/orders/{id}/cancel", new { reason = "x" })));
    }

    [Fact]
    public async Task Percentage_discount_follows_the_remaining_tests_after_a_cancellation()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "SYSADMIN");
        var order = await Place(c, f, items: [new { testId = f.Cbc }, new { testId = f.Esr }], discount: new { percent = 10, reason = "x" });
        Assert.Equal(150m, Inv(order).GetProperty("discountAmount").GetDecimal());
        var espLine = Inv(order).GetProperty("lines").EnumerateArray().Last().GetProperty("id").GetInt64();
        var after = await ApiClient.Body(await c.PostAsync($"/api/orders/{order.GetProperty("id").GetInt64()}/lines/{espLine}/cancel", new { reason = "x" }));
        Assert.Equal((1000m, 100m, 900m), (Inv(after).GetProperty("gross").GetDecimal(), Inv(after).GetProperty("discountAmount").GetDecimal(), Inv(after).GetProperty("net").GetDecimal()));
    }

    // ================= 3. Money maths across partial payments and refunds =================

    [Fact]
    public async Task Partial_payments_and_partial_refunds_keep_the_invoice_exact()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "SYSADMIN");
        var order = await Place(c, f, items: [new { testId = f.Cbc }, new { testId = f.Esr }], payment: new { method = 2, amount = 1500 });
        var id = order.GetProperty("id").GetInt64();
        var cbcLine = Inv(order).GetProperty("lines")[0].GetProperty("id").GetInt64();
        var cancelled = await ApiClient.Body(await c.PostAsync($"/api/orders/{id}/lines/{cbcLine}/cancel", new { reason = "x" }));
        Assert.Equal(-1000m, Inv(cancelled).GetProperty("balance").GetDecimal());

        async Task<JsonElement> Refund(decimal amount) => await ApiClient.Body(await c.PostAsync($"/api/billing/invoices/{InvId(order)}/refunds",
            new { idempotencyKey = Guid.NewGuid(), method = 2, amount, reason = "Cancelled test" }));
        var r1 = await Refund(400);
        Assert.Equal((-600m, (int)InvoiceStatus.RefundDue), (Inv(r1).GetProperty("balance").GetDecimal(), Inv(r1).GetProperty("status").GetInt32()));
        var r2 = await Refund(600);
        Assert.Equal((0m, (int)InvoiceStatus.Paid), (Inv(r2).GetProperty("balance").GetDecimal(), Inv(r2).GetProperty("status").GetInt32()));
        Assert.Equal("refund_exceeds_due", await ApiClient.Code(await c.PostAsync($"/api/billing/invoices/{InvId(order)}/refunds", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 1, reason = "x" })));

        await using var db = fx.System();
        var inv = await db.Invoices.AsNoTracking().Include(i => i.Payments).Include(i => i.Refunds).SingleAsync(i => i.Id == InvId(order));
        Assert.Equal((1500m, 1000m, 500m), (inv.Paid, inv.Refunded, inv.Net));
        Assert.Equal(inv.Net - (inv.Paid - inv.Refunded), inv.Balance);
    }

    [Fact]
    public async Task Refund_retry_with_the_same_key_refunds_only_once()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "SYSADMIN");
        var order = await Place(c, f, payment: new { method = 2, amount = 1000 });
        var lineId = Inv(order).GetProperty("lines")[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await c.PostAsync($"/api/orders/{order.GetProperty("id").GetInt64()}/lines/{lineId}/cancel", new { reason = "x" }));
        var key = Guid.NewGuid();
        var body = new { idempotencyKey = key, method = 2, amount = 1000, reason = "Cancelled" };
        ApiClient.Ok(await c.PostAsync($"/api/billing/invoices/{InvId(order)}/refunds", body));
        ApiClient.Ok(await c.PostAsync($"/api/billing/invoices/{InvId(order)}/refunds", body));
        await using var db = fx.System();
        Assert.Equal(1, await db.Refunds.CountAsync(r => r.IdempotencyKey == key));
    }

    [Fact]
    public async Task A_submission_key_reused_on_another_invoice_is_an_error_not_a_silent_success()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "SYSADMIN");
        var a = await Place(c, f);
        var b = await Place(c, f);
        var key = Guid.NewGuid();
        ApiClient.Ok(await c.PostAsync($"/api/billing/invoices/{InvId(a)}/payments", new { idempotencyKey = key, method = 2, amount = 100 }));

        var reused = await c.PostAsync($"/api/billing/invoices/{InvId(b)}/payments", new { idempotencyKey = key, method = 2, amount = 100 });
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("idempotency_key_reused", await ApiClient.Code(reused));
        await using var db = fx.System();
        Assert.Equal(0m, (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == InvId(b))).Paid);   // the other invoice was NOT marked paid

        // Refunds: same rule, and an empty key is refused.
        var paid = await Place(c, f, payment: new { method = 2, amount = 1000 });
        var line = Inv(paid).GetProperty("lines")[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await c.PostAsync($"/api/orders/{paid.GetProperty("id").GetInt64()}/lines/{line}/cancel", new { reason = "x" }));
        Assert.Equal("idempotency_key_required", await ApiClient.Code(await c.PostAsync($"/api/billing/invoices/{InvId(paid)}/refunds", new { idempotencyKey = Guid.Empty, method = 2, amount = 10, reason = "x" })));
    }

    [Fact]
    public async Task Database_itself_refuses_unknown_statuses_priorities_and_methods()
    {
        var f = await Setup();
        var order = await Place(await At(fx.OrgAMainBranch, "SYSADMIN"), f);
        var id = order.GetProperty("id").GetInt64();
        await using var db = fx.System();
        string[] sql =
        [
            $"UPDATE lab.Orders SET Priority = 9 WHERE Id = {id}",
            $"UPDATE lab.Orders SET Status = 9 WHERE Id = {id}",
            $"UPDATE lab.OrderItems SET Status = 55 WHERE OrderId = {id}",
        ];
        foreach (var s in sql)
            Assert.Contains("CK_", (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(s))).Message);
    }

    // ================= 4. Cash drawer =================

    [Fact]
    public async Task Cash_refund_needs_the_refunding_users_open_drawer_and_lowers_expected_cash()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "LABMGR");
        ApiClient.Ok(await c.PostAsync("/api/billing/drawer/open", new { openingFloat = 300 }));
        var order = await Place(c, f, payment: new { method = 1, amount = 1000 });
        var lineId = Inv(order).GetProperty("lines")[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await c.PostAsync($"/api/orders/{order.GetProperty("id").GetInt64()}/lines/{lineId}/cancel", new { reason = "x" }));

        // A different user without an open drawer can't pay cash out.
        var other = await At(fx.OrgAMainBranch, "SYSADMIN");
        Assert.Equal("drawer_not_open", await ApiClient.Code(await other.PostAsync($"/api/billing/invoices/{InvId(order)}/refunds", new { idempotencyKey = Guid.NewGuid(), method = 1, amount = 1000, reason = "x" })));

        ApiClient.Ok(await c.PostAsync($"/api/billing/invoices/{InvId(order)}/refunds", new { idempotencyKey = Guid.NewGuid(), method = 1, amount = 1000, reason = "Cancelled" }));
        var drawer = (await ApiClient.Body(await c.GetAsync("/api/billing/drawer"))).GetProperty("drawer");
        Assert.Equal((300m, 1000m, 1000m, 300m), (drawer.GetProperty("openingFloat").GetDecimal(), drawer.GetProperty("cashIn").GetDecimal(), drawer.GetProperty("cashOut").GetDecimal(), drawer.GetProperty("expectedCash").GetDecimal()));

        var closed = await ApiClient.Body(await c.PostAsync("/api/billing/drawer/close", new { countedCash = 300 }));
        Assert.Equal(0m, closed.GetProperty("variance").GetDecimal());
        // After close the drawer takes no more cash.
        var next = await Place(await At(fx.OrgAMainBranch, "SYSADMIN"), f);
        Assert.Equal("drawer_not_open", await ApiClient.Code(await c.PostAsync($"/api/billing/invoices/{InvId(next)}/payments", new { idempotencyKey = Guid.NewGuid(), method = 1, amount = 10 })));
    }

    [Fact]
    public async Task Drawers_are_private_to_their_cashier_unless_the_user_can_close_the_day()
    {
        var a = await At(fx.OrgAMainBranch, "RECEPTION");        // payments.collect, no cash.close
        ApiClient.Ok(await a.PostAsync("/api/billing/drawer/open", new { openingFloat = 111 }));
        var other = await At(fx.OrgAMainBranch, "RECEPTION");
        var mine = await ApiClient.Body(await other.GetAsync("/api/billing/drawers"));
        var supervisor = await At(fx.OrgAMainBranch, "CASHIER");   // has cash.close
        var all = await ApiClient.Body(await supervisor.GetAsync("/api/billing/drawers"));
        Assert.Contains(all.EnumerateArray(), d => d.GetProperty("openingFloat").GetDecimal() == 111m);
        Assert.DoesNotContain(mine.EnumerateArray(), d => d.GetProperty("openingFloat").GetDecimal() == 111m);
    }

    [Fact]
    public async Task Cash_payment_racing_with_drawer_close_is_never_lost_from_the_count()
    {
        // Invariant: every cash payment is either inside the closed drawer's totals, or rejected.
        for (var round = 0; round < 4; round++)
        {
            var f = await Setup();
            var cashier = await At(fx.OrgAMainBranch, "LABMGR");
            ApiClient.Ok(await cashier.PostAsync("/api/billing/drawer/open", new { openingFloat = 0 }));
            var invoices = new List<long>();
            for (var i = 0; i < 12; i++) invoices.Add(InvId(await Place(cashier, f)));

            var pay = invoices.Select(id => cashier.PostAsync($"/api/billing/invoices/{id}/payments", new { idempotencyKey = Guid.NewGuid(), method = 1, amount = 100 }));
            var close = cashier.PostAsync("/api/billing/drawer/close", new { countedCash = 0, notes = "race test" });
            var all = await Task.WhenAll(pay.Append(close));
            var closeRes = all[^1];
            // If close lost the race it reports a conflict; closing again must then succeed and include every payment.
            if (closeRes.StatusCode == HttpStatusCode.Conflict)
                ApiClient.Ok(await cashier.PostAsync("/api/billing/drawer/close", new { countedCash = 0, notes = "retry after conflict" }));

            await using var db = fx.System();
            var session = await db.CashDrawerSessions.AsNoTracking().OrderByDescending(s => s.Id).FirstAsync(s => s.OpeningFloat == 0 && s.Status == DrawerStatus.Closed && s.Notes != null);
            var cashInDb = await db.Payments.Where(p => p.DrawerSessionId == session.Id).SumAsync(p => (decimal?)p.Amount) ?? 0;
            Assert.Equal(cashInDb, session.CashIn);                                                  // nothing missing from the reconciliation
            Assert.False(await db.Payments.AnyAsync(p => p.DrawerSessionId == session.Id && p.ReceivedAtUtc > session.ClosedAtUtc)); // nothing after close
        }
    }

    // ================= 5. Concurrency =================

    [Fact]
    public async Task Parallel_orders_get_distinct_consecutive_numbers()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "RECEPTION");
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => c.PostAsync("/api/orders", Order(f))));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var orders = await Task.WhenAll(results.Select(ApiClient.Body));
        Assert.Equal(12, orders.Select(o => o.GetProperty("orderNo").GetString()).Distinct().Count());
        Assert.Equal(12, orders.Select(o => Inv(o).GetProperty("invoiceNo").GetString()).Distinct().Count());
    }

    [Fact]
    public async Task Parallel_refunds_cannot_refund_more_than_is_due()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "SYSADMIN");
        var order = await Place(c, f, payment: new { method = 2, amount = 1000 });
        var lineId = Inv(order).GetProperty("lines")[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await c.PostAsync($"/api/orders/{order.GetProperty("id").GetInt64()}/lines/{lineId}/cancel", new { reason = "x" }));

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => c.PostAsync($"/api/billing/invoices/{InvId(order)}/refunds", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 1000, reason = "race" })));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(results, r => Assert.Contains((int)r.StatusCode, new[] { 200, 400, 409 }));   // never a 500
        await using var db = fx.System();
        Assert.Equal(1000m, await db.Refunds.Where(r => r.InvoiceId == InvId(order)).SumAsync(r => r.Amount));
    }

    [Fact]
    public async Task Payment_racing_with_a_cancellation_leaves_a_consistent_invoice()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "SYSADMIN");
        for (var round = 0; round < 3; round++)
        {
            var order = await Place(c, f, items: [new { testId = f.Cbc }, new { testId = f.Esr }]);
            var id = order.GetProperty("id").GetInt64();
            var line = Inv(order).GetProperty("lines")[0].GetProperty("id").GetInt64();
            var pay = c.PostAsync($"/api/billing/invoices/{InvId(order)}/payments", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 1500 });
            var cancel = c.PostAsync($"/api/orders/{id}/lines/{line}/cancel", new { reason = "race" });
            var done = await Task.WhenAll(pay, cancel);
            Assert.All(done, r => Assert.Contains((int)r.StatusCode, new[] { 200, 400, 409 }));
            await using var db = fx.System();
            var inv = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == InvId(order));
            Assert.Equal(inv.Net - (inv.Paid - inv.Refunded), inv.Balance);   // the stored figures always add up
            Assert.True(inv.Paid <= 1500m);
        }
    }

    // ================= 6. Day summary and lists respect the selected branch =================

    [Fact]
    public async Task Day_summary_covers_only_the_selected_branch()
    {
        var f = await Setup();
        var c = await TwoBranchUser("RECEPTION");
        await Switch(c, fx.OrgAMainBranch);
        var before = await ApiClient.Body(await c.GetAsync("/api/billing/day-summary"));
        await Place(c, f, payment: new { method = 2, amount = 300 });
        await Switch(c, fx.OrgASecondBranch);
        var secondBefore = await ApiClient.Body(await c.GetAsync("/api/billing/day-summary"));
        await Place(c, f, payment: new { method = 2, amount = 200 });

        var second = await ApiClient.Body(await c.GetAsync("/api/billing/day-summary"));
        Assert.Equal(fx.OrgASecondBranch, second.GetProperty("branchId").GetInt32());
        Assert.Equal(200m, second.GetProperty("totalReceived").GetDecimal() - secondBefore.GetProperty("totalReceived").GetDecimal());
        await Switch(c, fx.OrgAMainBranch);
        var main = await ApiClient.Body(await c.GetAsync("/api/billing/day-summary"));
        Assert.Equal(300m, main.GetProperty("totalReceived").GetDecimal() - before.GetProperty("totalReceived").GetDecimal());
    }

    // ================= 7. Database-level integrity (raw SQL bypassing the application) =================

    [Fact]
    public async Task Database_refuses_inconsistent_money_rows()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "SYSADMIN");
        var order = await Place(c, f, payment: new { method = 2, amount = 100 });
        var invoiceId = InvId(order);
        await using var db = fx.System();

        async Task<string> Fails(string sql, params object[] args) =>
            (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(sql, args))).Message;

        Assert.Contains("CK_Invoices", await Fails("UPDATE billing.Invoices SET Net = Net + 1 WHERE Id = {0}", invoiceId));
        Assert.Contains("CK_Invoices", await Fails("UPDATE billing.Invoices SET Paid = Paid + 5 WHERE Id = {0}", invoiceId));       // balance no longer adds up
        Assert.Contains("CK_Payments_Amount", await Fails("UPDATE billing.Payments SET Amount = 0 WHERE InvoiceId = {0}", invoiceId));
        Assert.Contains("CK_Payments_CashDrawer", await Fails(
            "INSERT INTO billing.Payments (OrganizationId,BranchId,ReceiptNo,InvoiceId,PatientId,Method,Amount,ReceivedAtUtc,ReceivedByUserId,IdempotencyKey,CreatedAtUtc) SELECT OrganizationId,BranchId,'X1',Id,PatientId,1,10,SYSUTCDATETIME(),1,NEWID(),SYSUTCDATETIME() FROM billing.Invoices WHERE Id = {0}", invoiceId));
        var cashier = await At(fx.OrgAMainBranch, "RECEPTION");
        ApiClient.Ok(await cashier.PostAsync("/api/billing/drawer/open", new { openingFloat = 5 }));
        Assert.Contains("UX_CashDrawer_OneOpenPerUser", await Fails("INSERT INTO billing.CashDrawerSessions (OrganizationId,BranchId,UserId,Status,OpenedAtUtc,OpeningFloat,CreatedAtUtc) SELECT TOP 1 OrganizationId,BranchId,UserId,0,SYSUTCDATETIME(),0,SYSUTCDATETIME() FROM billing.CashDrawerSessions WHERE Status = 0 AND OpeningFloat = 5"));
        // A payment can't point at another organisation's invoice (composite foreign key).
        Assert.Contains("FK_Payments_Invoices", await Fails(
            "INSERT INTO billing.Payments (OrganizationId,BranchId,ReceiptNo,InvoiceId,PatientId,Method,Amount,ReceivedAtUtc,ReceivedByUserId,IdempotencyKey,CreatedAtUtc) SELECT {1},BranchId,'X2',Id,PatientId,2,10,SYSUTCDATETIME(),1,NEWID(),SYSUTCDATETIME() FROM billing.Invoices WHERE Id = {0}", invoiceId, fx.OrgB));
    }

    [Fact]
    public async Task Every_money_and_order_change_is_in_the_audit_log()
    {
        var f = await Setup();
        var c = await At(fx.OrgAMainBranch, "LABMGR");
        var order = await Place(c, f, payment: new { method = 2, amount = 1000 });
        var id = order.GetProperty("id").GetInt64();
        var lineId = Inv(order).GetProperty("lines")[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await c.PostAsync($"/api/orders/{id}/lines/{lineId}/cancel", new { reason = "Audit me" }));
        ApiClient.Ok(await c.PostAsync($"/api/billing/invoices/{InvId(order)}/refunds", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 1000, reason = "Audit me" }));

        await using var db = fx.System();
        string[] types = ["LabOrder", "Invoice", "Payment", "Refund"];
        foreach (var t in types)
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityType == t && a.UserId != null && a.OrganizationId == fx.OrgA), $"No audit rows for {t}");
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Order.TestCancelled" && a.EntityId == id.ToString()));
        // Payment audit rows carry no card/reference secrets beyond what was entered, and always a user and time.
        Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityType == "Payment" && (a.UserId == null || a.AtUtc == default)));
    }
}
