using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

/// <summary>The live dashboard, the audit viewer and the system settings screen's API.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class Phase12ApiTests(DatabaseFixture fx)
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

    private static async Task<JsonElement> Get(ApiClient c, string url)
    {
        var res = await c.GetAsync(url);
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    private async Task<int> NewTest(ApiClient admin, decimal price = 500)
    {
        var cat = (await ApiClient.Body(await admin.PostAsync("/api/catalog/categories", new { code = Code("C"), nameEn = "Cat" }))).GetProperty("id").GetInt32();
        var dept = (await ApiClient.Body(await admin.GetAsync("/api/organization/departments")))[0].GetProperty("id").GetInt32();
        return (await ApiClient.Body(await admin.PostAsync("/api/catalog/tests", new { code = Code("T"), nameEn = "Dash test", categoryId = cat, departmentId = dept, sampleTypeId = 1, price }))).GetProperty("id").GetInt32();
    }

    private static async Task<int> NewPatient(ApiClient c, string name, string? cnic = null) =>
        (await ApiClient.Body(await c.PostAsync("/api/patients", new { fullName = name, sex = 1, ageYears = 40, cnic, confirmNotDuplicate = true }))).GetProperty("id").GetInt32();

    // ---------------------------------------------------------------- dashboard

    [Fact]
    public async Task The_dashboard_shows_live_figures_that_move_with_the_work_done()
    {
        var admin = await A("SYSADMIN");
        var reception = await A("RECEPTION");
        var test = await NewTest(admin, 500);

        var before = await Get(admin, "/api/dashboard/summary?range=today");
        Assert.False(before.TryGetProperty("isPreview", out _));   // no sample data any more
        var b = before.GetProperty("stats");

        var patient = await NewPatient(reception, "Dashboard Patient " + Code(""));
        var order = await reception.PostAsync("/api/orders", new { idempotencyKey = Guid.NewGuid(), patientId = patient, priority = 0, items = new object[] { new { testId = test } }, payment = new { method = 2, amount = 200, reference = "9999" } });
        ApiClient.Ok(order);

        var after = await Get(admin, "/api/dashboard/summary?range=today");
        var a = after.GetProperty("stats");
        Assert.Equal(b.GetProperty("totalPatients").GetInt32() + 1, a.GetProperty("totalPatients").GetInt32());
        Assert.Equal(b.GetProperty("registrations").GetInt32() + 1, a.GetProperty("registrations").GetInt32());
        Assert.Equal(b.GetProperty("orders").GetInt32() + 1, a.GetProperty("orders").GetInt32());
        Assert.Equal(b.GetProperty("cashCollected").GetDecimal() + 200m, a.GetProperty("cashCollected").GetDecimal());
        Assert.Equal(b.GetProperty("pendingSamples").GetInt32() + 1, a.GetProperty("pendingSamples").GetInt32());   // the planned sample

        // The pipeline and the feed follow.
        int Stage(JsonElement d, string key) => d.GetProperty("pipeline").EnumerateArray().Single(p => p.GetProperty("key").GetString() == key).GetProperty("count").GetInt32();
        Assert.Equal(Stage(before, "awaitingCollection") + 1, Stage(after, "awaitingCollection"));
        var feed = after.GetProperty("activity").EnumerateArray().Select(x => x.GetProperty("type").GetString()).ToList();
        Assert.Contains("registration", feed);
        Assert.Contains("payment", feed);
        Assert.Contains(after.GetProperty("trend").EnumerateArray(), t => t.GetProperty("cash").GetDecimal() >= 200m);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"), after.GetProperty("trend").EnumerateArray().Last().GetProperty("date").GetString());
    }

    [Fact]
    public async Task A_sample_left_uncollected_for_more_than_a_day_is_overdue()
    {
        var admin = await A("SYSADMIN");
        var reception = await A("RECEPTION");
        var test = await NewTest(admin);
        var patient = await NewPatient(reception, "Overdue Patient " + Code(""));
        var before = (await Get(admin, "/api/dashboard/summary?range=today")).GetProperty("stats");
        var orderId = (await ApiClient.Body(await reception.PostAsync("/api/orders", new { idempotencyKey = Guid.NewGuid(), patientId = patient, priority = 0, items = new object[] { new { testId = test } } }))).GetProperty("id").GetInt64();
        var fresh = (await Get(admin, "/api/dashboard/summary?range=today")).GetProperty("stats");
        Assert.Equal(before.GetProperty("overdueSamples").GetInt32(), fresh.GetProperty("overdueSamples").GetInt32());   // just created: not overdue

        await using (var sys = fx.System())
            await sys.Database.ExecuteSqlInterpolatedAsync($"UPDATE lab.Samples SET CreatedAtUtc = DATEADD(hour, -30, GETUTCDATE()) WHERE OrderId = {orderId}");
        var late = (await Get(admin, "/api/dashboard/summary?range=today")).GetProperty("stats");
        Assert.Equal(before.GetProperty("overdueSamples").GetInt32() + 1, late.GetProperty("overdueSamples").GetInt32());
    }

    [Fact]
    public async Task Each_person_sees_only_the_parts_of_the_dashboard_their_permissions_allow()
    {
        var admin = await A("SYSADMIN");
        var all = (await Get(admin, "/api/dashboard/summary?range=d7")).GetProperty("sections");
        Assert.All(all.EnumerateObject(), p => Assert.True(p.Value.GetBoolean(), p.Name));

        var reception = await A("RECEPTION");
        var r = await Get(reception, "/api/dashboard/summary?range=d7");
        var sections = r.GetProperty("sections");
        Assert.True(sections.GetProperty("orders").GetBoolean());
        Assert.True(sections.GetProperty("money").GetBoolean());
        Assert.False(sections.GetProperty("results").GetBoolean());
        Assert.False(sections.GetProperty("corporate").GetBoolean());
        Assert.Equal(0, r.GetProperty("stats").GetProperty("awaitingApproval").GetInt32());
        Assert.Empty(r.GetProperty("approvals").EnumerateArray());
        Assert.DoesNotContain(r.GetProperty("pipeline").EnumerateArray(), p => p.GetProperty("key").GetString() is "inProcess" or "completed");
        Assert.DoesNotContain(r.GetProperty("activity").EnumerateArray(), x => x.GetProperty("type").GetString() is "approval" or "critical");

        var inventory = await A("INVENTORY");   // no patients, orders, billing or results permission
        var i = await Get(inventory, "/api/dashboard/summary?range=today");
        Assert.False(i.GetProperty("sections").GetProperty("patients").GetBoolean());
        Assert.False(i.GetProperty("sections").GetProperty("money").GetBoolean());
        Assert.Equal(0m, i.GetProperty("stats").GetProperty("cashCollected").GetDecimal());
        Assert.Empty(i.GetProperty("trend").EnumerateArray());
    }

    [Fact]
    public async Task The_dashboard_validates_its_range_and_refuses_anonymous_requests_and_other_organisations_data()
    {
        var admin = await A("SYSADMIN");
        Assert.Equal("range_invalid", await ApiClient.Code(await admin.GetAsync("/api/dashboard/summary?range=year")));
        Assert.Equal(30, (await Get(admin, "/api/dashboard/summary?range=d30")).GetProperty("trend").GetArrayLength());
        Assert.Equal(14, (await Get(admin, "/api/dashboard/summary?range=d7")).GetProperty("trend").GetArrayLength());
        Assert.Equal(HttpStatusCode.Unauthorized, (await (await Api.ClientAsync()).GetAsync("/api/dashboard/summary")).StatusCode);

        var before = (await Get(admin, "/api/dashboard/summary?range=today")).GetProperty("stats").GetProperty("totalPatients").GetInt32();
        var other = await At(fx.OrgBMainBranch, fx.OrgB, "SYSADMIN");
        await NewPatient(other, "Other Org Patient " + Code(""));
        Assert.Equal(before, (await Get(admin, "/api/dashboard/summary?range=today")).GetProperty("stats").GetProperty("totalPatients").GetInt32());
    }

    // ---------------------------------------------------------------- audit viewer

    [Fact]
    public async Task The_audit_viewer_lists_row_changes_and_named_actions_and_masks_sensitive_fields()
    {
        var admin = await A("SYSADMIN");
        var reception = await A("RECEPTION");
        var name = "Audit Patient " + Code("");
        var patient = await NewPatient(reception, name, cnic: "35202-1234567-1");

        var list = await Get(admin, $"/api/audit?entityType=Patient&entityId={patient}&pageSize=20");
        var row = list.GetProperty("items").EnumerateArray().Single(a => a.GetProperty("action").GetString() == "Insert");
        Assert.True(row.GetProperty("userName").GetString()!.StartsWith("Test u"));
        Assert.True(row.GetProperty("changedFields").GetInt32() > 3);

        var detail = await Get(admin, $"/api/audit/{row.GetProperty("id").GetInt64()}");
        var changes = detail.GetProperty("changes").EnumerateArray().ToList();
        Assert.Contains(changes, c => c.GetProperty("field").GetString() == "FullName" && c.GetProperty("new").GetString() == name);
        var raw = detail.GetProperty("rawChanges").GetString()!;
        Assert.DoesNotContain("1234567", raw);                  // the identity number was masked when the line was written
        Assert.DoesNotContain("patient@example.com", raw);

        // A named action (a purchase of stock) is found under "Business" and not under the automatic kinds.
        var store = await A("INVENTORY");
        var sup = (await ApiClient.Body(await store.PostAsync("/api/inventory/suppliers", new { code = Code("SU"), nameEn = "Audit supplier" }))).EnumerateArray().Last().GetProperty("id").GetInt32();
        var item = (await ApiClient.Body(await store.PostAsync("/api/inventory/items", new { code = Code("IT"), nameEn = "Audit item", kind = 1, unitName = "ml", tracksExpiry = false }))).GetProperty("id").GetInt32();
        ApiClient.Ok(await store.PostAsync("/api/inventory/purchases", new { idempotencyKey = Guid.NewGuid(), supplierId = sup, lines = new[] { new { itemId = item, batchNo = "A1", quantity = 3 } } }));
        var business = await Get(admin, "/api/audit?kind=Business&action=Inventory.PurchaseReceived&pageSize=5");
        Assert.True(business.GetProperty("total").GetInt32() >= 1);
        Assert.All(business.GetProperty("items").EnumerateArray(), a => Assert.Equal("Inventory.PurchaseReceived", a.GetProperty("action").GetString()));
        var rowKinds = await Get(admin, "/api/audit?kind=Business&pageSize=100");
        Assert.DoesNotContain(rowKinds.GetProperty("items").EnumerateArray(), a => a.GetProperty("action").GetString() is "Insert" or "Update" or "Delete");

        // Filters and the filter lists.
        var filters = await Get(admin, "/api/audit/filters");
        Assert.Contains(filters.GetProperty("entityTypes").EnumerateArray(), e => e.GetString() == "Patient");
        Assert.Contains(filters.GetProperty("actions").EnumerateArray(), e => e.GetString() == "Inventory.PurchaseReceived");
        var today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        Assert.True((await Get(admin, $"/api/audit?from={today}&to={today}&entityType=Patient&entityId={patient}")).GetProperty("total").GetInt32() >= 1);
        Assert.Equal(0, (await Get(admin, $"/api/audit?to=2000-01-01&entityType=Patient&entityId={patient}")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task The_audit_log_is_read_only_needs_its_own_permission_and_never_crosses_organisations()
    {
        var admin = await A("SYSADMIN");
        var patient = await NewPatient(admin, "Audit Isolation " + Code(""));
        var id = (await Get(admin, $"/api/audit?entityType=Patient&entityId={patient}")).GetProperty("items")[0].GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.Forbidden, (await (await A("RECEPTION")).GetAsync("/api/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await A("TECHNICIAN")).GetAsync($"/api/audit/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await (await Api.ClientAsync()).GetAsync("/api/audit")).StatusCode);

        var other = await At(fx.OrgBMainBranch, fx.OrgB, "SYSADMIN");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/audit/{id}")).StatusCode);
        Assert.Equal(0, (await Get(other, $"/api/audit?entityType=Patient&entityId={patient}")).GetProperty("total").GetInt32());

        // There is no way to change or remove a line through the API.
        Assert.True((await admin.PostAsync("/api/audit", new { action = "x" })).StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound);
        Assert.True((await admin.PutAsync($"/api/audit/{id}", new { reason = "x" })).StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound);
        Assert.Equal(1, (await Get(admin, $"/api/audit?entityType=Patient&entityId={patient}")).GetProperty("items").EnumerateArray().Count(a => a.GetProperty("action").GetString() == "Insert"));
    }

    // ---------------------------------------------------------------- settings

    [Fact]
    public async Task Settings_are_validated_versioned_audited_and_take_effect()
    {
        var admin = await A("SYSADMIN");
        var all = await Get(admin, "/api/settings");
        Assert.Equal(5, all.GetArrayLength());
        var days = all.EnumerateArray().Single(s => s.GetProperty("key").GetString() == "Inventory.ExpiryAlertDays");
        Assert.Equal(1, days.GetProperty("min").GetInt32());
        Assert.Equal(365, days.GetProperty("max").GetInt32());

        // Wrong values are refused and nothing is saved.
        Assert.Equal("setting_value_invalid", await ApiClient.Code(await admin.PutAsync("/api/settings/Inventory.ExpiryAlertDays", new { value = "0", rowVersion = days.GetProperty("rowVersion").GetString() })));
        Assert.Equal("setting_value_invalid", await ApiClient.Code(await admin.PutAsync("/api/settings/Inventory.ExpiryAlertDays", new { value = "366", rowVersion = days.GetProperty("rowVersion").GetString() })));
        Assert.Equal("setting_value_invalid", await ApiClient.Code(await admin.PutAsync("/api/settings/Inventory.ExpiryAlertDays", new { value = "ten", rowVersion = days.GetProperty("rowVersion").GetString() })));
        Assert.Equal("setting_value_invalid", await ApiClient.Code(await admin.PutAsync("/api/settings/Results.AllowSelfVerification", new { value = "maybe" })));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync("/api/settings/Currency", new { value = "USD" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync("/api/settings/Nope.Nothing", new { value = "1" })).StatusCode);

        // A change takes effect where it is used (the stock alert window), and the old copy of the page can no longer overwrite it.
        var saved = await ApiClient.Body(await admin.PutAsync("/api/settings/Inventory.ExpiryAlertDays", new { value = "12", rowVersion = days.GetProperty("rowVersion").GetString() }));
        Assert.Equal("12", saved.GetProperty("value").GetString());
        Assert.False(saved.GetProperty("isDefault").GetBoolean());
        var store = await A("INVENTORY");
        Assert.Equal(12, (await Get(store, "/api/inventory/alerts")).GetProperty("expiryAlertDays").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsync("/api/settings/Inventory.ExpiryAlertDays", new { value = "20", rowVersion = days.GetProperty("rowVersion").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsync("/api/settings/Inventory.ExpiryAlertDays", new { value = "20" })).StatusCode);   // an existing setting needs its version

        // Back to the default, and the change is in the audit log with old and new value.
        var back = await ApiClient.Body(await admin.PutAsync("/api/settings/Inventory.ExpiryAlertDays", new { value = "30", rowVersion = saved.GetProperty("rowVersion").GetString() }));
        Assert.True(back.GetProperty("isDefault").GetBoolean());
        var log = await Get(admin, "/api/audit?entityType=BusinessSetting&kind=Update&pageSize=20");
        var id = log.GetProperty("items").EnumerateArray().First().GetProperty("id").GetInt64();
        var detail = await Get(admin, $"/api/audit/{id}");
        Assert.Contains(detail.GetProperty("changes").EnumerateArray(), c => c.GetProperty("field").GetString() == "Value" && c.GetProperty("old").GetString() == "12" && c.GetProperty("new").GetString() == "30");
    }

    [Fact]
    public async Task A_boolean_setting_can_be_switched_and_only_by_someone_allowed_to_manage_settings()
    {
        var admin = await A("SYSADMIN");
        var s = (await Get(admin, "/api/settings")).EnumerateArray().Single(x => x.GetProperty("key").GetString() == "Results.BlockApprovalUntilCriticalNotified");
        Assert.Equal("true", s.GetProperty("value").GetString());   // the default is on
        var off = await ApiClient.Body(await admin.PutAsync("/api/settings/Results.BlockApprovalUntilCriticalNotified", new { value = "FALSE", rowVersion = s.GetProperty("rowVersion").GetString() }));
        Assert.Equal("false", off.GetProperty("value").GetString());   // normalised
        ApiClient.Ok(await admin.PutAsync("/api/settings/Results.BlockApprovalUntilCriticalNotified", new { value = "true", rowVersion = off.GetProperty("rowVersion").GetString() }));

        var tech = await A("TECHNICIAN");
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PutAsync("/api/settings/Results.AllowSelfVerification", new { value = "true" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await (await Api.ClientAsync()).GetAsync("/api/settings")).StatusCode);
    }
}
