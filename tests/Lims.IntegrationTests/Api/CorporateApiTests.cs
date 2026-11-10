using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

/// <summary>Corporate clients, contracts, credit orders, receipts, allocation, statements and reconciliation through the real API.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CorporateApiTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;
    private static string Code(string p) => p + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<ApiClient> At(int branch, int org, params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(org, roles)));
        ApiClient.Ok(await c.PostAsync("/api/auth/active-branch", new { branchId = branch }));
        await c.RefreshCsrfAsync();
        return c;
    }

    private Task<ApiClient> A(params string[] roles) => At(fx.OrgAMainBranch, fx.OrgA, roles);

    /// <summary>Two tests at 1,000 and 500, a patient, and a corporate client (credit limit as given) with a 10% contract and a fixed price of 800 for test A.</summary>
    private sealed record Fix(int TestA, int TestB, int PatientId, int ClientId, int ContractId);

    private async Task<Fix> Setup(decimal? creditLimit = null, int terms = 30)
    {
        var admin = await A("SYSADMIN");
        var cat = (await ApiClient.Body(await admin.PostAsync("/api/catalog/categories", new { code = Code("C"), nameEn = "Cat" }))).GetProperty("id").GetInt32();
        var dept = (await ApiClient.Body(await admin.GetAsync("/api/organization/departments")))[0].GetProperty("id").GetInt32();
        async Task<int> Test(decimal price) => (await ApiClient.Body(await admin.PostAsync("/api/catalog/tests", new { code = Code("T"), nameEn = "Fixture", categoryId = cat, departmentId = dept, sampleTypeId = 1, price }))).GetProperty("id").GetInt32();
        var a = await Test(1000); var b = await Test(500);
        var patient = (await ApiClient.Body(await admin.PostAsync("/api/patients", new { fullName = "Corporate Patient " + Code(""), sex = 1, ageYears = 40, confirmNotDuplicate = true }))).GetProperty("id").GetInt32();
        var client = await ApiClient.Body(await admin.PostAsync("/api/corporate/clients", new { code = Code("CL"), nameEn = "Acme Ltd", nameUr = "ایکمی", creditLimit, paymentTermsDays = terms }));
        var clientId = client.GetProperty("id").GetInt32();
        var detail = await ApiClient.Body(await admin.PostAsync($"/api/corporate/clients/{clientId}/contracts", new
        {
            name = "2026 contract", validFrom = "2026-01-01", validTo = (string?)null, discountPercent = 10, prices = new[] { new { testId = a, price = 800 } },
        }));
        return new Fix(a, b, patient, clientId, detail.GetProperty("contracts")[0].GetProperty("id").GetInt32());
    }

    private static object Order(Fix f, int? clientId, params int[] tests) => new
    {
        idempotencyKey = Guid.NewGuid(), patientId = f.PatientId, priority = 0, corporateClientId = clientId,
        items = tests.Select(t => (object)new { testId = t }).ToArray(),
    };

    private async Task<JsonElement> CreditOrder(ApiClient c, Fix f, params int[] tests)
    {
        var res = await c.PostAsync("/api/orders", Order(f, f.ClientId, tests));
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    private static long InvoiceId(JsonElement order) => order.GetProperty("invoice").GetProperty("id").GetInt64();
    private static decimal Net(JsonElement order) => order.GetProperty("invoice").GetProperty("net").GetDecimal();

    private static async Task<JsonElement> NewReceipt(ApiClient c, int clientId, decimal amount, int kind = 1, string? reason = null, Guid? key = null)
    {
        var res = await c.PostAsync("/api/corporate/receipts", new { idempotencyKey = key ?? Guid.NewGuid(), clientId, kind, method = kind == 1 ? 3 : (int?)null, amount, reference = "TRX-1", reason });
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    private static object Allocate(JsonElement receipt, params (long Invoice, decimal Amount)[] items) =>
        new { rowVersion = receipt.GetProperty("rowVersion").GetString(), items = items.Select(i => new { invoiceId = i.Invoice, amount = i.Amount }).ToArray() };

    [Fact]
    public async Task Clients_and_contracts_are_managed_with_their_own_permissions_and_rules()
    {
        var f = await Setup();
        var billing = await A("CORPBILLING");
        var reception = await A("RECEPTION");
        var admin = await A("SYSADMIN");

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/corporate/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await billing.GetAsync("/api/corporate/clients")).StatusCode);

        var code = Code("CL");
        ApiClient.Ok(await billing.PostAsync("/api/corporate/clients", new { code, nameEn = "Second Ltd", paymentTermsDays = 15, creditLimit = 5000 }), HttpStatusCode.Created);
        Assert.Equal("client_code_taken", await ApiClient.Code(await billing.PostAsync("/api/corporate/clients", new { code = code.ToLowerInvariant(), nameEn = "Again" })));
        Assert.Equal(HttpStatusCode.BadRequest, (await billing.PostAsync("/api/corporate/clients", new { code = "BAD CODE", nameEn = "x" })).StatusCode);

        // Contract rules: dates, duplicate tests, unknown tests, overlap with another active contract.
        string Url() => $"/api/corporate/clients/{f.ClientId}/contracts";
        Assert.Equal("contract_dates_invalid", await ApiClient.Code(await admin.PostAsync(Url(), new { name = "x", validFrom = "2027-02-01", validTo = "2027-01-01", discountPercent = 5 })));
        Assert.Equal("contract_duplicate_test", await ApiClient.Code(await admin.PostAsync(Url(), new { name = "x", validFrom = "2030-01-01", discountPercent = 5, prices = new[] { new { testId = f.TestB, price = 1 }, new { testId = f.TestB, price = 2 } } })));
        Assert.Equal("contract_test_invalid", await ApiClient.Code(await admin.PostAsync(Url(), new { name = "x", validFrom = "2030-01-01", discountPercent = 5, prices = new[] { new { testId = 999999, price = 1 } } })));
        Assert.Equal("contract_overlap", await ApiClient.Code(await admin.PostAsync(Url(), new { name = "overlap", validFrom = "2026-06-01", discountPercent = 5 })));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync(Url(), new { name = "x", validFrom = "2030-01-01", discountPercent = 150 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await A("ACCOUNTANT")).PostAsync(Url(), new { name = "x", validFrom = "2030-01-01", discountPercent = 5 })).StatusCode);

        var detail = await ApiClient.Body(await admin.GetAsync($"/api/corporate/clients/{f.ClientId}"));
        Assert.Equal(800m, detail.GetProperty("contracts")[0].GetProperty("prices")[0].GetProperty("price").GetDecimal());
        Assert.Equal(1000m, detail.GetProperty("contracts")[0].GetProperty("prices")[0].GetProperty("catalogPrice").GetDecimal());
    }

    [Fact]
    public async Task A_credit_order_is_priced_from_the_contract_and_raises_a_credit_invoice_with_a_due_date()
    {
        var f = await Setup(terms: 45);
        var billing = await A("CORPBILLING");

        var quote = await ApiClient.Body(await billing.PostAsync("/api/orders/quote", new { corporateClientId = f.ClientId, items = new object[] { new { testId = f.TestA }, new { testId = f.TestB } } }));
        Assert.Equal(1300m, quote.GetProperty("gross").GetDecimal());            // 800 (contract price) + 500 (catalogue)
        Assert.Equal(130m, quote.GetProperty("discountAmount").GetDecimal());    // 10% contract discount
        Assert.Equal(1170m, quote.GetProperty("net").GetDecimal());

        var order = await CreditOrder(billing, f, f.TestA, f.TestB);
        var inv = order.GetProperty("invoice");
        Assert.Equal(1170m, inv.GetProperty("net").GetDecimal());
        Assert.Equal(0m, inv.GetProperty("paid").GetDecimal());
        Assert.Equal(1170m, inv.GetProperty("balance").GetDecimal());
        Assert.Equal(f.ClientId, order.GetProperty("corporateClientId").GetInt32());
        Assert.Equal("Acme Ltd", order.GetProperty("corporateClientName").GetString());

        await using var db = fx.System();
        var row = await db.Invoices.IgnoreQueryFilters().SingleAsync(i => i.Id == InvoiceId(order));
        Assert.Equal(f.ClientId, row.CorporateClientId);
        Assert.Equal(f.ContractId, row.ContractId);
        Assert.InRange(row.DueDate!.Value.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(45).DayNumber, -1, 1);   // 45 days of terms (give or take the time zone)
        Assert.Equal("Contract: 2026 contract", row.DiscountReason);
    }

    [Fact]
    public async Task Credit_orders_refuse_counter_payment_manual_discount_missing_permission_and_inactive_clients()
    {
        var f = await Setup();
        var billing = await A("CORPBILLING");
        var admin = await A("SYSADMIN");

        Assert.Equal("credit_order_no_payment", await ApiClient.Code(await billing.PostAsync("/api/orders", new
        { idempotencyKey = Guid.NewGuid(), patientId = f.PatientId, priority = 0, corporateClientId = f.ClientId, items = new object[] { new { testId = f.TestB } }, payment = new { method = 2, amount = 100 } })));
        Assert.Equal("corporate_no_manual_discount", await ApiClient.Code(await admin.PostAsync("/api/orders", new
        { idempotencyKey = Guid.NewGuid(), patientId = f.PatientId, priority = 0, corporateClientId = f.ClientId, items = new object[] { new { testId = f.TestB } }, discount = new { percent = 5, reason = "x" } })));
        // Reception can create orders but not credit orders.
        Assert.Equal("corporate_forbidden", await ApiClient.Code(await (await A("RECEPTION")).PostAsync("/api/orders", Order(f, f.ClientId, f.TestB))));

        // Patients cannot pay a credit invoice at the counter.
        var order = await CreditOrder(billing, f, f.TestB);
        var cashier = await A("CASHIER");
        Assert.Equal("invoice_is_credit", await ApiClient.Code(await cashier.PostAsync($"/api/billing/invoices/{InvoiceId(order)}/payments", new { idempotencyKey = Guid.NewGuid(), method = 2, amount = 10 })));

        // An inactive client cannot be billed.
        var detail = await ApiClient.Body(await admin.GetAsync($"/api/corporate/clients/{f.ClientId}"));
        ApiClient.Ok(await admin.PutAsync($"/api/corporate/clients/{f.ClientId}", new { code = detail.GetProperty("code").GetString(), nameEn = "Acme Ltd", paymentTermsDays = 30, isActive = false, rowVersion = detail.GetProperty("rowVersion").GetString() }));
        Assert.Equal("corporate_client_invalid", await ApiClient.Code(await billing.PostAsync("/api/orders", Order(f, f.ClientId, f.TestB))));
    }

    [Fact]
    public async Task The_credit_limit_stops_an_order_that_would_go_past_it()
    {
        var f = await Setup(creditLimit: 1500m);
        var billing = await A("CORPBILLING");
        await CreditOrder(billing, f, f.TestB);                                  // 450 after the 10% discount
        await CreditOrder(billing, f, f.TestA);                                  // 720 → balance 1,170
        var res = await billing.PostAsync("/api/orders", Order(f, f.ClientId, f.TestA, f.TestB));     // +1,170 would be 2,340
        Assert.Equal("credit_limit_exceeded", await ApiClient.Code(res));
        var account = await ApiClient.Body(await billing.GetAsync($"/api/corporate/clients/{f.ClientId}/account"));
        Assert.Equal(1170m, account.GetProperty("balance").GetDecimal());
        Assert.Equal(330m, account.GetProperty("available").GetDecimal());
    }

    [Fact]
    public async Task A_receipt_is_allocated_to_invoices_without_ever_over_allocating()
    {
        var f = await Setup();
        var billing = await A("CORPBILLING");
        var o1 = await CreditOrder(billing, f, f.TestA);    // net 720
        var o2 = await CreditOrder(billing, f, f.TestB);    // net 450
        var accountant = await A("ACCOUNTANT");

        var receipt = await NewReceipt(accountant, f.ClientId, 1000m);
        Assert.Matches(@"^CRC-\d{4}-\d{4}$", receipt.GetProperty("receiptNo").GetString());
        Assert.Equal(1000m, receipt.GetProperty("unallocated").GetDecimal());
        // The client's balance already reflects the money: 1,170 invoiced less 1,000 received.
        Assert.Equal(170m, (await ApiClient.Body(await accountant.GetAsync($"/api/corporate/clients/{f.ClientId}/account"))).GetProperty("balance").GetDecimal());

        // More than the invoice owes, more than the receipt has, the wrong client, a repeated invoice: all refused.
        Assert.Equal("allocation_exceeds_invoice", await ApiClient.Code(await accountant.PostAsync($"/api/corporate/receipts/{receipt.GetProperty("id").GetInt64()}/allocate", Allocate(receipt, (InvoiceId(o2), 450.01m)))));
        Assert.Equal("allocation_exceeds_receipt", await ApiClient.Code(await accountant.PostAsync($"/api/corporate/receipts/{receipt.GetProperty("id").GetInt64()}/allocate", Allocate(receipt, (InvoiceId(o1), 720m), (InvoiceId(o2), 450m)))));
        Assert.Equal("allocation_duplicate_invoice", await ApiClient.Code(await accountant.PostAsync($"/api/corporate/receipts/{receipt.GetProperty("id").GetInt64()}/allocate", Allocate(receipt, (InvoiceId(o1), 10m), (InvoiceId(o1), 10m)))));
        var other = await Setup();
        var foreign = await CreditOrder(await A("CORPBILLING"), other, other.TestB);
        Assert.Equal("allocation_wrong_client", await ApiClient.Code(await accountant.PostAsync($"/api/corporate/receipts/{receipt.GetProperty("id").GetInt64()}/allocate", Allocate(receipt, (InvoiceId(foreign), 10m)))));

        var allocated = await ApiClient.Body(await accountant.PostAsync($"/api/corporate/receipts/{receipt.GetProperty("id").GetInt64()}/allocate", Allocate(receipt, (InvoiceId(o1), 720m), (InvoiceId(o2), 280m))));
        Assert.Equal(1000m, allocated.GetProperty("allocated").GetDecimal());
        Assert.Equal(0m, allocated.GetProperty("unallocated").GetDecimal());
        Assert.Equal(2, allocated.GetProperty("allocations").GetArrayLength());

        var i1 = await ApiClient.Body(await billing.GetAsync($"/api/orders/{o1.GetProperty("id").GetInt64()}"));
        Assert.Equal(2, i1.GetProperty("invoice").GetProperty("status").GetInt32());                 // paid
        var i2 = await ApiClient.Body(await billing.GetAsync($"/api/orders/{o2.GetProperty("id").GetInt64()}"));
        Assert.Equal(170m, i2.GetProperty("invoice").GetProperty("balance").GetDecimal());
        Assert.Equal(1, i2.GetProperty("invoice").GetProperty("status").GetInt32());                 // partially paid
        Assert.Equal(6, i2.GetProperty("invoice").GetProperty("payments")[0].GetProperty("method").GetInt32());   // recorded as a corporate payment

        // Nothing left on the receipt; the books agree.
        Assert.Equal("allocation_exceeds_receipt", await ApiClient.Code(await accountant.PostAsync($"/api/corporate/receipts/{receipt.GetProperty("id").GetInt64()}/allocate", Allocate(allocated, (InvoiceId(o2), 1m)))));
        var rec = await ApiClient.Body(await accountant.GetAsync($"/api/corporate/reconcile?clientId={f.ClientId}"));
        Assert.True(rec.GetProperty("ok").GetBoolean(), rec.ToString());
        Assert.Equal(170m, (await ApiClient.Body(await accountant.GetAsync($"/api/corporate/clients/{f.ClientId}/account"))).GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task The_same_receipt_cannot_be_allocated_twice_even_at_the_same_moment()
    {
        var f = await Setup();
        var billing = await A("CORPBILLING");
        var o1 = await CreditOrder(billing, f, f.TestA);
        var o2 = await CreditOrder(billing, f, f.TestA);
        var accountant = await A("ACCOUNTANT"); var second = await A("ACCOUNTANT");
        var receipt = await NewReceipt(accountant, f.ClientId, 720m);
        var id = receipt.GetProperty("id").GetInt64();

        // Two people each allocate the whole receipt, to different invoices.
        var results = await Task.WhenAll(
            accountant.PostAsync($"/api/corporate/receipts/{id}/allocate", Allocate(receipt, (InvoiceId(o1), 720m))),
            second.PostAsync($"/api/corporate/receipts/{id}/allocate", Allocate(receipt, (InvoiceId(o2), 720m))));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest));
        var rec = await ApiClient.Body(await accountant.GetAsync($"/api/corporate/reconcile?clientId={f.ClientId}"));
        Assert.True(rec.GetProperty("ok").GetBoolean(), rec.ToString());
        await using var db = fx.System();
        Assert.Equal(1, await db.CorporateAllocations.IgnoreQueryFilters().CountAsync(a => a.ReceiptId == id));
    }

    [Fact]
    public async Task Receipts_use_their_own_permissions_idempotency_and_credit_notes_need_a_reason()
    {
        var f = await Setup();
        var billing = await A("CORPBILLING");         // receive, but not adjust
        var accountant = await A("ACCOUNTANT");       // receive and adjust

        var key = Guid.NewGuid();
        var first = await NewReceipt(billing, f.ClientId, 100m, key: key);
        var again = await NewReceipt(billing, f.ClientId, 100m, key: key);
        Assert.Equal(first.GetProperty("id").GetInt64(), again.GetProperty("id").GetInt64());                   // double click: one receipt
        Assert.Equal("payment_method_invalid", await ApiClient.Code(await billing.PostAsync("/api/corporate/receipts", new { idempotencyKey = Guid.NewGuid(), clientId = f.ClientId, kind = 1, method = 99, amount = 10 })));
        Assert.Equal("corporate_method_invalid", await ApiClient.Code(await billing.PostAsync("/api/corporate/receipts", new { idempotencyKey = Guid.NewGuid(), clientId = f.ClientId, kind = 1, method = 1, amount = 10 })));   // cash
        Assert.Equal(HttpStatusCode.BadRequest, (await billing.PostAsync("/api/corporate/receipts", new { idempotencyKey = Guid.NewGuid(), clientId = f.ClientId, kind = 1, method = 3, amount = 0 })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await billing.PostAsync("/api/corporate/receipts", new { idempotencyKey = Guid.NewGuid(), clientId = f.ClientId, kind = 2, amount = 50, reason = "Write-off" })).StatusCode);
        Assert.Equal("reason_required", await ApiClient.Code(await accountant.PostAsync("/api/corporate/receipts", new { idempotencyKey = Guid.NewGuid(), clientId = f.ClientId, kind = 2, amount = 50 })));
        var note = await NewReceipt(accountant, f.ClientId, 50m, kind: 2, reason: "Agreed service credit");
        Assert.Equal(2, note.GetProperty("kind").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await (await A("RECEPTION")).PostAsync("/api/corporate/receipts", new { idempotencyKey = Guid.NewGuid(), clientId = f.ClientId, kind = 1, method = 3, amount = 10 })).StatusCode);
    }

    [Fact]
    public async Task Aging_and_the_statement_follow_due_dates_and_dates_of_issue()
    {
        var f = await Setup();
        var billing = await A("CORPBILLING");
        var old = await CreditOrder(billing, f, f.TestB);       // 450
        var recent = await CreditOrder(billing, f, f.TestA);    // 720
        await using (var db = fx.System())
        {
            // The first invoice fell due 45 days ago.
            var due = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(-45);
            await db.Invoices.IgnoreQueryFilters().Where(i => i.Id == InvoiceId(old)).ExecuteUpdateAsync(s => s.SetProperty(i => i.DueDate, due));
        }
        var accountant = await A("ACCOUNTANT");
        var account = await ApiClient.Body(await accountant.GetAsync($"/api/corporate/clients/{f.ClientId}/account"));
        var aging = account.GetProperty("aging");
        Assert.Equal(450m, aging.GetProperty("days31To60").GetDecimal());
        Assert.Equal(720m, aging.GetProperty("notDue").GetDecimal());
        Assert.Equal(450m, aging.GetProperty("overdue").GetDecimal());
        Assert.Equal(1170m, account.GetProperty("outstanding").GetDecimal());

        await NewReceipt(accountant, f.ClientId, 200m);
        var statement = await ApiClient.Body(await accountant.GetAsync($"/api/corporate/clients/{f.ClientId}/statement"));
        Assert.Equal(0m, statement.GetProperty("openingBalance").GetDecimal());
        var types = statement.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("type").GetString()).ToList();
        Assert.Equal(["Invoice", "Invoice", "Receipt"], types);
        Assert.Equal(970m, statement.GetProperty("closingBalance").GetDecimal());                  // 1,170 invoiced less 200 received
        Assert.Equal(970m, statement.GetProperty("account").GetProperty("balance").GetDecimal());

        // A statement that starts tomorrow opens with everything so far and has no lines.
        var tomorrow = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd");
        var later = await ApiClient.Body(await accountant.GetAsync($"/api/corporate/clients/{f.ClientId}/statement?from={tomorrow}&to={tomorrow}"));
        Assert.Equal(970m, later.GetProperty("openingBalance").GetDecimal());
        Assert.Equal(0, later.GetProperty("lines").GetArrayLength());
        Assert.Equal("statement_dates_invalid", await ApiClient.Code(await accountant.GetAsync($"/api/corporate/clients/{f.ClientId}/statement?from=2026-12-01&to=2026-11-01")));
        _ = recent;
    }

    [Fact]
    public async Task An_invoice_that_has_been_paid_by_allocation_cannot_be_cancelled_below_what_was_allocated()
    {
        var f = await Setup();
        var billing = await A("CORPBILLING");
        var admin = await A("SYSADMIN");
        var order = await CreditOrder(billing, f, f.TestA, f.TestB);        // net 1,170
        var accountant = await A("ACCOUNTANT");
        var receipt = await NewReceipt(accountant, f.ClientId, 1170m);
        ApiClient.Ok(await accountant.PostAsync($"/api/corporate/receipts/{receipt.GetProperty("id").GetInt64()}/allocate", Allocate(receipt, (InvoiceId(order), 1170m))));

        var lineId = order.GetProperty("invoice").GetProperty("lines")[0].GetProperty("id").GetInt64();
        Assert.Equal("credit_invoice_overallocated", await ApiClient.Code(await admin.PostAsync($"/api/orders/{order.GetProperty("id").GetInt64()}/lines/{lineId}/cancel", new { reason = "Mistake" })));

        // Before anything is allocated, a credit invoice can shrink normally.
        var open = await CreditOrder(billing, f, f.TestA, f.TestB);
        var line = open.GetProperty("invoice").GetProperty("lines")[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await admin.PostAsync($"/api/orders/{open.GetProperty("id").GetInt64()}/lines/{line}/cancel", new { reason = "Not needed" }));
    }

    [Fact]
    public async Task Isolation_anonymous_and_token_less_requests_and_database_constraints()
    {
        var f = await Setup();
        var billing = await A("CORPBILLING");
        var order = await CreditOrder(billing, f, f.TestB);
        var accountant = await A("ACCOUNTANT");
        var receipt = await NewReceipt(accountant, f.ClientId, 100m);

        var other = await At(fx.OrgBMainBranch, fx.OrgB, "SYSADMIN");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/corporate/clients/{f.ClientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/corporate/receipts/{receipt.GetProperty("id").GetInt64()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/corporate/receipts/{receipt.GetProperty("id").GetInt64()}/allocate", Allocate(receipt, (InvoiceId(order), 10m)))).StatusCode);
        Assert.Equal(0, (await ApiClient.Body(await other.GetAsync("/api/corporate/invoices"))).GetProperty("total").GetInt32());

        var anon = await Api.ClientAsync();
        foreach (var url in new[] { "/api/corporate/clients", "/api/corporate/invoices", "/api/corporate/receipts", "/api/corporate/reconcile", $"/api/corporate/clients/{f.ClientId}/statement" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(url)).StatusCode);
        foreach (var url in new[] { "/api/corporate/clients", "/api/corporate/receipts", $"/api/corporate/receipts/{receipt.GetProperty("id").GetInt64()}/allocate" })
            Assert.Equal(HttpStatusCode.BadRequest, (await (await A("SYSADMIN")).PostAsync(url, new { }, withCsrf: false)).StatusCode);

        await using var db = fx.System();
        var id = receipt.GetProperty("id").GetInt64();
        var over = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE billing.CorporateReceipts SET Allocated = Amount + 1 WHERE Id = {0}", id));
        Assert.Contains("CK_CorporateReceipts_Amount", (over.InnerException ?? over).Message);
        var noReason = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO billing.CorporateReceipts (OrganizationId,ClientId,ReceiptNo,IdempotencyKey,Kind,Amount,Allocated,ReceivedAtUtc,ReceivedByUserId,CreatedAtUtc) SELECT OrganizationId,ClientId,'X-1',NEWID(),2,10,0,ReceivedAtUtc,ReceivedByUserId,CreatedAtUtc FROM billing.CorporateReceipts WHERE Id = {0}", id));
        Assert.Contains("CK_CorporateReceipts_Kind", (noReason.InnerException ?? noReason).Message);
        // A payment method outside the known list is refused by the database (create a real corporate payment first).
        ApiClient.Ok(await accountant.PostAsync($"/api/corporate/receipts/{id}/allocate", Allocate(receipt, (InvoiceId(order), 50m))));
        var method7 = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE billing.Payments SET Method = 7 WHERE InvoiceId = {0}", InvoiceId(order)));
        Assert.Contains("CK_Payments_Method", (method7.InnerException ?? method7).Message);
    }
}
