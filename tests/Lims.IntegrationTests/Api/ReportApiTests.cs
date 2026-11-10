using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Lims.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

/// <summary>Report preview, issue, versions, printing, integrity and the template, through the real API.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReportApiTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;
    private static string Code(string p) => p + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    private static readonly object Confirmed = new { identityConfirmed = true };

    private async Task<ApiClient> At(int branch, int org, params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(org, roles)));
        ApiClient.Ok(await c.PostAsync("/api/auth/active-branch", new { branchId = branch }));
        await c.RefreshCsrfAsync();
        return c;
    }

    private Task<ApiClient> A(params string[] roles) => At(fx.OrgAMainBranch, fx.OrgA, roles);

    /// <summary>HB: numeric with an approved range 10–20 (critical 5/30). MIX: a second numeric test for interim/final reports.</summary>
    private sealed record Fix(int Hb, int HbParam, int Mix, int MixParam, int PatientId);

    private async Task<Fix> Setup()
    {
        var admin = await A("SYSADMIN");
        var cat = (await ApiClient.Body(await admin.PostAsync("/api/catalog/categories", new { code = Code("C"), nameEn = "Cat" }))).GetProperty("id").GetInt32();
        var dept = (await ApiClient.Body(await admin.GetAsync("/api/organization/departments")))[0].GetProperty("id").GetInt32();
        async Task<(int Test, int Param)> Numeric()
        {
            var test = (await ApiClient.Body(await admin.PostAsync("/api/catalog/tests", new { code = Code("T"), nameEn = "Fixture " + Code(""), nameUr = "ٹیسٹ", categoryId = cat, departmentId = dept, sampleTypeId = 1, turnaroundHours = 24, price = 100 }))).GetProperty("id").GetInt32();
            var p = (await ApiClient.Body(await admin.PostAsync($"/api/catalog/tests/{test}/parameters", new { code = Code("P"), nameEn = "Analyte", nameUr = "تجزیہ", resultType = 1, unitId = 10, decimalPlaces = 1 }))).GetProperty("id").GetInt32();
            var range = (await ApiClient.Body(await admin.PostAsync($"/api/catalog/parameters/{p}/ranges", new { sex = 0, ageFromDays = 0, low = 10, high = 20, criticalLow = 5, criticalHigh = 30, source = "Fixture" }))).GetProperty("id").GetInt32();
            ApiClient.Ok(await admin.PostAsync($"/api/catalog/ranges/{range}/approve"), HttpStatusCode.NoContent);
            return (test, p);
        }
        var (hb, hbp) = await Numeric();
        var (mix, mixp) = await Numeric();
        var patient = (await ApiClient.Body(await admin.PostAsync("/api/patients", new { fullName = "Report Patient " + Code(""), fullNameUr = "رپورٹ مریض", sex = 2, ageYears = 34, confirmNotDuplicate = true }))).GetProperty("id").GetInt32();
        return new Fix(hb, hbp, mix, mixp, patient);
    }

    private sealed record Ordered(long OrderId, long[] ItemIds);

    /// <summary>One order with the given tests; every sample collected and received at the main branch.</summary>
    private async Task<Ordered> ReceivedOrder(Fix f, params int[] testIds)
    {
        var reception = await A("RECEPTION");
        var order = await ApiClient.Body(await reception.PostAsync("/api/orders", new { idempotencyKey = Guid.NewGuid(), patientId = f.PatientId, priority = 0, items = testIds.Select(t => (object)new { testId = t }).ToArray() }));
        var orderId = order.GetProperty("id").GetInt64();
        var phleb = await A("PHLEBOTOMY");
        var tech = await A("TECHNICIAN");
        foreach (var s in (await ApiClient.Body(await reception.GetAsync($"/api/orders/{orderId}/samples"))).EnumerateArray())
        {
            var sid = s.GetProperty("id").GetInt64();
            ApiClient.Ok(await phleb.PostAsync($"/api/samples/{sid}/collect", Confirmed));
            ApiClient.Ok(await tech.PostAsync($"/api/samples/{sid}/receive"));
        }
        return new Ordered(orderId, order.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt64()).ToArray());
    }

    private static long Rid(JsonElement e) => e.GetProperty("current").GetProperty("id").GetInt64();
    private static string Rv(JsonElement e) => e.GetProperty("current").GetProperty("rowVersion").GetString()!;

    /// <summary>Enters a value, submits, verifies and approves it with three different people; returns the released result.</summary>
    private async Task<JsonElement> Release(long itemId, int param, string value)
    {
        var tech = await A("TECHNICIAN");
        var saved = await ApiClient.Body(await tech.PutAsync($"/api/results/items/{itemId}/draft", new { rowVersion = (string?)null, comment = (string?)null, values = new[] { new { parameterId = param, value, optionId = (int?)null } } }));
        var submitted = await Step(tech, $"/api/results/{Rid(saved)}/submit", new { rowVersion = Rv(saved) });
        var patho = await A("PATHOLOGIST");
        var verified = await Step(patho, $"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted) });
        return await Step(patho, $"/api/results/{Rid(verified)}/approve", new { rowVersion = Rv(verified) });
    }

    private static async Task<JsonElement> Step(ApiClient c, string path, object body)
    {
        var res = await c.PostAsync(path, body);
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    private static object Issue(int language = 0, string? reason = null) => new { language, reason };

    private static async Task<JsonElement> IssueOk(ApiClient c, long orderId, int language = 0, string? reason = null)
    {
        var res = await c.PostAsync($"/api/orders/{orderId}/reports", Issue(language, reason));
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    [Fact]
    public async Task Preview_stores_nothing_and_needs_a_released_result()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb);
        var patho = await A("PATHOLOGIST");
        Assert.Equal("report_nothing_released", await ApiClient.Code(await patho.PostAsync($"/api/orders/{o.OrderId}/reports/preview", new { language = 0 })));
        Assert.Equal("report_nothing_released", await ApiClient.Code(await patho.PostAsync($"/api/orders/{o.OrderId}/reports", Issue())));

        await Release(o.ItemIds[0], f.HbParam, "15");
        var preview = await ApiClient.Body(await patho.PostAsync($"/api/orders/{o.OrderId}/reports/preview", new { language = 0 }));
        Assert.True(preview.GetProperty("isPreview").GetBoolean());
        Assert.Equal("PREVIEW", preview.GetProperty("snapshot").GetProperty("reportNo").GetString());
        Assert.Equal(0, (await ApiClient.Body(await patho.GetAsync($"/api/orders/{o.OrderId}/reports"))).GetArrayLength());   // nothing was stored
        Assert.Equal("report_language_invalid", await ApiClient.Code(await patho.PostAsync($"/api/orders/{o.OrderId}/reports/preview", new { language = 99 })));
        Assert.Equal(HttpStatusCode.NotFound, (await patho.PostAsync("/api/orders/999999999/reports/preview", new { language = 0 })).StatusCode);
    }

    [Fact]
    public async Task Issuing_freezes_exactly_what_was_released_and_a_later_change_cannot_alter_it()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var patho = await A("PATHOLOGIST");

        var report = await IssueOk(patho, o.OrderId, language: 2);
        var meta = report.GetProperty("meta");
        var snap = report.GetProperty("snapshot");
        Assert.Matches(@"^REP-\d{4}-\d{4}$", meta.GetProperty("reportNo").GetString());
        Assert.Equal(1, meta.GetProperty("version").GetInt32());
        Assert.Equal(1, meta.GetProperty("status").GetInt32());              // final: the only test is released
        Assert.Equal(2, meta.GetProperty("language").GetInt32());
        Assert.False(meta.GetProperty("isDuplicate").GetBoolean());
        var p = snap.GetProperty("sections")[0].GetProperty("tests")[0].GetProperty("parameters")[0];
        Assert.Equal("15.0", p.GetProperty("display").GetString());
        Assert.Equal(1, p.GetProperty("flag").GetInt32());
        Assert.Equal("10.0 – 20.0", p.GetProperty("referenceText").GetString());
        Assert.Equal("رپورٹ مریض", snap.GetProperty("patient").GetProperty("nameUr").GetString());
        Assert.Equal(34, snap.GetProperty("patient").GetProperty("ageYears").GetInt32());              // age on the collection date
        Assert.True(snap.GetProperty("signatories").GetArrayLength() >= 1);
        Assert.True(snap.GetProperty("signatories")[0].GetProperty("approved").GetBoolean());

        // Changing the patient's name and the test name afterwards does not change the issued report.
        var id = meta.GetProperty("id").GetInt64();
        await using (var db = fx.System())
        {
            await db.Patients.IgnoreQueryFilters().Where(x => x.Id == f.PatientId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, "Someone Else"));
            await db.Tests.IgnoreQueryFilters().Where(x => x.Id == f.Hb).ExecuteUpdateAsync(s => s.SetProperty(x => x.NameEn, "Renamed test"));
        }
        var again = await ApiClient.Body(await patho.GetAsync($"/api/reports/{id}"));
        Assert.StartsWith("Report Patient", again.GetProperty("snapshot").GetProperty("patient").GetProperty("nameEn").GetString());
        Assert.NotEqual("Renamed test", again.GetProperty("snapshot").GetProperty("sections")[0].GetProperty("tests")[0].GetProperty("nameEn").GetString());

        var verify = await ApiClient.Body(await patho.GetAsync($"/api/reports/{id}/verify"));
        Assert.True(verify.GetProperty("intact").GetBoolean());
        Assert.Equal(meta.GetProperty("fingerprint").GetString(), verify.GetProperty("fingerprint").GetString());
    }

    [Fact]
    public async Task The_lists_follow_the_report_state_and_a_current_report_cannot_be_issued_twice()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var patho = await A("PATHOLOGIST");
        async Task<bool> In(string tab) => (await ApiClient.Body(await patho.GetAsync($"/api/reports?tab={tab}&pageSize=100"))).GetProperty("items").EnumerateArray().Any(i => i.GetProperty("orderId").GetInt64() == o.OrderId);

        Assert.True(await In("ready"));
        Assert.False(await In("issued"));
        await IssueOk(patho, o.OrderId);
        Assert.False(await In("ready"));
        Assert.True(await In("issued"));
        Assert.Equal("report_up_to_date", await ApiClient.Code(await patho.PostAsync($"/api/orders/{o.OrderId}/reports", Issue(0, "again"))));
    }

    [Fact]
    public async Task An_interim_report_lists_pending_tests_and_a_later_release_makes_a_new_version_that_supersedes_it()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb, f.Mix);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var patho = await A("PATHOLOGIST");
        var v1 = await IssueOk(patho, o.OrderId);
        Assert.Equal(0, v1.GetProperty("meta").GetProperty("status").GetInt32());                         // interim
        Assert.Equal(1, v1.GetProperty("snapshot").GetProperty("pending").GetArrayLength());
        Assert.Equal(1, v1.GetProperty("snapshot").GetProperty("sections").EnumerateArray().Sum(s => s.GetProperty("tests").GetArrayLength()));
        var reportNo = v1.GetProperty("meta").GetProperty("reportNo").GetString();

        await Release(o.ItemIds[1], f.MixParam, "12");
        var versions = await ApiClient.Body(await patho.GetAsync($"/api/orders/{o.OrderId}/reports"));
        Assert.True(versions[0].GetProperty("isOutdated").GetBoolean());                                   // a newly released test

        Assert.Equal("report_reason_required", await ApiClient.Code(await patho.PostAsync($"/api/orders/{o.OrderId}/reports", Issue())));
        var v2 = await IssueOk(patho, o.OrderId, reason: "All tests now released");
        Assert.Equal(2, v2.GetProperty("meta").GetProperty("version").GetInt32());
        Assert.Equal(1, v2.GetProperty("meta").GetProperty("status").GetInt32());                          // final
        Assert.Equal(reportNo, v2.GetProperty("meta").GetProperty("reportNo").GetString());                // one number across versions
        Assert.Equal(0, v2.GetProperty("snapshot").GetProperty("pending").GetArrayLength());
        Assert.Equal("All tests now released", v2.GetProperty("snapshot").GetProperty("amendmentReason").GetString());

        var old = await ApiClient.Body(await patho.GetAsync($"/api/reports/{v1.GetProperty("meta").GetProperty("id").GetInt64()}"));
        Assert.Equal(90, old.GetProperty("meta").GetProperty("status").GetInt32());
        Assert.Equal(2, old.GetProperty("meta").GetProperty("supersededByVersion").GetInt32());
    }

    [Fact]
    public async Task Amending_a_released_result_makes_the_report_outdated_and_the_old_version_keeps_the_old_value()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb);
        var released = await Release(o.ItemIds[0], f.HbParam, "15");
        var patho = await A("PATHOLOGIST");
        var v1 = await IssueOk(patho, o.OrderId);

        var tech = await A("TECHNICIAN");
        var amending = await Step(patho, $"/api/results/{Rid(released)}/amend", new { reason = "Rerun", rowVersion = Rv(released) });
        var edited = await ApiClient.Body(await tech.PutAsync($"/api/results/items/{o.ItemIds[0]}/draft", new { rowVersion = Rv(amending), comment = (string?)null, values = new[] { new { parameterId = f.HbParam, value = "18.2", optionId = (int?)null } } }));
        var submitted = await Step(tech, $"/api/results/{Rid(edited)}/submit", new { rowVersion = Rv(edited) });
        var verified = await Step(patho, $"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted) });
        await Step(patho, $"/api/results/{Rid(verified)}/approve", new { rowVersion = Rv(verified) });

        Assert.True((await ApiClient.Body(await patho.GetAsync($"/api/orders/{o.OrderId}/reports")))[0].GetProperty("isOutdated").GetBoolean());
        var v2 = await IssueOk(patho, o.OrderId, reason: "Result amended");
        Assert.Equal("18.2", v2.GetProperty("snapshot").GetProperty("sections")[0].GetProperty("tests")[0].GetProperty("parameters")[0].GetProperty("display").GetString());
        var oldView = await ApiClient.Body(await patho.GetAsync($"/api/reports/{v1.GetProperty("meta").GetProperty("id").GetInt64()}"));
        Assert.Equal("15.0", oldView.GetProperty("snapshot").GetProperty("sections")[0].GetProperty("tests")[0].GetProperty("parameters")[0].GetProperty("display").GetString());
    }

    [Fact]
    public async Task Prints_are_counted_atomically_and_every_print_after_the_first_is_a_copy()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var patho = await A("PATHOLOGIST");
        var id = (await IssueOk(patho, o.OrderId)).GetProperty("meta").GetProperty("id").GetInt64();
        var reception = await A("RECEPTION");

        Assert.False((await ApiClient.Body(await reception.GetAsync($"/api/reports/{id}"))).GetProperty("meta").GetProperty("isDuplicate").GetBoolean());
        Assert.Equal(1, (await ApiClient.Body(await reception.PostAsync($"/api/reports/{id}/printed"))).GetProperty("printCount").GetInt32());
        Assert.True((await ApiClient.Body(await reception.GetAsync($"/api/reports/{id}"))).GetProperty("meta").GetProperty("isDuplicate").GetBoolean());

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => reception.PostAsync($"/api/reports/{id}/printed")));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(6, (await ApiClient.Body(await reception.GetAsync($"/api/reports/{id}"))).GetProperty("meta").GetProperty("printCount").GetInt32());
    }

    [Fact]
    public async Task Two_people_issuing_at_the_same_moment_produce_one_report()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var a = await A("PATHOLOGIST"); var b = await A("PATHOLOGIST");
        var results = await Task.WhenAll(a.PostAsync($"/api/orders/{o.OrderId}/reports", Issue()), b.PostAsync($"/api/orders/{o.OrderId}/reports", Issue()));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var db = fx.System();
        Assert.Equal(1, await db.LabReports.IgnoreQueryFilters().CountAsync(r => r.OrderId == o.OrderId));
    }

    [Fact]
    public async Task Roles_see_print_and_issue_only_what_their_permissions_allow()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var reception = await A("RECEPTION");        // reports.view + reports.print, no issue
        var tech = await A("TECHNICIAN");            // reports.view only
        var accountant = await A("ACCOUNTANT");      // no report permissions

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync($"/api/orders/{o.OrderId}/reports", Issue())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync($"/api/orders/{o.OrderId}/reports", Issue())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tech.PostAsync($"/api/orders/{o.OrderId}/reports/preview", new { language = 0 })).StatusCode);   // a technician may preview
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("/api/reports")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsync($"/api/orders/{o.OrderId}/reports/preview", new { language = 0 })).StatusCode);

        var id = (await IssueOk(await A("PATHOLOGIST"), o.OrderId)).GetProperty("meta").GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await reception.PostAsync($"/api/reports/{id}/printed")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync($"/api/reports/{id}/printed")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync($"/api/reports/{id}")).StatusCode);
    }

    [Fact]
    public async Task Reports_are_hidden_from_other_organisations_and_from_branches_without_a_link()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var id = (await IssueOk(await A("PATHOLOGIST"), o.OrderId)).GetProperty("meta").GetProperty("id").GetInt64();

        var other = await At(fx.OrgBMainBranch, fx.OrgB, "SYSADMIN");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/reports/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/reports/{id}/printed")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/reports/{id}/verify")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/orders/{o.OrderId}/reports/preview", new { language = 0 })).StatusCode);

        var username = await Api.CreateUserAsync(fx.OrgA, ["PATHOLOGIST"], allBranches: false, branchIds: [fx.OrgASecondBranch]);
        var limited = await Api.ClientAsync();
        ApiClient.Ok(await limited.LoginAsync(username));
        ApiClient.Ok(await limited.PostAsync("/api/auth/active-branch", new { branchId = fx.OrgASecondBranch }));
        await limited.RefreshCsrfAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await limited.GetAsync($"/api/reports/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await limited.PostAsync($"/api/orders/{o.OrderId}/reports", Issue())).StatusCode);
        Assert.Equal(0, (await ApiClient.Body(await limited.GetAsync($"/api/orders/{o.OrderId}/reports"))).GetArrayLength());
    }

    [Fact]
    public async Task Tampering_with_an_issued_report_is_detected_and_the_database_refuses_inconsistent_reports()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb, f.Mix);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var patho = await A("PATHOLOGIST");
        var id = (await IssueOk(patho, o.OrderId)).GetProperty("meta").GetProperty("id").GetInt64();
        await using var db = fx.System();

        // A second live version for the same order.
        var dup = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO lab.LabReports (OrganizationId,OrderId,PatientId,OrderBranchId,LabBranchId,ReportNo,Version,Status,Language,IssuedByUserId,IssuedAtUtc,AmendmentReason,ReleasedCount,ActiveTestCount,SnapshotJson,SnapshotHash,PrintCount,CreatedAtUtc) " +
            "SELECT OrganizationId,OrderId,PatientId,OrderBranchId,LabBranchId,ReportNo,2,0,Language,IssuedByUserId,IssuedAtUtc,'x',ReleasedCount,ActiveTestCount,SnapshotJson,SnapshotHash,0,CreatedAtUtc FROM lab.LabReports WHERE Id = {0}", id));
        Assert.Contains("UX_LabReports_OneLivePerOrder", (dup.InnerException ?? dup).Message);

        // "Final" while a test is still pending.
        var final = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE lab.LabReports SET Status = 1 WHERE Id = {0}", id));
        Assert.Contains("CK_LabReports_Final", (final.InnerException ?? final).Message);

        // A hash that is not 64 characters.
        var badHash = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE lab.LabReports SET SnapshotHash = 'abc' WHERE Id = {0}", id));
        Assert.Contains("CK_LabReports_Hash", (badHash.InnerException ?? badHash).Message);

        // Someone edits the stored report directly: the fingerprint no longer matches.
        await db.Database.ExecuteSqlRawAsync("UPDATE lab.LabReports SET SnapshotJson = REPLACE(SnapshotJson, '\"display\":\"15.0\"', '\"display\":\"51.0\"') WHERE Id = {0}", id);
        var verify = await ApiClient.Body(await patho.GetAsync($"/api/reports/{id}/verify"));
        Assert.False(verify.GetProperty("intact").GetBoolean());
    }

    [Fact]
    public async Task Template_text_and_logo_are_managed_with_their_own_permission_and_feed_new_reports()
    {
        var f = await Setup();
        var mgr = await A("LABMGR");
        var reception = await A("RECEPTION");
        var current = await ApiClient.Body(await mgr.GetAsync("/api/report-template"));
        var rowVersion = current.GetProperty("rowVersion").ValueKind == JsonValueKind.Null ? null : current.GetProperty("rowVersion").GetString();
        var header = "Open 24 hours " + Code("");
        var update = new { rowVersion, en = new { headerNote = header, footerNote = "Thank you", disclaimer = "Results relate to the sample received." }, ur = new { headerNote = "چوبیس گھنٹے", footerNote = "شکریہ", disclaimer = "نتائج موصول نمونے سے متعلق ہیں۔" } };
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PutAsync("/api/report-template", update)).StatusCode);
        ApiClient.Ok(await mgr.PutAsync("/api/report-template", update), HttpStatusCode.NoContent);
        // An existing template needs the version that was read; a version that is no longer current is refused.
        Assert.Equal("row_version_required", await ApiClient.Code(await mgr.PutAsync("/api/report-template", update)));
        var seen = (await ApiClient.Body(await mgr.GetAsync("/api/report-template"))).GetProperty("rowVersion").GetString();
        ApiClient.Ok(await mgr.PutAsync("/api/report-template", new { rowVersion = seen, update.en, update.ur }), HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.Conflict, (await mgr.PutAsync("/api/report-template", new { rowVersion = seen, update.en, update.ur })).StatusCode);

        var o = await ReceivedOrder(f, f.Hb);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var letterhead = (await IssueOk(await A("PATHOLOGIST"), o.OrderId, language: 1)).GetProperty("snapshot").GetProperty("letterhead");
        Assert.Equal(header, letterhead.GetProperty("headerNoteEn").GetString());
        Assert.Equal("چوبیس گھنٹے", letterhead.GetProperty("headerNoteUr").GetString());

        // Logo: only real PNG or JPEG bytes, within the size limit, whatever the file is called.
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(reception, png, "logo.png")).StatusCode);
        Assert.Equal("image_type_invalid", await ApiClient.Code(await Upload(mgr, System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"), "logo.png")));
        Assert.Equal("image_type_invalid", await ApiClient.Code(await Upload(mgr, System.Text.Encoding.ASCII.GetBytes("MZ fake executable"), "logo.png")));
        Assert.Equal("image_too_large", await ApiClient.Code(await Upload(mgr, png.Concat(new byte[310 * 1024]).ToArray(), "logo.png")));
        ApiClient.Ok(await Upload(mgr, png, "logo.png"), HttpStatusCode.NoContent);

        var logo = await reception.GetAsync("/api/report-template/logo");
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        Assert.Equal("image/png", logo.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", logo.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True((await ApiClient.Body(await mgr.GetAsync("/api/report-template"))).GetProperty("hasLogo").GetBoolean());
        ApiClient.Ok(await Delete(mgr, "/api/report-template/logo"), HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NotFound, (await reception.GetAsync("/api/report-template/logo")).StatusCode);
    }

    [Fact]
    public async Task Viewing_printing_issuing_and_verifying_are_recorded_in_the_audit_log()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var patho = await A("PATHOLOGIST");
        var meta = (await IssueOk(patho, o.OrderId)).GetProperty("meta");
        var id = meta.GetProperty("id").GetInt64();
        ApiClient.Ok(await patho.PostAsync($"/api/reports/{id}/printed"));
        ApiClient.Ok(await patho.GetAsync($"/api/reports/{id}/verify"));
        var key = $"{meta.GetProperty("reportNo").GetString()}/v1";
        await using var db = fx.System();
        var actions = await db.AuditLogs.Where(a => a.EntityId == key).Select(a => a.Action).ToListAsync();
        Assert.Contains("Report.Issue", actions);
        Assert.Contains("Report.View", actions);
        Assert.Contains("Report.Print", actions);
        Assert.Contains("Report.Verify", actions);
        // The patient-identifying snapshot is never copied into the audit log.
        Assert.DoesNotContain(await db.AuditLogs.Where(a => a.EntityType == "LabReport" && a.Changes != null).Select(a => a.Changes!).ToListAsync(), c => c.Contains("SnapshotJson") && c.Contains("Report Patient"));
    }

    [Fact]
    public async Task Every_report_endpoint_refuses_anonymous_and_token_less_requests()
    {
        var anon = await Api.ClientAsync();
        foreach (var url in new[] { "/api/reports", "/api/reports/1", "/api/reports/1/verify", "/api/orders/1/reports", "/api/report-template", "/api/report-template/logo" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(url)).StatusCode);
        var user = await A("SYSADMIN");
        foreach (var url in new[] { "/api/reports/1/printed", "/api/orders/1/reports/preview", "/api/orders/1/reports" })
            Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsync(url, new { }, withCsrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PutAsyncNoCsrf("/api/report-template", new { })).StatusCode);
    }

    [Fact]
    public async Task A_signatory_with_a_registration_number_and_signature_appears_on_the_report_and_only_real_images_are_accepted()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var patho = await A("PATHOLOGIST");
        var admin = await A("SYSADMIN");

        // Who approved it?
        var preview = await ApiClient.Body(await patho.PostAsync($"/api/orders/{o.OrderId}/reports/preview", new { language = 0 }));
        var approver = preview.GetProperty("snapshot").GetProperty("signatories").EnumerateArray().First(s => s.GetProperty("approved").GetBoolean());
        var userId = approver.GetProperty("userId").GetInt32();
        Assert.False(approver.GetProperty("hasSignature").GetBoolean());

        // Registration number through the normal user update; the detail reports it back.
        var detail = await ApiClient.Body(await admin.GetAsync($"/api/users/{userId}"));
        Assert.False(detail.GetProperty("hasSignature").GetBoolean());
        var body = new
        {
            displayNameEn = detail.GetProperty("displayNameEn").GetString(), displayNameUr = (string?)null, designation = "Consultant Pathologist", registrationNo = "PMDC-12345",
            email = (string?)null, phone = (string?)null, preferredLanguage = "en", allBranches = true, defaultBranchId = (int?)null,
            roleIds = detail.GetProperty("roleIds").EnumerateArray().Select(r => r.GetInt32()).ToArray(), branchIds = Array.Empty<int>(),
            isActive = true, rowVersion = detail.GetProperty("rowVersion").GetString(),
        };
        ApiClient.Ok(await admin.PutAsync($"/api/users/{userId}", body));

        // Signature: real PNG or JPEG only, within the size limit, whatever the file is called.
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9, 9, 9];
        var url = $"/api/users/{userId}/signature";
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadTo(await A("RECEPTION"), url, png, "sig.png")).StatusCode);
        Assert.Equal("image_type_invalid", await ApiClient.Code(await UploadTo(admin, url, System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'/>"), "sig.png")));
        Assert.Equal("image_type_invalid", await ApiClient.Code(await UploadTo(admin, url, [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 2], "sig.png")));   // GIF
        Assert.Equal("image_too_large", await ApiClient.Code(await UploadTo(admin, url, png.Concat(new byte[210 * 1024]).ToArray(), "sig.png")));
        ApiClient.Ok(await UploadTo(admin, url, png, "sig.png"), HttpStatusCode.NoContent);
        Assert.True((await ApiClient.Body(await admin.GetAsync($"/api/users/{userId}"))).GetProperty("hasSignature").GetBoolean());

        // The image is served to people who can view reports, as an image, never sniffed as anything else.
        var img = await (await A("RECEPTION")).GetAsync($"/api/reports/signatures/{userId}");
        Assert.Equal(HttpStatusCode.OK, img.StatusCode);
        Assert.Equal("image/png", img.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", img.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(HttpStatusCode.Forbidden, (await (await A("ACCOUNTANT")).GetAsync($"/api/reports/signatures/{userId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await (await At(fx.OrgBMainBranch, fx.OrgB, "SYSADMIN")).GetAsync($"/api/reports/signatures/{userId}")).StatusCode);   // another organisation's user

        // Both now appear on the issued report.
        var signer = (await IssueOk(patho, o.OrderId)).GetProperty("snapshot").GetProperty("signatories").EnumerateArray().First(s => s.GetProperty("userId").GetInt32() == userId);
        Assert.True(signer.GetProperty("hasSignature").GetBoolean());
        Assert.Equal("PMDC-12345", signer.GetProperty("registrationNo").GetString());
        Assert.Equal("Consultant Pathologist", signer.GetProperty("designation").GetString());

        ApiClient.Ok(await Delete(admin, url), HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NotFound, (await (await A("RECEPTION")).GetAsync($"/api/reports/signatures/{userId}")).StatusCode);
    }

    private static async Task<HttpResponseMessage> UploadTo(ApiClient c, string url, byte[] bytes, string name)
    {
        var token = (await ApiClient.Body(await c.GetAsync("/api/auth/csrf"))).GetProperty("token").GetString();
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");   // the declared type is never trusted
        form.Add(file, "file", name);
        var req = new HttpRequestMessage(HttpMethod.Put, url) { Content = form };
        req.Headers.Add("X-XSRF-TOKEN", token);
        return await c.Http.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> Upload(ApiClient c, byte[] bytes, string name)
    {
        var token = (await ApiClient.Body(await c.GetAsync("/api/auth/csrf"))).GetProperty("token").GetString();
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");   // the declared type is never trusted
        form.Add(file, "file", name);
        var req = new HttpRequestMessage(HttpMethod.Put, "/api/report-template/logo") { Content = form };
        req.Headers.Add("X-XSRF-TOKEN", token);
        return await c.Http.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> Delete(ApiClient c, string url)
    {
        var token = (await ApiClient.Body(await c.GetAsync("/api/auth/csrf"))).GetProperty("token").GetString();
        var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add("X-XSRF-TOKEN", token);
        return await c.Http.SendAsync(req);
    }
}

internal static class ReportClientExtensions
{
    public static Task<HttpResponseMessage> PutAsyncNoCsrf(this ApiClient c, string url, object body) =>
        c.Http.PutAsync(url, System.Net.Http.Json.JsonContent.Create(body));
}
