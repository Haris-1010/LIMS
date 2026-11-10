using System.Net;
using System.Text.Json;
using Lims.Domain.Inventory;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

/// <summary>Stock items, suppliers, purchases, batches, manual changes, transfers, automatic consumption, alerts and the ledger through the real API.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class InventoryApiTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;
    private static string Code(string p) => p + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    private static string Soon(int days) => DateOnly.FromDateTime(DateTime.Today.AddDays(days)).ToString("yyyy-MM-dd");

    private async Task<ApiClient> At(int branch, int org, params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(org, roles)));
        ApiClient.Ok(await c.PostAsync("/api/auth/active-branch", new { branchId = branch }));
        await c.RefreshCsrfAsync();
        return c;
    }

    private Task<ApiClient> Store() => At(fx.OrgAMainBranch, fx.OrgA, "INVENTORY");

    private static async Task<JsonElement> Do(ApiClient c, string path, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var res = await c.PostAsync(path, body);
        ApiClient.Ok(res, expected);
        return await ApiClient.Body(res);
    }

    private static async Task<int> NewSupplier(ApiClient c)
    {
        var list = await Do(c, "/api/inventory/suppliers", new { code = Code("SU"), nameEn = "Medico Supplies", nameUr = "میڈیکو" });
        return list.EnumerateArray().Last(s => s.GetProperty("nameEn").GetString() == "Medico Supplies").GetProperty("id").GetInt32();
    }

    private static async Task<int> NewItem(ApiClient c, decimal reorder = 0, bool tracksExpiry = true)
    {
        var res = await c.PostAsync("/api/inventory/items", new { code = Code("IT"), nameEn = "Glucose reagent", kind = 1, unitName = "ml", reorderLevel = reorder, tracksExpiry });
        ApiClient.Ok(res, HttpStatusCode.Created);
        return (await ApiClient.Body(res)).GetProperty("id").GetInt32();
    }

    private static object Line(int item, string batch, decimal qty, string? expiry, decimal? cost = 2) => new { itemId = item, batchNo = batch, expiryDate = expiry, quantity = qty, unitCost = cost };

    private static Task<JsonElement> Buy(ApiClient c, int supplier, params object[] lines) =>
        Do(c, "/api/inventory/purchases", new { idempotencyKey = Guid.NewGuid(), supplierId = supplier, supplierInvoiceNo = "INV-1", lines });

    private static async Task<JsonElement> Item(ApiClient c, int id) => await ApiClient.Body(await c.GetAsync($"/api/inventory/items/{id}"));

    private static long BatchIdOf(JsonElement item, string batchNo) => item.GetProperty("batches").EnumerateArray().Single(b => b.GetProperty("batchNo").GetString() == batchNo).GetProperty("id").GetInt64();
    private static decimal OnHandAt(JsonElement item, string batchNo, int branchId) => item.GetProperty("batches").EnumerateArray().Single(b => b.GetProperty("batchNo").GetString() == batchNo && b.GetProperty("branchId").GetInt32() == branchId).GetProperty("quantityOnHand").GetDecimal();
    private static decimal OnHand(JsonElement item, string batchNo) => item.GetProperty("batches").EnumerateArray().Single(b => b.GetProperty("batchNo").GetString() == batchNo).GetProperty("quantityOnHand").GetDecimal();

    // ---------------------------------------------------------------- items and suppliers

    [Fact]
    public async Task Items_and_suppliers_are_managed_with_their_own_permissions_and_rules()
    {
        var store = await Store();
        var code = Code("IT");
        ApiClient.Ok(await store.PostAsync("/api/inventory/items", new { code, nameEn = "Reagent A", kind = 1, unitName = "kit", reorderLevel = 5 }), HttpStatusCode.Created);
        Assert.Equal("item_code_taken", await ApiClient.Code(await store.PostAsync("/api/inventory/items", new { code = code.ToLowerInvariant(), nameEn = "Again", kind = 1, unitName = "kit" })));
        Assert.Equal(HttpStatusCode.BadRequest, (await store.PostAsync("/api/inventory/items", new { code = "bad code!", nameEn = "X", kind = 1, unitName = "kit" })).StatusCode);
        Assert.Equal("supplier_invalid", await ApiClient.Code(await store.PostAsync("/api/inventory/items", new { code = Code("IT"), nameEn = "X", kind = 1, unitName = "kit", defaultSupplierId = 999999 })));

        var sup = await NewSupplier(store);
        var supCode = Code("SU");
        ApiClient.Ok(await store.PostAsync("/api/inventory/suppliers", new { code = supCode, nameEn = "Second" }));
        Assert.Equal("supplier_code_taken", await ApiClient.Code(await store.PostAsync("/api/inventory/suppliers", new { code = supCode, nameEn = "Dup" })));

        // A technician may look at stock but not manage it; reception has no stock access at all.
        var tech = await At(fx.OrgAMainBranch, fx.OrgA, "TECHNICIAN");
        ApiClient.Ok(await tech.GetAsync("/api/inventory/items"));
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync("/api/inventory/items", new { code = Code("IT"), nameEn = "X", kind = 1, unitName = "kit" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync("/api/inventory/suppliers", new { code = Code("SU"), nameEn = "X" })).StatusCode);
        var reception = await At(fx.OrgAMainBranch, fx.OrgA, "RECEPTION");
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/inventory/items")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/inventory/ledger")).StatusCode);

        // Editing carries the version that was seen.
        var created = await Item(store, (await ApiClient.Body(await store.GetAsync($"/api/inventory/items?search={code}"))).GetProperty("items")[0].GetProperty("id").GetInt32());
        var rv = created.GetProperty("rowVersion").GetString();
        var id = created.GetProperty("id").GetInt32();
        ApiClient.Ok(await store.PutAsync($"/api/inventory/items/{id}", new { code, nameEn = "Reagent A2", kind = 1, unitName = "kit", reorderLevel = 6, tracksExpiry = true, isActive = true, rowVersion = rv }));
        Assert.Equal(HttpStatusCode.Conflict, (await store.PutAsync($"/api/inventory/items/{id}", new { code, nameEn = "Stale", kind = 1, unitName = "kit", reorderLevel = 6, tracksExpiry = true, isActive = true, rowVersion = rv })).StatusCode);
        Assert.True(sup > 0);
    }

    // ---------------------------------------------------------------- purchases

    [Fact]
    public async Task A_purchase_creates_batches_and_ledger_lines_and_enforces_expiry_and_batch_rules()
    {
        var store = await Store();
        var sup = await NewSupplier(store);
        var item = await NewItem(store);
        var plain = await NewItem(store, tracksExpiry: false);

        var receipt = await Buy(store, sup, Line(item, "B1", 10, Soon(400)), Line(plain, "P1", 3, null));
        Assert.StartsWith("GRN", receipt.GetProperty("receiptNo").GetString());
        Assert.Equal(26m, receipt.GetProperty("total").GetDecimal());   // 10 × 2 + 3 × 2
        Assert.Equal(2, receipt.GetProperty("lines").GetArrayLength());

        var stocked = await Item(store, item);
        Assert.Equal(10m, OnHand(stocked, "B1"));
        Assert.Equal(Soon(400), stocked.GetProperty("batches")[0].GetProperty("expiryDate").GetString());
        Assert.Null((await Item(store, plain)).GetProperty("batches")[0].GetProperty("expiryDate").GetString());

        var ledger = await ApiClient.Body(await store.GetAsync($"/api/inventory/ledger?itemId={item}"));
        var line = Assert.Single(ledger.GetProperty("items").EnumerateArray());
        Assert.Equal(1, line.GetProperty("type").GetInt32());   // PurchaseIn
        Assert.Equal(10m, line.GetProperty("quantity").GetDecimal());
        Assert.Equal(receipt.GetProperty("receiptNo").GetString(), line.GetProperty("reference").GetString());

        // The receipt can be found again by its number.
        var found = await ApiClient.Body(await store.GetAsync($"/api/inventory/purchases?search={receipt.GetProperty("receiptNo").GetString()}"));
        Assert.Equal(1, found.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Purchase_validation_refuses_missing_or_past_expiry_unknown_supplier_and_a_changed_expiry()
    {
        var store = await Store();
        var sup = await NewSupplier(store);
        var item = await NewItem(store);
        object Body(params object[] lines) => new { idempotencyKey = Guid.NewGuid(), supplierId = sup, lines };

        Assert.Equal("expiry_required", await ApiClient.Code(await store.PostAsync("/api/inventory/purchases", Body(Line(item, "B1", 5, null)))));
        Assert.Equal("expiry_in_past", await ApiClient.Code(await store.PostAsync("/api/inventory/purchases", Body(Line(item, "B1", 5, Soon(-3))))));
        Assert.Equal("batch_no_invalid", await ApiClient.Code(await store.PostAsync("/api/inventory/purchases", Body(Line(item, "UNBATCHED", 5, Soon(100))))));
        Assert.Equal("supplier_invalid", await ApiClient.Code(await store.PostAsync("/api/inventory/purchases", new { idempotencyKey = Guid.NewGuid(), supplierId = 987654, lines = new[] { Line(item, "B1", 5, Soon(100)) } })));
        Assert.Equal(HttpStatusCode.BadRequest, (await store.PostAsync("/api/inventory/purchases", Body(Line(item, "B1", 0, Soon(100))))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await store.PostAsync("/api/inventory/purchases", Body())).StatusCode);
        Assert.Equal("idempotency_key_required", await ApiClient.Code(await store.PostAsync("/api/inventory/purchases", new { idempotencyKey = Guid.Empty, supplierId = sup, lines = new[] { Line(item, "B1", 5, Soon(100)) } })));

        // Nothing above reached the stock.
        Assert.Empty((await Item(store, item)).GetProperty("batches").EnumerateArray());

        // The same batch received again adds to it, but only with the same expiry date.
        await Buy(store, sup, Line(item, "B1", 5, Soon(100)));
        await Buy(store, sup, Line(item, "B1", 7, Soon(100)));
        Assert.Equal(12m, OnHand(await Item(store, item), "B1"));
        Assert.Equal("batch_expiry_mismatch", await ApiClient.Code(await store.PostAsync("/api/inventory/purchases", Body(Line(item, "B1", 1, Soon(200))))));
        Assert.Equal(12m, OnHand(await Item(store, item), "B1"));
    }

    [Fact]
    public async Task Resubmitting_a_purchase_returns_the_same_receipt_even_when_sent_at_the_same_time()
    {
        var store = await Store();
        var sup = await NewSupplier(store);
        var item = await NewItem(store);
        var key = Guid.NewGuid();
        object Body() => new { idempotencyKey = key, supplierId = sup, lines = new[] { Line(item, "B1", 10, Soon(300)) } };

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            var res = await store.PostAsync("/api/inventory/purchases", Body());
            return res.IsSuccessStatusCode ? (await ApiClient.Body(res)).GetProperty("id").GetInt64() : -1L;
        }));
        Assert.Single(results.Where(r => r > 0).Distinct());
        Assert.Equal(10m, OnHand(await Item(store, item), "B1"));   // received once
    }

    // ---------------------------------------------------------------- manual changes

    [Fact]
    public async Task Stock_never_goes_below_zero_and_two_people_cannot_take_the_same_stock()
    {
        var store = await Store();
        var sup = await NewSupplier(store);
        var item = await NewItem(store);
        await Buy(store, sup, Line(item, "B1", 10, Soon(300)));
        var batch = BatchIdOf(await Item(store, item), "B1");

        Assert.Equal("insufficient_stock", await ApiClient.Code(await store.PostAsync("/api/inventory/movements", new { batchId = batch, kind = 1, quantity = 11 })));
        Assert.Equal("reason_required", await ApiClient.Code(await store.PostAsync("/api/inventory/movements", new { batchId = batch, kind = 3, quantity = 1 })));
        Assert.Equal("reason_required", await ApiClient.Code(await store.PostAsync("/api/inventory/movements", new { batchId = batch, kind = 4, quantity = 1 })));
        Assert.Equal(HttpStatusCode.BadRequest, (await store.PostAsync("/api/inventory/movements", new { batchId = batch, kind = 1, quantity = -2 })).StatusCode);
        Assert.Equal("movement_kind_invalid", await ApiClient.Code(await store.PostAsync("/api/inventory/movements", new { batchId = batch, kind = 9, quantity = 1 })));
        Assert.Equal(10m, OnHand(await Item(store, item), "B1"));

        // Eight people each try to issue 3 from a batch of 10: exactly three can.
        var codes = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => (await store.PostAsync("/api/inventory/movements", new { batchId = batch, kind = 1, quantity = 3 })).StatusCode));
        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.OK));
        Assert.All(codes.Where(c => c != HttpStatusCode.OK), c => Assert.Equal(HttpStatusCode.Conflict, c));
        Assert.Equal(1m, OnHand(await Item(store, item), "B1"));

        // The ledger adds up to what is on hand, line by line.
        var ledger = await ApiClient.Body(await store.GetAsync($"/api/inventory/ledger?itemId={item}&pageSize=100"));
        var lines = ledger.GetProperty("items").EnumerateArray().OrderBy(l => l.GetProperty("id").GetInt64()).ToList();
        Assert.Equal(4, lines.Count);
        Assert.Equal(1m, lines.Sum(l => l.GetProperty("quantity").GetDecimal()) - 0m);
        Assert.Equal(1m, lines.Last().GetProperty("balanceAfter").GetDecimal());

        // A correction is a new line, never an edit of an old one.
        var adj = await Do(store, "/api/inventory/movements", new { batchId = batch, kind = 2, quantity = 4, reason = "Found in the cold room" });
        Assert.Equal(5m, adj.GetProperty("balanceAfter").GetDecimal());
    }

    [Fact]
    public async Task Writing_off_stock_and_adjusting_need_the_adjust_permission_and_a_reason()
    {
        var store = await Store();
        var sup = await NewSupplier(store);
        var item = await NewItem(store);
        await Buy(store, sup, Line(item, "B1", 10, Soon(300)));
        var batch = BatchIdOf(await Item(store, item), "B1");
        var tech = await At(fx.OrgAMainBranch, fx.OrgA, "TECHNICIAN");
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync("/api/inventory/movements", new { batchId = batch, kind = 1, quantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync("/api/inventory/purchases", new { idempotencyKey = Guid.NewGuid(), supplierId = sup, lines = new[] { Line(item, "B2", 1, Soon(100)) } })).StatusCode);

        var w = await Do(store, "/api/inventory/movements", new { batchId = batch, kind = 4, quantity = 2, reason = "Dropped" });
        Assert.Equal(-2m, w.GetProperty("quantity").GetDecimal());
        Assert.Equal(8m, w.GetProperty("balanceAfter").GetDecimal());
    }

    // ---------------------------------------------------------------- transfers and branch visibility

    [Fact]
    public async Task Stock_moves_between_branches_as_two_linked_ledger_lines_and_each_branch_sees_only_its_own()
    {
        var store = await Store();
        var sup = await NewSupplier(store);
        var item = await NewItem(store);
        await Buy(store, sup, Line(item, "B1", 10, Soon(300)));
        var batch = BatchIdOf(await Item(store, item), "B1");

        Assert.Equal("transfer_same_branch", await ApiClient.Code(await store.PostAsync("/api/inventory/transfers", new { batchId = batch, toBranchId = fx.OrgAMainBranch, quantity = 1 })));
        Assert.Equal("transfer_branch_invalid", await ApiClient.Code(await store.PostAsync("/api/inventory/transfers", new { batchId = batch, toBranchId = 987654, quantity = 1 })));
        Assert.Equal("transfer_branch_invalid", await ApiClient.Code(await store.PostAsync("/api/inventory/transfers", new { batchId = batch, toBranchId = fx.OrgBMainBranch, quantity = 1 })));
        Assert.Equal("insufficient_stock", await ApiClient.Code(await store.PostAsync("/api/inventory/transfers", new { batchId = batch, toBranchId = fx.OrgASecondBranch, quantity = 11 })));

        var moved = await Do(store, "/api/inventory/transfers", new { batchId = batch, toBranchId = fx.OrgASecondBranch, quantity = 4, notes = "For the centre" });
        Assert.Equal(-4m, moved.GetProperty("out").GetProperty("quantity").GetDecimal());
        Assert.Equal(4m, moved.GetProperty("in").GetProperty("quantity").GetDecimal());
        Assert.Equal(moved.GetProperty("out").GetProperty("reference").GetString(), moved.GetProperty("in").GetProperty("reference").GetString());
        var both = await Item(store, item);   // this user may see both branches, so both batches are listed, each with its own branch
        Assert.Equal(6m, OnHandAt(both, "B1", fx.OrgAMainBranch));
        Assert.Equal(4m, OnHandAt(both, "B1", fx.OrgASecondBranch));
        Assert.Equal(Soon(300), both.GetProperty("batches").EnumerateArray().Single(b => b.GetProperty("branchId").GetInt32() == fx.OrgASecondBranch).GetProperty("expiryDate").GetString());   // the expiry travels with the stock

        // A user limited to the second branch cannot touch the main branch's batch (it does not exist for them).
        var limited = await Api.ClientAsync();
        ApiClient.Ok(await limited.LoginAsync(await Api.CreateUserAsync(fx.OrgA, ["INVENTORY"], allBranches: false, branchIds: [fx.OrgASecondBranch])));
        ApiClient.Ok(await limited.PostAsync("/api/auth/active-branch", new { branchId = fx.OrgASecondBranch }));
        await limited.RefreshCsrfAsync();
        var seen = await Item(limited, item);
        Assert.Equal(fx.OrgASecondBranch, Assert.Single(seen.GetProperty("batches").EnumerateArray()).GetProperty("branchId").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await limited.PostAsync("/api/inventory/movements", new { batchId = batch, kind = 1, quantity = 1 })).StatusCode);
        Assert.Equal(6m, OnHandAt(await Item(store, item), "B1", fx.OrgAMainBranch));
    }

    // ---------------------------------------------------------------- consumption

    private async Task<(int Test, int Param, int PatientId)> TestWithRange()
    {
        var admin = await At(fx.OrgAMainBranch, fx.OrgA, "SYSADMIN");
        var cat = (await ApiClient.Body(await admin.PostAsync("/api/catalog/categories", new { code = Code("C"), nameEn = "Cat" }))).GetProperty("id").GetInt32();
        var dept = (await ApiClient.Body(await admin.GetAsync("/api/organization/departments")))[0].GetProperty("id").GetInt32();
        var test = (await ApiClient.Body(await admin.PostAsync("/api/catalog/tests", new { code = Code("T"), nameEn = "Glucose", categoryId = cat, departmentId = dept, sampleTypeId = 1, turnaroundHours = 24, price = 100 }))).GetProperty("id").GetInt32();
        var param = (await ApiClient.Body(await admin.PostAsync($"/api/catalog/tests/{test}/parameters", new { code = Code("P"), nameEn = "Glucose", resultType = 1, unitId = 10, decimalPlaces = 1 }))).GetProperty("id").GetInt32();
        var patient = (await ApiClient.Body(await admin.PostAsync("/api/patients", new { fullName = "Stock Patient " + Code(""), sex = 1, ageYears = 40, confirmNotDuplicate = true }))).GetProperty("id").GetInt32();
        return (test, param, patient);
    }

    private async Task<long> ReceivedItem(int patient, int test)
    {
        var reception = await At(fx.OrgAMainBranch, fx.OrgA, "RECEPTION");
        var order = await ApiClient.Body(await reception.PostAsync("/api/orders", new { idempotencyKey = Guid.NewGuid(), patientId = patient, priority = 0, items = new object[] { new { testId = test } } }));
        var item = order.GetProperty("items")[0].GetProperty("id").GetInt64();
        var sid = (await ApiClient.Body(await reception.GetAsync($"/api/orders/{order.GetProperty("id").GetInt64()}/samples")))[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await (await At(fx.OrgAMainBranch, fx.OrgA, "PHLEBOTOMY")).PostAsync($"/api/samples/{sid}/collect", new { identityConfirmed = true }));
        ApiClient.Ok(await (await At(fx.OrgAMainBranch, fx.OrgA, "TECHNICIAN")).PostAsync($"/api/samples/{sid}/receive"));
        return item;
    }

    private static async Task<JsonElement> Submit(ApiClient tech, long item, int param, string value)
    {
        var entry = await ApiClient.Body(await tech.GetAsync($"/api/results/items/{item}"));
        var rv = entry.TryGetProperty("current", out var cur) && cur.ValueKind == JsonValueKind.Object ? cur.GetProperty("rowVersion").GetString() : null;
        var saved = await ApiClient.Body(await tech.PutAsync($"/api/results/items/{item}/draft", new { rowVersion = rv, comment = (string?)null, values = new[] { new { parameterId = param, value, optionId = (int?)null } } }));
        var res = await tech.PostAsync($"/api/results/{saved.GetProperty("current").GetProperty("id").GetInt64()}/submit", new { rowVersion = saved.GetProperty("current").GetProperty("rowVersion").GetString() });
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    [Fact]
    public async Task Submitting_a_result_charges_the_reagents_earliest_expiry_first_once_only()
    {
        var store = await Store();
        var sup = await NewSupplier(store);
        var item = await NewItem(store);
        var (test, param, patient) = await TestWithRange();
        ApiClient.Ok(await store.PutAsync($"/api/inventory/items/{item}/profile", new { tests = new[] { new { testId = test, quantityPerTest = 3 } } }));
        Assert.Equal(test, (await Item(store, item)).GetProperty("profile")[0].GetProperty("testId").GetInt32());
        // Two batches: the one expiring sooner holds only 2, so the third unit comes from the later one.
        await Buy(store, sup, Line(item, "LATE", 10, Soon(300)), Line(item, "SOON", 2, Soon(60)));

        var tech = await At(fx.OrgAMainBranch, fx.OrgA, "TECHNICIAN");
        var orderItem = await ReceivedItem(patient, test);
        Assert.Equal(12m, (await Item(store, item)).GetProperty("batches").EnumerateArray().Sum(b => b.GetProperty("quantityOnHand").GetDecimal()));   // saving a draft costs nothing
        var submitted = await Submit(tech, orderItem, param, "5");

        var after = await Item(store, item);
        Assert.Equal(9m, after.GetProperty("batches").EnumerateArray().Sum(b => b.GetProperty("quantityOnHand").GetDecimal()));
        Assert.DoesNotContain(after.GetProperty("batches").EnumerateArray(), b => b.GetProperty("batchNo").GetString() == "SOON");   // used up first
        Assert.Equal(9m, OnHand(after, "LATE"));

        // Return it and submit again: the same test run is not charged twice.
        var patho = await At(fx.OrgAMainBranch, fx.OrgA, "PATHOLOGIST");
        var cur = submitted.GetProperty("current");
        ApiClient.Ok(await patho.PostAsync($"/api/results/{cur.GetProperty("id").GetInt64()}/return", new { reason = "Recheck", rowVersion = cur.GetProperty("rowVersion").GetString() }));
        await Submit(tech, orderItem, param, "6");
        Assert.Equal(9m, (await Item(store, item)).GetProperty("batches").EnumerateArray().Sum(b => b.GetProperty("quantityOnHand").GetDecimal()));

        var ledger = await ApiClient.Body(await store.GetAsync($"/api/inventory/ledger?itemId={item}&type=2&pageSize=50"));
        var consumption = ledger.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, consumption.Count);   // 2 from SOON, 1 from LATE
        Assert.All(consumption, m => Assert.True(m.GetProperty("quantity").GetDecimal() < 0));
        Assert.Equal(-3m, consumption.Sum(m => m.GetProperty("quantity").GetDecimal()));
    }

    [Fact]
    public async Task When_stock_is_short_the_result_is_still_saved_and_the_shortfall_is_recorded_until_someone_resolves_it()
    {
        var store = await Store();
        var sup = await NewSupplier(store);
        var item = await NewItem(store);
        var (test, param, patient) = await TestWithRange();
        ApiClient.Ok(await store.PutAsync($"/api/inventory/items/{item}/profile", new { tests = new[] { new { testId = test, quantityPerTest = 5 } } }));
        await Buy(store, sup, Line(item, "B1", 2, Soon(300)));

        var before = (await ApiClient.Body(await store.GetAsync("/api/inventory/alerts"))).GetProperty("openShortfalls").GetInt32();
        var tech = await At(fx.OrgAMainBranch, fx.OrgA, "TECHNICIAN");
        await Submit(tech, await ReceivedItem(patient, test), param, "5");   // Submit asserts HTTP 200: the result was saved

        Assert.Empty((await Item(store, item)).GetProperty("batches").EnumerateArray());   // the 2 that existed were used, the other 3 are missing
        var shortfalls = await ApiClient.Body(await store.GetAsync("/api/inventory/shortfalls?openOnly=true&pageSize=100"));
        var mine = shortfalls.GetProperty("items").EnumerateArray().Single(s => s.GetProperty("itemId").GetInt32() == item);
        Assert.Equal(3m, mine.GetProperty("quantity").GetDecimal());
        Assert.Equal(before + 1, (await ApiClient.Body(await store.GetAsync("/api/inventory/alerts"))).GetProperty("openShortfalls").GetInt32());

        var id = mine.GetProperty("id").GetInt64();
        ApiClient.Ok(await store.PostAsync($"/api/inventory/shortfalls/{id}/resolve", new { note = "Used the bench stock" }), HttpStatusCode.NoContent);
        Assert.Equal("shortfall_resolved", await ApiClient.Code(await store.PostAsync($"/api/inventory/shortfalls/{id}/resolve", new { note = "again" })));
        Assert.Equal(before, (await ApiClient.Body(await store.GetAsync("/api/inventory/alerts"))).GetProperty("openShortfalls").GetInt32());
    }

    [Fact]
    public async Task Profile_rules_refuse_duplicate_and_unknown_tests()
    {
        var store = await Store();
        var item = await NewItem(store);
        var (test, _, _) = await TestWithRange();
        Assert.Equal("profile_duplicate_test", await ApiClient.Code(await store.PutAsync($"/api/inventory/items/{item}/profile", new { tests = new[] { new { testId = test, quantityPerTest = 1 }, new { testId = test, quantityPerTest = 2 } } })));
        Assert.Equal("profile_test_invalid", await ApiClient.Code(await store.PutAsync($"/api/inventory/items/{item}/profile", new { tests = new[] { new { testId = 987654, quantityPerTest = 1 } } })));
        Assert.Equal(HttpStatusCode.BadRequest, (await store.PutAsync($"/api/inventory/items/{item}/profile", new { tests = new[] { new { testId = test, quantityPerTest = 0 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await store.PutAsync("/api/inventory/items/987654/profile", new { tests = Array.Empty<object>() })).StatusCode);
    }

    // ---------------------------------------------------------------- alerts

    [Fact]
    public async Task Alerts_list_low_stock_and_batches_that_are_expiring_or_expired()
    {
        var store = await Store();
        var sup = await NewSupplier(store);
        var low = await NewItem(store, reorder: 20);
        var fine = await NewItem(store, reorder: 5);
        await Buy(store, sup, Line(low, "L1", 10, Soon(300)), Line(fine, "F1", 50, Soon(15)), Line(fine, "F2", 5, Soon(300)));

        var alerts = await ApiClient.Body(await store.GetAsync("/api/inventory/alerts"));
        Assert.Contains(alerts.GetProperty("low").EnumerateArray(), l => l.GetProperty("itemId").GetInt32() == low);
        Assert.DoesNotContain(alerts.GetProperty("low").EnumerateArray(), l => l.GetProperty("itemId").GetInt32() == fine);
        var expiring = alerts.GetProperty("expiring").EnumerateArray().Single(e => e.GetProperty("itemId").GetInt32() == fine);
        Assert.Equal("F1", expiring.GetProperty("batchNo").GetString());
        Assert.InRange(expiring.GetProperty("daysLeft").GetInt32(), 14, 16);

        // A batch that passes its expiry date moves to "expired" and is no longer used for tests.
        await using (var sys = fx.System())
            await sys.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.StockBatches SET ExpiryDate = DATEADD(day, -2, CAST(GETDATE() AS date)) WHERE ItemId = {fine} AND BatchNo = 'F1'");
        var later = await ApiClient.Body(await store.GetAsync("/api/inventory/alerts"));
        Assert.Contains(later.GetProperty("expired").EnumerateArray(), e => e.GetProperty("itemId").GetInt32() == fine && e.GetProperty("batchNo").GetString() == "F1");
        Assert.DoesNotContain(later.GetProperty("expiring").EnumerateArray(), e => e.GetProperty("itemId").GetInt32() == fine);

        var lowOnly = await ApiClient.Body(await store.GetAsync("/api/inventory/items?lowOnly=true&pageSize=100"));
        Assert.Contains(lowOnly.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetInt32() == low);
        Assert.DoesNotContain(lowOnly.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetInt32() == fine);
    }

    // ---------------------------------------------------------------- isolation, anonymous access, database

    [Fact]
    public async Task Another_organisation_sees_none_of_it_and_anonymous_or_token_less_requests_are_refused()
    {
        var store = await Store();
        var sup = await NewSupplier(store);
        var item = await NewItem(store);
        await Buy(store, sup, Line(item, "B1", 10, Soon(300)));
        var batch = BatchIdOf(await Item(store, item), "B1");

        var other = await At(fx.OrgBMainBranch, fx.OrgB, "INVENTORY");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/inventory/items/{item}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync("/api/inventory/movements", new { batchId = batch, kind = 1, quantity = 1 })).StatusCode);
        Assert.Equal("supplier_invalid", await ApiClient.Code(await other.PostAsync("/api/inventory/purchases", new { idempotencyKey = Guid.NewGuid(), supplierId = sup, lines = new[] { Line(item, "X", 1, Soon(100)) } })));
        Assert.DoesNotContain((await ApiClient.Body(await other.GetAsync("/api/inventory/items?pageSize=100"))).GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetInt32() == item);
        Assert.DoesNotContain((await ApiClient.Body(await other.GetAsync("/api/inventory/ledger?pageSize=100"))).GetProperty("items").EnumerateArray(), l => l.GetProperty("itemId").GetInt32() == item);
        Assert.Equal(10m, OnHand(await Item(store, item), "B1"));

        var anon = await Api.ClientAsync();
        foreach (var url in new[] { "/api/inventory/items", "/api/inventory/suppliers", "/api/inventory/purchases", "/api/inventory/ledger", "/api/inventory/alerts", "/api/inventory/shortfalls" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(url)).StatusCode);

        // State-changing requests need the anti-forgery header.
        Assert.Equal(HttpStatusCode.BadRequest, (await store.PostAsync("/api/inventory/movements", new { batchId = batch, kind = 1, quantity = 1 }, withCsrf: false)).StatusCode);
        Assert.Equal(10m, OnHand(await Item(store, item), "B1"));
    }

    [Fact]
    public async Task The_database_refuses_negative_stock_and_a_ledger_line_that_points_the_wrong_way()
    {
        var store = await Store();
        var sup = await NewSupplier(store);
        var item = await NewItem(store);
        await Buy(store, sup, Line(item, "B1", 10, Soon(300)));
        var batch = BatchIdOf(await Item(store, item), "B1");

        await using var db = fx.System();
        var ex = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.StockBatches SET QuantityOnHand = -1 WHERE Id = {batch}"));
        Assert.Contains("CK_StockBatches_NotNegative", ex.Message);
        // Stock in must be positive, stock out negative.
        var bad = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO inventory.StockMovements (OrganizationId, BranchId, ItemId, BatchId, Type, Quantity, BalanceAfter, UserId, AtUtc) SELECT OrganizationId, BranchId, ItemId, Id, 2, 5, 5, 1, GETUTCDATE() FROM inventory.StockBatches WHERE Id = {batch}"));
        Assert.Contains("CK_StockMovements_Direction", bad.Message);
        // The ledger has no update path in the API, and its automatic charges are unique per test run.
        Assert.Equal(10m, OnHand(await Item(store, item), "B1"));
    }
}
