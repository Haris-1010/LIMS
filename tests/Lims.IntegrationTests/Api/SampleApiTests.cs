using System.Net;
using System.Text.Json;
using Lims.Domain.Samples;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

/// <summary>Sample planning, collection, receiving, rejection and visibility through the real API.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SampleApiTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;
    private static string Code(string p) => p + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<ApiClient> At(int orgBranch, int org, params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(org, roles)));
        ApiClient.Ok(await c.PostAsync("/api/auth/active-branch", new { branchId = orgBranch }));
        await c.RefreshCsrfAsync();
        return c;
    }

    private Task<ApiClient> A(params string[] roles) => At(fx.OrgAMainBranch, fx.OrgA, roles);

    private sealed record Fixture(int TestSerum1, int TestSerum2, int TestBlood, int PatientId);

    private async Task<Fixture> Setup()
    {
        var admin = await A("SYSADMIN");
        var cat = (await ApiClient.Body(await admin.PostAsync("/api/catalog/categories", new { code = Code("C"), nameEn = "Cat" }))).GetProperty("id").GetInt32();
        var dept = (await ApiClient.Body(await admin.GetAsync("/api/organization/departments")))[0].GetProperty("id").GetInt32();
        async Task<int> Test(int sampleType) => (await ApiClient.Body(await admin.PostAsync("/api/catalog/tests",
            new { code = Code("T"), nameEn = "Fixture", categoryId = cat, departmentId = dept, sampleTypeId = sampleType, price = 100 }))).GetProperty("id").GetInt32();
        var patient = (await ApiClient.Body(await admin.PostAsync("/api/patients", new { fullName = "Sample Patient " + Code(""), sex = 1, ageYears = 30, confirmNotDuplicate = true }))).GetProperty("id").GetInt32();
        return new Fixture(await Test(1), await Test(1), await Test(2), patient);
    }

    private static async Task<JsonElement> NewOrder(ApiClient c, Fixture f, params int[] testIds)
    {
        var res = await c.PostAsync("/api/orders", new
        {
            idempotencyKey = Guid.NewGuid(), patientId = f.PatientId, priority = 0,
            items = testIds.Select(t => (object)new { testId = t }).ToArray(),
        });
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    private static async Task<JsonElement> SamplesOf(ApiClient c, long orderId)
    {
        var res = await c.GetAsync($"/api/orders/{orderId}/samples");
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    private static readonly object Confirmed = new { identityConfirmed = true };

    [Fact]
    public async Task Tests_are_grouped_into_one_sample_per_sample_type_with_unique_barcodes()
    {
        var f = await Setup();
        var reception = await A("RECEPTION");
        var order = await NewOrder(reception, f, f.TestSerum1, f.TestSerum2, f.TestBlood);
        var samples = await SamplesOf(reception, order.GetProperty("id").GetInt64());
        Assert.Equal(2, samples.GetArrayLength());
        var barcodes = samples.EnumerateArray().Select(s => s.GetProperty("barcode").GetString()).ToList();
        Assert.Equal(2, barcodes.Distinct().Count());
        Assert.All(barcodes, b => Assert.Matches(@"^S-\d{6}-\d{4}$", b));
        Assert.All(samples.EnumerateArray(), s => Assert.Equal((int)SampleStatus.Planned, s.GetProperty("status").GetInt32()));
        Assert.Contains(samples.EnumerateArray(), s => s.GetProperty("testCodes").GetArrayLength() == 2);
        Assert.All(samples.EnumerateArray(), s => Assert.True(s.GetProperty("barcodeModules").GetString()!.All(ch => ch is '0' or '1')));
    }

    [Fact]
    public async Task Collecting_needs_permission_and_identity_confirmation_and_moves_tests_forward()
    {
        var f = await Setup();
        var reception = await A("RECEPTION");
        var order = await NewOrder(reception, f, f.TestSerum1);
        var id = order.GetProperty("id").GetInt64();
        var sample = (await SamplesOf(reception, id))[0];
        var sid = sample.GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync($"/api/samples/{sid}/collect", Confirmed)).StatusCode);

        var phleb = await A("PHLEBOTOMY");
        Assert.Equal("identity_confirmation_required", await ApiClient.Code(await phleb.PostAsync($"/api/samples/{sid}/collect", new { identityConfirmed = false })));
        ApiClient.Ok(await phleb.PostAsync($"/api/samples/{sid}/collect", Confirmed));
        Assert.Equal("sample_already_collected", await ApiClient.Code(await phleb.PostAsync($"/api/samples/{sid}/collect", Confirmed)));

        await using var db = fx.System();
        Assert.Equal(SampleStatus.Collected, (await db.Samples.IgnoreQueryFilters().SingleAsync(s => s.Id == sid)).Status);
        var itemStatus = await db.OrderItems.IgnoreQueryFilters().Where(i => i.OrderId == id).Select(i => (int)i.Status).ToListAsync();
        Assert.All(itemStatus, s => Assert.Equal((int)Lims.Domain.Orders.OrderItemStatus.Collected, s));
        Assert.True(await db.SampleEvents.IgnoreQueryFilters().AnyAsync(e => e.SampleId == sid && e.Type == SampleEventTypes.Collected));
    }

    [Fact]
    public async Task Parallel_collects_have_exactly_one_winner()
    {
        var f = await Setup();
        var phleb = await A("PHLEBOTOMY");
        var order = await NewOrder(await A("RECEPTION"), f, f.TestSerum1);
        var sid = (await SamplesOf(phleb, order.GetProperty("id").GetInt64()))[0].GetProperty("id").GetInt64();
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => phleb.PostAsync($"/api/samples/{sid}/collect", Confirmed)));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Receiving_requires_a_collected_sample_and_rejection_creates_a_replacement()
    {
        var f = await Setup();
        var reception = await A("RECEPTION");
        var phleb = await A("PHLEBOTOMY");
        var tech = await A("TECHNICIAN");
        var order = await NewOrder(reception, f, f.TestSerum1);
        var oid = order.GetProperty("id").GetInt64();
        var sid = (await SamplesOf(reception, oid))[0].GetProperty("id").GetInt64();

        Assert.Equal("sample_not_collected", await ApiClient.Code(await tech.PostAsync($"/api/samples/{sid}/receive")));
        ApiClient.Ok(await phleb.PostAsync($"/api/samples/{sid}/collect", Confirmed));
        ApiClient.Ok(await tech.PostAsync($"/api/samples/{sid}/receive"));
        Assert.Equal("sample_already_received", await ApiClient.Code(await tech.PostAsync($"/api/samples/{sid}/receive")));

        var reasons = await ApiClient.Body(await tech.GetAsync("/api/samples/rejection-reasons"));
        Assert.Equal(10, reasons.GetArrayLength());
        var haemolysed = reasons.EnumerateArray().First(r => r.GetProperty("code").GetString() == "HAEMOLYSED").GetProperty("id").GetInt32();
        var other = reasons.EnumerateArray().First(r => r.GetProperty("code").GetString() == "OTHER").GetProperty("id").GetInt32();

        Assert.Equal("rejection_reason_invalid", await ApiClient.Code(await tech.PostAsync($"/api/samples/{sid}/reject", new { reasonId = 999999 })));
        Assert.Equal("rejection_note_required", await ApiClient.Code(await tech.PostAsync($"/api/samples/{sid}/reject", new { reasonId = other })));
        ApiClient.Ok(await tech.PostAsync($"/api/samples/{sid}/reject", new { reasonId = haemolysed, requestRecollection = true }));
        Assert.Equal("sample_closed", await ApiClient.Code(await tech.PostAsync($"/api/samples/{sid}/reject", new { reasonId = haemolysed })));

        var after = await SamplesOf(reception, oid);
        var live = after.EnumerateArray().Where(s => s.GetProperty("status").GetInt32() == (int)SampleStatus.Planned).ToList();
        Assert.Single(live);
        Assert.True(live[0].GetProperty("isRecollection").GetBoolean());
        Assert.NotEqual(sid, live[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Cancelling_a_line_cancels_its_planned_sample_but_never_a_collected_one()
    {
        var f = await Setup();
        var admin = await A("SYSADMIN");
        var order = await NewOrder(admin, f, f.TestSerum1, f.TestBlood);
        var oid = order.GetProperty("id").GetInt64();
        var samples = await SamplesOf(admin, oid);
        Assert.Equal(2, samples.GetArrayLength());
        // Serum and blood are both single-test samples here; collect one, then cancel the other test's line.
        var first = samples[0];
        var second = samples[1];
        ApiClient.Ok(await admin.PostAsync($"/api/samples/{first.GetProperty("id").GetInt64()}/collect", Confirmed));
        var secondCode = second.GetProperty("testCodes")[0].GetString();
        var line = order.GetProperty("items").EnumerateArray().First(i => i.GetProperty("testCode").GetString() == secondCode).GetProperty("invoiceLineId").GetInt64();
        ApiClient.Ok(await admin.PostAsync($"/api/orders/{oid}/lines/{line}/cancel", new { reason = "Test cancel" }));

        await using var db = fx.System();
        var statuses = await db.Samples.IgnoreQueryFilters().Where(s => s.OrderId == oid).ToDictionaryAsync(s => s.Id, s => s.Status);
        Assert.Equal(SampleStatus.Collected, statuses[first.GetProperty("id").GetInt64()]);
        Assert.Equal(SampleStatus.Cancelled, statuses[second.GetProperty("id").GetInt64()]);
    }

    [Fact]
    public async Task Barcode_lookup_normalises_scanner_input_and_hides_other_organisations()
    {
        var f = await Setup();
        var reception = await A("RECEPTION");
        var order = await NewOrder(reception, f, f.TestSerum1);
        var barcode = (await SamplesOf(reception, order.GetProperty("id").GetInt64()))[0].GetProperty("barcode").GetString()!;

        var found = await reception.GetAsync($"/api/samples/by-barcode/{Uri.EscapeDataString(" " + barcode.ToLowerInvariant() + " ")}");
        ApiClient.Ok(found);
        Assert.Equal(barcode, (await ApiClient.Body(found)).GetProperty("sample").GetProperty("barcode").GetString());

        var other = await At(fx.OrgBMainBranch, fx.OrgB, "SYSADMIN");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/samples/by-barcode/{barcode}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/samples/{(await ApiClient.Body(found)).GetProperty("sample").GetProperty("id").GetInt64()}")).StatusCode);
    }

    [Fact]
    public async Task A_branch_without_a_link_to_the_sample_cannot_see_it()
    {
        var f = await Setup();
        var reception = await A("RECEPTION");
        var order = await NewOrder(reception, f, f.TestSerum1);
        var sid = (await SamplesOf(reception, order.GetProperty("id").GetInt64()))[0].GetProperty("id").GetInt64();

        // A user limited to the second branch has no origin/current/destination link to a main-branch sample.
        var username = await Api.CreateUserAsync(fx.OrgA, ["TECHNICIAN"], allBranches: false, branchIds: [fx.OrgASecondBranch]);
        var other = await Api.ClientAsync();
        ApiClient.Ok(await other.LoginAsync(username));
        ApiClient.Ok(await other.PostAsync("/api/auth/active-branch", new { branchId = fx.OrgASecondBranch }));
        await other.RefreshCsrfAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/samples/{sid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/samples/{sid}/receive")).StatusCode);
    }

    [Fact]
    public async Task Database_refuses_a_second_live_sample_for_the_same_order_and_type()
    {
        var f = await Setup();
        var order = await NewOrder(await A("RECEPTION"), f, f.TestSerum1);
        var oid = order.GetProperty("id").GetInt64();
        await using var db = fx.System();
        var existing = await db.Samples.IgnoreQueryFilters().SingleAsync(s => s.OrderId == oid);
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO lab.Samples (OrganizationId,Barcode,OrderId,PatientId,SampleTypeId,Status,Priority,OriginBranchId,CurrentBranchId,IdentityConfirmed,LabelPrintCount,CreatedAtUtc) " +
            "SELECT OrganizationId,'DUP-'+CONVERT(varchar(20),Id),OrderId,PatientId,SampleTypeId,0,Priority,OriginBranchId,CurrentBranchId,0,0,CreatedAtUtc FROM lab.Samples WHERE Id = {0}", existing.Id));
        Assert.Contains("UX_Samples_OneLivePerOrderType", ex.InnerException?.Message ?? ex.Message);
    }

    [Fact]
    public async Task Anonymous_and_token_less_requests_are_refused_on_every_sample_endpoint()
    {
        var anon = await Api.ClientAsync();
        foreach (var url in new[] { "/api/samples", "/api/samples/collection-queue", "/api/samples/1", "/api/samples/by-barcode/X", "/api/samples/rejection-reasons", "/api/sample-transfers", "/api/sample-transfers/1", "/api/sample-transfers/destinations", "/api/orders/1/samples" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(url)).StatusCode);

        var user = await A("SYSADMIN");
        foreach (var url in new[] { "/api/samples/1/collect", "/api/samples/1/receive", "/api/samples/1/reject", "/api/sample-transfers" })
            Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsync(url, new { }, withCsrf: false)).StatusCode);
    }

    [Fact]
    public async Task Roles_only_reach_the_sample_actions_they_hold_a_permission_for()
    {
        var f = await Setup();
        var order = await NewOrder(await A("RECEPTION"), f, f.TestSerum1);
        var sid = (await SamplesOf(await A("RECEPTION"), order.GetProperty("id").GetInt64()))[0].GetProperty("id").GetInt64();

        var cashier = await A("CASHIER");                       // no samples.* permissions at all
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/api/samples")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync($"/api/orders/{order.GetProperty("id").GetInt64()}/samples")).StatusCode);

        var reception = await A("RECEPTION");                   // view only
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync("/api/samples")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync($"/api/samples/{sid}/receive")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync($"/api/samples/{sid}/reject", new { reasonId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync("/api/samples/rejection-reasons", new { code = "X1", nameEn = "X" })).StatusCode);

        var tech = await A("TECHNICIAN");                       // receives, cannot collect or edit reasons
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync($"/api/samples/{sid}/collect", Confirmed)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync("/api/samples/rejection-reasons", new { code = "X2", nameEn = "X" })).StatusCode);

        var mgr = await A("LABMGR");
        var code = Code("R");
        ApiClient.Ok(await mgr.PostAsync("/api/samples/rejection-reasons", new { code, nameEn = "Custom reason", nameUr = "حسب ضرورت", requiresNote = true, sortOrder = 50 }));
        Assert.Equal("reason_code_taken", await ApiClient.Code(await mgr.PostAsync("/api/samples/rejection-reasons", new { code, nameEn = "Again" })));
    }
}
