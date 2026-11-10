using System.Net;
using Lims.Domain.Samples;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

/// <summary>Transfer manifests between branches through the real API.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SampleTransferApiTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;
    private static string Code(string p) => p + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    private static readonly object Confirmed = new { identityConfirmed = true };

    private async Task<ApiClient> At(int branch, params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(fx.OrgA, roles)));
        ApiClient.Ok(await c.PostAsync("/api/auth/active-branch", new { branchId = branch }));
        await c.RefreshCsrfAsync();
        return c;
    }

    /// <summary>A collected sample at the main branch.</summary>
    private async Task<long> CollectedSample()
    {
        var admin = await At(fx.OrgAMainBranch, "SYSADMIN");
        var cat = (await ApiClient.Body(await admin.PostAsync("/api/catalog/categories", new { code = Code("C"), nameEn = "Cat" }))).GetProperty("id").GetInt32();
        var dept = (await ApiClient.Body(await admin.GetAsync("/api/organization/departments")))[0].GetProperty("id").GetInt32();
        var test = (await ApiClient.Body(await admin.PostAsync("/api/catalog/tests", new { code = Code("T"), nameEn = "T", categoryId = cat, departmentId = dept, sampleTypeId = 1, price = 100 }))).GetProperty("id").GetInt32();
        var patient = (await ApiClient.Body(await admin.PostAsync("/api/patients", new { fullName = "Transfer Patient " + Code(""), sex = 1, ageYears = 30, confirmNotDuplicate = true }))).GetProperty("id").GetInt32();
        var order = await ApiClient.Body(await admin.PostAsync("/api/orders", new { idempotencyKey = Guid.NewGuid(), patientId = patient, priority = 0, items = new object[] { new { testId = test } } }));
        var sid = (await ApiClient.Body(await admin.GetAsync($"/api/orders/{order.GetProperty("id").GetInt64()}/samples")))[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await admin.PostAsync($"/api/samples/{sid}/collect", Confirmed));
        return sid;
    }

    private static object Manifest(int to, params long[] ids) => new { toBranchId = to, sampleIds = ids };

    [Fact]
    public async Task Dispatch_then_receive_at_destination_completes_the_manifest()
    {
        var sid = await CollectedSample();
        var sender = await At(fx.OrgAMainBranch, "TECHNICIAN");
        var res = await sender.PostAsync("/api/sample-transfers", Manifest(fx.OrgASecondBranch, sid));
        ApiClient.Ok(res);
        var t = await ApiClient.Body(res);
        Assert.Matches(@"^TRF-", t.GetProperty("transfer").GetProperty("transferNo").GetString());
        var tid = t.GetProperty("transfer").GetProperty("id").GetInt64();

        Assert.Equal((int)SampleStatus.InTransit, (await ApiClient.Body(await sender.GetAsync($"/api/samples/{sid}"))).GetProperty("sample").GetProperty("status").GetInt32());
        Assert.Equal("sample_not_at_branch", await ApiClient.Code(await sender.PostAsync($"/api/samples/{sid}/receive")));
        var receiver = await At(fx.OrgASecondBranch, "TECHNICIAN");
        ApiClient.Ok(await receiver.PostAsync($"/api/samples/{sid}/receive"));

        var after = await ApiClient.Body(await receiver.GetAsync($"/api/sample-transfers/{tid}"));
        Assert.Equal((int)TransferStatus.Completed, after.GetProperty("transfer").GetProperty("status").GetInt32());
        Assert.Equal(1, after.GetProperty("transfer").GetProperty("receivedCount").GetInt32());
    }

    [Fact]
    public async Task Same_sample_cannot_be_on_two_manifests_even_in_a_race()
    {
        var sid = await CollectedSample();
        var a = await At(fx.OrgAMainBranch, "TECHNICIAN");
        var b = await At(fx.OrgAMainBranch, "TECHNICIAN");
        var results = await Task.WhenAll(
            a.PostAsync("/api/sample-transfers", Manifest(fx.OrgASecondBranch, sid)),
            b.PostAsync("/api/sample-transfers", Manifest(fx.OrgASecondBranch, sid)));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var db = fx.System();
        Assert.Equal(1, await db.SampleTransferItems.IgnoreQueryFilters().CountAsync(i => i.SampleId == sid));
    }

    [Fact]
    public async Task Validation_permissions_and_cancel_rules()
    {
        var sid = await CollectedSample();
        var tech = await At(fx.OrgAMainBranch, "TECHNICIAN");
        Assert.Equal("transfer_same_branch", await ApiClient.Code(await tech.PostAsync("/api/sample-transfers", Manifest(fx.OrgAMainBranch, sid))));
        Assert.Equal("transfer_empty", await ApiClient.Code(await tech.PostAsync("/api/sample-transfers", Manifest(fx.OrgASecondBranch))));
        Assert.Equal("transfer_branch_invalid", await ApiClient.Code(await tech.PostAsync("/api/sample-transfers", Manifest(999999, sid))));
        var phleb = await At(fx.OrgAMainBranch, "PHLEBOTOMY");
        Assert.Equal(HttpStatusCode.Forbidden, (await phleb.PostAsync("/api/sample-transfers", Manifest(fx.OrgASecondBranch, sid))).StatusCode);

        var t = await ApiClient.Body(await tech.PostAsync("/api/sample-transfers", Manifest(fx.OrgASecondBranch, sid)));
        var tid = t.GetProperty("transfer").GetProperty("id").GetInt64();
        var receiver = await At(fx.OrgASecondBranch, "TECHNICIAN");
        Assert.Equal("transfer_not_sender", await ApiClient.Code(await receiver.PostAsync($"/api/sample-transfers/{tid}/cancel")));
        Assert.Equal("transfer_not_destination", await ApiClient.Code(await tech.PostAsync($"/api/sample-transfers/{tid}/complete")));

        ApiClient.Ok(await tech.PostAsync($"/api/sample-transfers/{tid}/cancel"));
        Assert.Equal((int)SampleStatus.Collected, (await ApiClient.Body(await tech.GetAsync($"/api/samples/{sid}"))).GetProperty("sample").GetProperty("status").GetInt32());
        Assert.Equal("transfer_not_in_transit", await ApiClient.Code(await tech.PostAsync($"/api/sample-transfers/{tid}/cancel")));
        ApiClient.Ok(await tech.PostAsync("/api/sample-transfers", Manifest(fx.OrgASecondBranch, sid)));   // can go out again
    }

    [Fact]
    public async Task Closing_with_missing_samples_marks_them_and_a_late_arrival_is_still_receivable()
    {
        var sid = await CollectedSample();
        var tech = await At(fx.OrgAMainBranch, "TECHNICIAN");
        var tid = (await ApiClient.Body(await tech.PostAsync("/api/sample-transfers", Manifest(fx.OrgASecondBranch, sid)))).GetProperty("transfer").GetProperty("id").GetInt64();
        var receiver = await At(fx.OrgASecondBranch, "TECHNICIAN");
        var done = await ApiClient.Body(await receiver.PostAsync($"/api/sample-transfers/{tid}/complete"));
        Assert.Equal(1, done.GetProperty("transfer").GetProperty("missingCount").GetInt32());
        ApiClient.Ok(await receiver.PostAsync($"/api/samples/{sid}/receive"));
        await using var db = fx.System();
        Assert.False((await db.SampleTransferItems.IgnoreQueryFilters().SingleAsync(i => i.SampleId == sid)).MarkedMissing);
    }

    [Fact]
    public async Task Other_organisations_cannot_see_a_manifest()
    {
        var sid = await CollectedSample();
        var tech = await At(fx.OrgAMainBranch, "TECHNICIAN");
        var tid = (await ApiClient.Body(await tech.PostAsync("/api/sample-transfers", Manifest(fx.OrgASecondBranch, sid)))).GetProperty("transfer").GetProperty("id").GetInt64();
        var other = await Api.ClientAsync();
        ApiClient.Ok(await other.LoginAsync(await Api.CreateUserAsync(fx.OrgB, ["SYSADMIN"])));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/sample-transfers/{tid}")).StatusCode);
    }
}
