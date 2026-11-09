using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

/// <summary>Order, invoice, payment, refund and cash drawer rules through the real API.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderApiTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;
    private static string Code(string p) => p + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    /// <summary>Signs in and selects a working branch (orders and payments need one).</summary>
    private async Task<ApiClient> At(int branchId, params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(fx.OrgA, roles)));
        ApiClient.Ok(await c.PostAsync("/api/auth/active-branch", new { branchId }));
        await c.RefreshCsrfAsync();
        return c;
    }

    private sealed record Catalog(int Cbc, int Esr, int Lft, int Package, int PatientId);

    /// <summary>Two tests at 1,200 and 400, a third at 900, a package (CBC+ESR) at 1,400, and a patient.</summary>
    private async Task<Catalog> Setup()
    {
        var admin = await At(fx.OrgAMainBranch, "SYSADMIN");
        var cat = (await ApiClient.Body(await admin.PostAsync("/api/catalog/categories", new { code = Code("C"), nameEn = "Cat" }))).GetProperty("id").GetInt32();
        var dept = (await ApiClient.Body(await admin.GetAsync("/api/organization/departments")))[0].GetProperty("id").GetInt32();
        async Task<int> Test(decimal price) => (await ApiClient.Body(await admin.PostAsync("/api/catalog/tests",
            new { code = Code("T"), nameEn = "Fixture", categoryId = cat, departmentId = dept, sampleTypeId = 1, price }))).GetProperty("id").GetInt32();
        var cbc = await Test(1200);
        var esr = await Test(400);
        var lft = await Test(900);
        var pkg = (await ApiClient.Body(await admin.PostAsync("/api/catalog/packages", new { code = Code("K"), nameEn = "Pkg", price = 1400, testIds = new[] { cbc, esr } }))).GetProperty("id").GetInt32();
        var patient = (await ApiClient.Body(await admin.PostAsync("/api/patients", new { fullName = "Order Patient " + Code(""), sex = 1, ageYears = 40, confirmNotDuplicate = true }))).GetProperty("id").GetInt32();
        return new Catalog(cbc, esr, lft, pkg, patient);
    }

    private static object OrderBody(Catalog c, Guid? key = null, object? discount = null, object? payment = null, object[]? items = null) => new
    {
        idempotencyKey = key ?? Guid.NewGuid(),
        patientId = c.PatientId,
        priority = 0,
        items = items ?? new object[] { new { packageId = c.Package }, new { testId = c.Lft } },
        discount,
        payment,
    };

    private static JsonElement Invoice(JsonElement order) => order.GetProperty("invoice");

    [Fact]
    public async Task Order_with_package_discount_and_cash_payment_is_priced_by_the_server()
    {
        var c = await Setup();
        var admin = await At(fx.OrgAMainBranch, "SYSADMIN");
        ApiClient.Ok(await admin.PostAsync("/api/billing/drawer/open", new { openingFloat = 500 }));

        var quote = await ApiClient.Body(await admin.PostAsync("/api/orders/quote", new { items = new object[] { new { packageId = c.Package }, new { testId = c.Lft } }, discount = new { percent = 10 } }));
        Assert.Equal(2300m, quote.GetProperty("gross").GetDecimal());
        Assert.Equal(2070m, quote.GetProperty("net").GetDecimal());

        var res = await admin.PostAsync("/api/orders", OrderBody(c, discount: new { percent = 10, reason = "Staff family" }, payment: new { method = 1, amount = 2000 }));
        ApiClient.Ok(res);
        var order = await ApiClient.Body(res);
        var inv = Invoice(order);
        Assert.Matches(@"^ORD-\d{4}-\d{5}$", order.GetProperty("orderNo").GetString());
        Assert.Equal(3, order.GetProperty("items").GetArrayLength());            // package expands to its 2 tests + LFT
        Assert.Equal((2300m, 230m, 2070m, 2000m, 70m), (inv.GetProperty("gross").GetDecimal(), inv.GetProperty("discountAmount").GetDecimal(),
            inv.GetProperty("net").GetDecimal(), inv.GetProperty("paid").GetDecimal(), inv.GetProperty("balance").GetDecimal()));
        Assert.Equal(1, inv.GetProperty("status").GetInt32());                    // partially paid
        Assert.Matches(@"^RCP-", inv.GetProperty("payments")[0].GetProperty("receiptNo").GetString());

        var drawer = (await ApiClient.Body(await admin.GetAsync("/api/billing/drawer"))).GetProperty("drawer");
        Assert.Equal(2500m, drawer.GetProperty("expectedCash").GetDecimal());   // float 500 + 2000 cash
    }

    [Fact]
    public async Task Same_submission_key_never_creates_a_second_order()
    {
        var c = await Setup();
        var reception = await At(fx.OrgAMainBranch, "RECEPTION");
        var key = Guid.NewGuid();

        var first = await ApiClient.Body(await reception.PostAsync("/api/orders", OrderBody(c, key)));
        var again = await ApiClient.Body(await reception.PostAsync("/api/orders", OrderBody(c, key)));
        Assert.Equal(first.GetProperty("id").GetInt64(), again.GetProperty("id").GetInt64());

        // Five simultaneous double-clicks with one key.
        var key2 = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => reception.PostAsync("/api/orders", OrderBody(c, key2))));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var ids = await Task.WhenAll(results.Select(async r => (await ApiClient.Body(r)).GetProperty("id").GetInt64()));
        Assert.Single(ids.Distinct());
        await using var db = fx.System();
        Assert.Equal(1, await db.Orders.CountAsync(o => o.IdempotencyKey == key2));
    }

    [Fact]
    public async Task Same_test_twice_is_rejected()
    {
        var c = await Setup();
        var reception = await At(fx.OrgAMainBranch, "RECEPTION");
        var res = await reception.PostAsync("/api/orders", OrderBody(c, items: new object[] { new { packageId = c.Package }, new { testId = c.Cbc } }));
        Assert.Equal("duplicate_test", await ApiClient.Code(res));
    }

    [Fact]
    public async Task Cash_needs_an_open_drawer_and_discounts_need_permission()
    {
        var c = await Setup();
        var reception = await At(fx.OrgAMainBranch, "RECEPTION");      // payments.collect, no billing.discount
        var noDrawer = await reception.PostAsync("/api/orders", OrderBody(c, payment: new { method = 1, amount = 100 }));
        Assert.Equal("drawer_not_open", await ApiClient.Code(noDrawer));

        // Card payments don't go through the drawer.
        ApiClient.Ok(await reception.PostAsync("/api/orders", OrderBody(c, payment: new { method = 2, amount = 100, reference = "1234" })));

        var discount = await reception.PostAsync("/api/orders", OrderBody(c, discount: new { amount = 100, reason = "x" }));
        Assert.Equal(HttpStatusCode.Forbidden, discount.StatusCode);
        Assert.Equal("discount_forbidden", await ApiClient.Code(discount));

        // Nothing was half-saved by the failed attempts.
        await using var db = fx.System();
        Assert.Equal(1, await db.Orders.CountAsync(o => o.PatientId == c.PatientId));
    }

    [Fact]
    public async Task Overpayment_is_refused_and_payment_retries_are_idempotent()
    {
        var c = await Setup();
        var reception = await At(fx.OrgAMainBranch, "RECEPTION");
        var order = await ApiClient.Body(await reception.PostAsync("/api/orders", OrderBody(c)));
        var invoiceId = Invoice(order).GetProperty("id").GetInt64();

        var over = await reception.PostAsync($"/api/billing/invoices/{invoiceId}/payments", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 999999 });
        Assert.Equal("payment_exceeds_balance", await ApiClient.Code(over));

        var key = Guid.NewGuid();
        ApiClient.Ok(await reception.PostAsync($"/api/billing/invoices/{invoiceId}/payments", new { idempotencyKey = key, method = 2, amount = 500 }));
        var retry = await ApiClient.Body(await reception.PostAsync($"/api/billing/invoices/{invoiceId}/payments", new { idempotencyKey = key, method = 2, amount = 500 }));
        Assert.Equal(500m, Invoice(retry).GetProperty("paid").GetDecimal());
        Assert.Single(Invoice(retry).GetProperty("payments").EnumerateArray());
    }

    [Fact]
    public async Task Parallel_payments_cannot_collect_more_than_the_balance()
    {
        var c = await Setup();
        var reception = await At(fx.OrgAMainBranch, "RECEPTION");
        var order = await ApiClient.Body(await reception.PostAsync("/api/orders", OrderBody(c, items: new object[] { new { testId = c.Esr } })));
        var invoiceId = Invoice(order).GetProperty("id").GetInt64();

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            reception.PostAsync($"/api/billing/invoices/{invoiceId}/payments", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 400 })));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);

        await using var db = fx.System();
        var inv = await db.Invoices.SingleAsync(i => i.Id == invoiceId);
        Assert.Equal((400m, 0m), (inv.Paid, inv.Balance));
    }

    [Fact]
    public async Task Cancelling_a_paid_test_creates_a_refund_due_that_only_refund_permission_can_settle()
    {
        var c = await Setup();
        var reception = await At(fx.OrgAMainBranch, "RECEPTION");
        var order = await ApiClient.Body(await reception.PostAsync("/api/orders", OrderBody(c, payment: new { method = 2, amount = 2300 })));
        var orderId = order.GetProperty("id").GetInt64();
        var inv = Invoice(order);
        var lftLine = inv.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("kind").GetInt32() == 1).GetProperty("id").GetInt64();

        // Receptionist role has no orders.cancel by default; lab manager does.
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync($"/api/orders/{orderId}/lines/{lftLine}/cancel", new { reason = "Doctor changed request" })).StatusCode);
        var manager = await At(fx.OrgAMainBranch, "LABMGR");
        var noReason = await manager.PostAsync($"/api/orders/{orderId}/lines/{lftLine}/cancel", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        var cancelled = await ApiClient.Body(await manager.PostAsync($"/api/orders/{orderId}/lines/{lftLine}/cancel", new { reason = "Doctor changed request" }));
        Assert.Equal((-900m, 3), (Invoice(cancelled).GetProperty("balance").GetDecimal(), Invoice(cancelled).GetProperty("status").GetInt32()));

        var invoiceId = inv.GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync($"/api/billing/invoices/{invoiceId}/refunds", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 900, reason = "Cancelled" })).StatusCode);

        var accountant = await At(fx.OrgAMainBranch, "ACCOUNTANT");
        var tooMuch = await accountant.PostAsync($"/api/billing/invoices/{invoiceId}/refunds", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 1000, reason = "Cancelled" });
        Assert.Equal("refund_exceeds_due", await ApiClient.Code(tooMuch));
        var refunded = await ApiClient.Body(await accountant.PostAsync($"/api/billing/invoices/{invoiceId}/refunds", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 900, reason = "Cancelled" }));
        Assert.Equal((0m, 2), (Invoice(refunded).GetProperty("balance").GetDecimal(), Invoice(refunded).GetProperty("status").GetInt32()));

        await using var db = fx.System();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Order.TestCancelled" && a.EntityId == orderId.ToString() && a.Reason == "Doctor changed request"));
    }

    [Fact]
    public async Task Orders_are_visible_only_within_permitted_branches()
    {
        var c = await Setup();
        var ccUser = await Api.ClientAsync();
        ApiClient.Ok(await ccUser.LoginAsync(await Api.CreateUserAsync(fx.OrgA, ["RECEPTION"], allBranches: false, branchIds: [fx.OrgASecondBranch])));
        var order = await ApiClient.Body(await ccUser.PostAsync("/api/orders", OrderBody(c)));
        var id = order.GetProperty("id").GetInt64();
        Assert.Equal(fx.OrgASecondBranch, order.GetProperty("branchId").GetInt32());

        var mainOnly = await Api.ClientAsync();
        ApiClient.Ok(await mainOnly.LoginAsync(await Api.CreateUserAsync(fx.OrgA, ["RECEPTION"], allBranches: false, branchIds: [fx.OrgAMainBranch])));
        Assert.Equal(HttpStatusCode.NotFound, (await mainOnly.GetAsync($"/api/orders/{id}")).StatusCode);
        var list = await ApiClient.Body(await mainOnly.GetAsync($"/api/orders?patientId={c.PatientId}"));
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), o => o.GetProperty("id").GetInt64() == id);

        var otherOrg = await Api.ClientAsync();
        ApiClient.Ok(await otherOrg.LoginAsync(await Api.CreateUserAsync(fx.OrgB, ["SYSADMIN"])));
        Assert.Equal(HttpStatusCode.NotFound, (await otherOrg.GetAsync($"/api/orders/{id}")).StatusCode);
    }

    [Fact]
    public async Task Lists_and_summaries_never_include_another_organisations_money()
    {
        // Regression: a join that disabled query filters once leaked rows across branches.
        var c = await Setup();
        var reception = await At(fx.OrgAMainBranch, "RECEPTION");
        ApiClient.Ok(await reception.PostAsync("/api/orders", OrderBody(c, payment: new { method = 2, amount = 100 })));

        var otherOrg = await Api.ClientAsync();
        ApiClient.Ok(await otherOrg.LoginAsync(await Api.CreateUserAsync(fx.OrgB, ["SYSADMIN"])));
        Assert.Equal(0, (await ApiClient.Body(await otherOrg.GetAsync("/api/orders?pageSize=100"))).GetProperty("total").GetInt32());
        Assert.Equal(0, (await ApiClient.Body(await otherOrg.GetAsync("/api/billing/payments?pageSize=100"))).GetProperty("total").GetInt32());
        Assert.Equal(0m, (await ApiClient.Body(await otherOrg.GetAsync("/api/billing/day-summary"))).GetProperty("totalReceived").GetDecimal());
    }

    [Fact]
    public async Task All_branches_view_cannot_register_orders()
    {
        var c = await Setup();
        var admin = await Api.ClientAsync();
        ApiClient.Ok(await admin.LoginAsync(await Api.CreateUserAsync(fx.OrgA, ["SYSADMIN"])));   // all branches, none selected
        Assert.Equal("branch_required", await ApiClient.Code(await admin.PostAsync("/api/orders", OrderBody(c))));
    }

    [Fact]
    public async Task Closing_a_drawer_with_a_difference_needs_a_note()
    {
        var cashier = await At(fx.OrgAMainBranch, "CASHIER");
        ApiClient.Ok(await cashier.PostAsync("/api/billing/drawer/open", new { openingFloat = 1000 }));
        Assert.Equal("drawer_already_open", await ApiClient.Code(await cashier.PostAsync("/api/billing/drawer/open", new { openingFloat = 0 })));

        var res = await cashier.PostAsync("/api/billing/drawer/close", new { countedCash = 950 });
        Assert.Equal("drawer_variance_note_required", await ApiClient.Code(res));

        var closed = await ApiClient.Body(await cashier.PostAsync("/api/billing/drawer/close", new { countedCash = 950, notes = "Short by 50, recounted twice" }));
        Assert.Equal((1000m, -50m), (closed.GetProperty("expectedCash").GetDecimal(), closed.GetProperty("variance").GetDecimal()));
    }
}
