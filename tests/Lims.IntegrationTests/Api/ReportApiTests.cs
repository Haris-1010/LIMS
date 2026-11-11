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

    private static object Design(string style = "modern", string accent = "#0f766e", string[]? blocks = null, int marginTop = 12) => new
    {
        style, accentColor = accent, fontSizePt = 10, marginTopMm = marginTop, marginBottomMm = 14, marginSideMm = 12, showLetterhead = true,
        logoPosition = "start", showRangeBars = true, showFlagWords = true, colourFlags = true, tableStyle = "zebra",
        blocks = blocks ?? ["patient", "results", "pending", "signatures", "disclaimer"],
    };

    [Fact]
    public async Task Report_designs_are_managed_with_the_template_permission_and_there_is_one_default()
    {
        var admin = await A("LABMGR");
        var name1 = "Design " + Code("");
        var first = (await ApiClient.Body(await admin.PostAsync("/api/report-layouts", new { name = name1, isDefault = true, design = Design() }))).GetProperty("id").GetInt32();
        var second = (await ApiClient.Body(await admin.PostAsync("/api/report-layouts", new { name = "Design " + Code(""), isDefault = true, design = Design("compact", blocks: ["results", "patient", "signatures", "pending", "disclaimer"]) }))).GetProperty("id").GetInt32();

        // Making a second design the default takes the mark off the first.
        var list = (await ApiClient.Body(await admin.GetAsync("/api/report-layouts"))).EnumerateArray().ToList();
        Assert.True(list.Single(x => x.GetProperty("id").GetInt32() == second).GetProperty("isDefault").GetBoolean());
        Assert.False(list.Single(x => x.GetProperty("id").GetInt32() == first).GetProperty("isDefault").GetBoolean());
        Assert.Equal("results", list.Single(x => x.GetProperty("id").GetInt32() == second).GetProperty("design").GetProperty("blocks")[0].GetString());

        // Names are unique; settings are checked; a block cannot be left out (results, signatories... must always print).
        Assert.Equal("report_layout_name_taken", await ApiClient.Code(await admin.PostAsync("/api/report-layouts", new { name = name1, isDefault = false, design = Design() })));
        Assert.Equal("report_layout_invalid", await ApiClient.Code(await admin.PostAsync("/api/report-layouts", new { name = Code("D"), isDefault = false, design = Design(accent: "red") })));
        Assert.Equal("report_layout_invalid", await ApiClient.Code(await admin.PostAsync("/api/report-layouts", new { name = Code("D"), isDefault = false, design = Design(blocks: ["patient", "results", "signatures", "disclaimer"]) })));
        Assert.Equal("report_layout_invalid", await ApiClient.Code(await admin.PostAsync("/api/report-layouts", new { name = Code("D"), isDefault = false, design = Design(marginTop: 200) })));

        // Update needs the version the editor saw.
        var rv = list.Single(x => x.GetProperty("id").GetInt32() == first).GetProperty("rowVersion").GetString();
        ApiClient.Ok(await admin.PutAsync($"/api/report-layouts/{first}", new { name = name1 + " v2", isDefault = true, design = Design("colourful"), rowVersion = rv }), HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsync($"/api/report-layouts/{first}", new { name = name1 + " v3", isDefault = true, design = Design(), rowVersion = rv })).StatusCode);
        list = (await ApiClient.Body(await admin.GetAsync("/api/report-layouts"))).EnumerateArray().ToList();
        Assert.Equal(1, list.Count(x => x.GetProperty("isDefault").GetBoolean()));
        Assert.Equal(first, list.Single(x => x.GetProperty("isDefault").GetBoolean()).GetProperty("id").GetInt32());

        // Reception can read designs (to print with one) but not change them.
        var reception = await A("RECEPTION");
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync("/api/report-layouts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync("/api/report-layouts", new { name = Code("D"), isDefault = false, design = Design() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Delete(reception, $"/api/report-layouts/{second}")).StatusCode);

        // Another organisation neither sees nor changes them.
        var other = await At(fx.OrgBMainBranch, fx.OrgB, "SYSADMIN");
        Assert.DoesNotContain((await ApiClient.Body(await other.GetAsync("/api/report-layouts"))).EnumerateArray(), x => x.GetProperty("id").GetInt32() == first);
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(other, $"/api/report-layouts/{first}")).StatusCode);

        ApiClient.Ok(await Delete(admin, $"/api/report-layouts/{second}"), HttpStatusCode.NoContent);
        Assert.DoesNotContain((await ApiClient.Body(await admin.GetAsync("/api/report-layouts"))).EnumerateArray(), x => x.GetProperty("id").GetInt32() == second);
        // Remove the default again so other tests see the built-in design.
        ApiClient.Ok(await Delete(admin, $"/api/report-layouts/{first}"), HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_print_records_the_design_and_which_tests_were_printed()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb, f.Mix);
        await Release(o.ItemIds[0], f.HbParam, "15");
        await Release(o.ItemIds[1], f.MixParam, "25");
        var patho = await A("PATHOLOGIST");
        var issued = await IssueOk(patho, o.OrderId);
        var id = issued.GetProperty("meta").GetProperty("id").GetInt64();
        var key = $"{issued.GetProperty("meta").GetProperty("reportNo").GetString()}/v1";

        // The snapshot carries the number and limits for the range bar.
        var hb = issued.GetProperty("snapshot").GetProperty("sections")[0].GetProperty("tests").EnumerateArray()
            .Single(t => t.GetProperty("orderItemId").GetInt64() == o.ItemIds[0]).GetProperty("parameters")[0];
        Assert.Equal(15m, hb.GetProperty("value").GetDecimal());
        Assert.Equal(10m, hb.GetProperty("low").GetDecimal());
        Assert.Equal(20m, hb.GetProperty("high").GetDecimal());
        Assert.Equal(3, issued.GetProperty("snapshot").GetProperty("schemaVersion").GetInt32());

        var admin = await A("LABMGR");
        var layout = (await ApiClient.Body(await admin.PostAsync("/api/report-layouts", new { name = "Print " + Code(""), isDefault = false, design = Design() }))).GetProperty("id").GetInt32();
        var reception = await A("RECEPTION");
        ApiClient.Ok(await reception.PostAsync($"/api/reports/{id}/printed", new { layoutId = layout, orderItemIds = new[] { o.ItemIds[1] } }));
        ApiClient.Ok(await reception.PostAsync($"/api/reports/{id}/printed", new { orderItemIds = o.ItemIds, language = 1 }));   // all chosen = full print, an Urdu copy
        Assert.Equal("report_language_invalid", await ApiClient.Code(await reception.PostAsync($"/api/reports/{id}/printed", new { language = 7 })));

        // Tests that are not on the report, and designs of another organisation, are refused (and not counted).
        Assert.Equal("report_tests_invalid", await ApiClient.Code(await reception.PostAsync($"/api/reports/{id}/printed", new { orderItemIds = new[] { o.ItemIds[0] + 999_999 } })));
        var other = await At(fx.OrgBMainBranch, fx.OrgB, "SYSADMIN");
        var foreignLayout = (await ApiClient.Body(await other.PostAsync("/api/report-layouts", new { name = "Foreign " + Code(""), isDefault = false, design = Design() }))).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await reception.PostAsync($"/api/reports/{id}/printed", new { layoutId = foreignLayout })).StatusCode);
        Assert.Equal(2, (await ApiClient.Body(await reception.GetAsync($"/api/reports/{id}"))).GetProperty("meta").GetProperty("printCount").GetInt32());

        await using var db = fx.System();
        var prints = await db.AuditLogs.Where(a => a.EntityId == key && a.Action == "Report.Print").OrderBy(a => a.Id).Select(a => a.Changes!).ToListAsync();
        Assert.Equal(2, prints.Count);
        using (var partial = JsonDocument.Parse(prints[0]))
        {
            var text = partial.RootElement.GetRawText();
            Assert.Contains("\"partial\":true", text.Replace(" ", ""));
            Assert.Contains(o.ItemIds[1].ToString(), text);
            Assert.Contains(layout.ToString(), text);
            Assert.Contains("\"language\":\"English\"", text.Replace(" ", ""));   // no language chosen = as issued
        }
        Assert.Contains("\"partial\":false", prints[1].Replace(" ", ""));
        // The report was issued in English; this copy was printed in Urdu, and the audit line says both.
        Assert.Contains("\"language\":\"Urdu\"", prints[1].Replace(" ", ""));
        Assert.Contains("\"issuedIn\":\"English\"", prints[1].Replace(" ", ""));
        await Delete(admin, $"/api/report-layouts/{layout}");
    }

    [Fact]
    public async Task Sharing_on_whatsapp_checks_the_number_needs_the_print_permission_and_keeps_the_number_out_of_the_audit()
    {
        var f = await Setup();
        var o = await ReceivedOrder(f, f.Hb, f.Mix);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var patho = await A("PATHOLOGIST");
        var issued = await IssueOk(patho, o.OrderId);
        var id = issued.GetProperty("meta").GetProperty("id").GetInt64();
        var reception = await A("RECEPTION");

        var ok = await ApiClient.Body(await reception.PostAsync($"/api/reports/{id}/share", new { channel = "whatsapp", phone = "0300-1234567" }));
        Assert.Equal("923001234567", ok.GetProperty("phone").GetString());
        Assert.Equal("phone_invalid", await ApiClient.Code(await reception.PostAsync($"/api/reports/{id}/share", new { channel = "whatsapp", phone = "12ab" })));
        Assert.Equal("share_channel_invalid", await ApiClient.Code(await reception.PostAsync($"/api/reports/{id}/share", new { channel = "email", phone = "03001234567" })));
        Assert.Equal(HttpStatusCode.Forbidden, (await (await A("TECHNICIAN")).PostAsync($"/api/reports/{id}/share", new { channel = "whatsapp", phone = "03001234567" })).StatusCode);
        var other = await At(fx.OrgBMainBranch, fx.OrgB, "SYSADMIN");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/reports/{id}/share", new { channel = "whatsapp", phone = "03001234567" })).StatusCode);

        // A superseded version is never sent to the patient.
        await Release(o.ItemIds[1], f.MixParam, "12");
        await IssueOk(patho, o.OrderId, reason: "Second test released");
        Assert.Equal("report_superseded_share", await ApiClient.Code(await reception.PostAsync($"/api/reports/{id}/share", new { channel = "whatsapp", phone = "03001234567" })));

        await using var db = fx.System();
        var key = $"{issued.GetProperty("meta").GetProperty("reportNo").GetString()}/v1";
        var shares = await db.AuditLogs.Where(a => a.EntityId == key && a.Action == "Report.Share").Select(a => a.Changes ?? "").ToListAsync();
        Assert.Single(shares);
        Assert.DoesNotContain("1234567", shares[0]);
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
    public async Task Header_and_footer_images_are_checked_stored_separately_and_chosen_by_the_design()
    {
        // The second laboratory, so the first one's template stays untouched for the template-text test.
        var mgr = await At(fx.OrgBMainBranch, fx.OrgB, "LABMGR");
        var reception = await At(fx.OrgBMainBranch, fx.OrgB, "RECEPTION");
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9, 9];
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

        foreach (var kind in new[] { "header", "footer" })
        {
            var url = $"/api/report-template/images/{kind}";
            Assert.Equal(HttpStatusCode.Forbidden, (await UploadTo(reception, url, png, kind + ".png")).StatusCode);
            Assert.Equal("image_type_invalid", await ApiClient.Code(await UploadTo(mgr, url, System.Text.Encoding.UTF8.GetBytes("<svg/>"), kind + ".png")));
            // Page-wide images may be larger than a logo (600 KB), but no larger.
            ApiClient.Ok(await UploadTo(mgr, url, png.Concat(new byte[500 * 1024]).ToArray(), kind + ".png"), HttpStatusCode.NoContent);
            Assert.Equal("image_too_large", await ApiClient.Code(await UploadTo(mgr, url, png.Concat(new byte[610 * 1024]).ToArray(), kind + ".png")));
        }
        ApiClient.Ok(await UploadTo(mgr, "/api/report-template/images/footer", jpeg, "footer.jpg"), HttpStatusCode.NoContent);
        // Only "header" and "footer" exist; any other name is not an image route.
        Assert.NotEqual("image/png", (await reception.GetAsync("/api/report-template/images/side")).Content.Headers.ContentType?.MediaType);

        var header = await reception.GetAsync("/api/report-template/images/header");
        Assert.Equal("image/png", header.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", header.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("image/jpeg", (await reception.GetAsync("/api/report-template/images/footer")).Content.Headers.ContentType?.MediaType);
        var t = await ApiClient.Body(await mgr.GetAsync("/api/report-template"));
        Assert.True(t.GetProperty("hasHeaderImage").GetBoolean());
        Assert.True(t.GetProperty("hasFooterImage").GetBoolean());

        // Removing one leaves the other (and the logo) alone.
        ApiClient.Ok(await Delete(mgr, "/api/report-template/images/header"), HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NotFound, (await reception.GetAsync("/api/report-template/images/header")).StatusCode);
        t = await ApiClient.Body(await mgr.GetAsync("/api/report-template"));
        Assert.False(t.GetProperty("hasHeaderImage").GetBoolean());
        Assert.True(t.GetProperty("hasFooterImage").GetBoolean());

        // A design remembers whether it prints the header image and the footer image.
        var name = "Banner " + Code("");
        var created = await mgr.PostAsync("/api/report-layouts", new { name, isDefault = false, design = new { style = "modern", accentColor = "#0f766e", fontSizePt = 9.5, marginTopMm = 12, marginBottomMm = 14, marginSideMm = 12, showLetterhead = true, logoPosition = "start", useHeaderImage = true, showFooterImage = true, showRangeBars = true, showFlagWords = true, colourFlags = false, tableStyle = "lines", blocks = new[] { "patient", "results", "pending", "signatures", "disclaimer" } } });
        ApiClient.Ok(created);
        var saved = (await ApiClient.Body(await mgr.GetAsync("/api/report-layouts"))).EnumerateArray().Single(l => l.GetProperty("name").GetString() == name).GetProperty("design");
        Assert.True(saved.GetProperty("useHeaderImage").GetBoolean());
        Assert.True(saved.GetProperty("showFooterImage").GetBoolean());
    }

    [Fact]
    public async Task Template_signatories_print_on_new_reports_and_issued_reports_keep_them()
    {
        var f = await Setup();
        var mgr = await A("LABMGR");
        var reception = await A("RECEPTION");
        var patho = await A("PATHOLOGIST");
        var name = "Dr. Consultant " + Code("");

        // Managed with the template permission; anyone who can see reports can list them.
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync("/api/report-template/signatories", new { nameEn = name })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mgr.PostAsync("/api/report-template/signatories", new { nameEn = "  " })).StatusCode);   // a name is required
        var id = (await ApiClient.Body(await mgr.PostAsync("/api/report-template/signatories", new { nameEn = name, nameUr = "ڈاکٹر", qualification = "MBBS, FCPS", designation = "Consultant Pathologist", registrationNo = "PMDC 1-P" }))).GetProperty("id").GetInt32();
        var listed = (await ApiClient.Body(await reception.GetAsync("/api/report-template/signatories"))).EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == id);
        Assert.Equal("MBBS, FCPS", listed.GetProperty("qualification").GetString());
        Assert.False(listed.GetProperty("hasSignature").GetBoolean());

        // Signature image: real PNG/JPEG only, within 200 KB.
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 5, 5];
        var sigUrl = $"/api/report-template/signatories/{id}/signature";
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadTo(reception, sigUrl, png, "sig.png")).StatusCode);
        Assert.Equal("image_type_invalid", await ApiClient.Code(await UploadTo(mgr, sigUrl, System.Text.Encoding.UTF8.GetBytes("<svg/>"), "sig.png")));
        Assert.Equal("image_too_large", await ApiClient.Code(await UploadTo(mgr, sigUrl, png.Concat(new byte[210 * 1024]).ToArray(), "sig.png")));
        ApiClient.Ok(await UploadTo(mgr, sigUrl, png, "sig.png"), HttpStatusCode.NoContent);
        Assert.Equal("image/png", (await reception.GetAsync(sigUrl)).Content.Headers.ContentType?.MediaType);

        // Editing needs the version that was read.
        var rv = (await ApiClient.Body(await mgr.GetAsync("/api/report-template/signatories"))).EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == id).GetProperty("rowVersion").GetString();
        ApiClient.Ok(await mgr.PutAsync($"/api/report-template/signatories/{id}", new { nameEn = name, designation = "Head of Pathology", rowVersion = rv }), HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.Conflict, (await mgr.PutAsync($"/api/report-template/signatories/{id}", new { nameEn = name, rowVersion = rv })).StatusCode);

        // Issued: copied into the snapshot, separately from who approved.
        var o = await ReceivedOrder(f, f.Hb);
        await Release(o.ItemIds[0], f.HbParam, "15");
        var issued = await IssueOk(patho, o.OrderId);
        var snap = issued.GetProperty("snapshot");
        var printed = snap.GetProperty("fixedSignatories").EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == id);
        Assert.Equal("Head of Pathology", printed.GetProperty("designation").GetString());
        Assert.True(printed.GetProperty("hasSignature").GetBoolean());
        Assert.DoesNotContain(snap.GetProperty("signatories").EnumerateArray(), x => x.GetProperty("nameEn").GetString() == name);

        // Removing: gone from the list and from new reports; the issued report is unchanged and its signature still loads.
        ApiClient.Ok(await Delete(mgr, $"/api/report-template/signatories/{id}"), HttpStatusCode.NoContent);
        Assert.DoesNotContain((await ApiClient.Body(await mgr.GetAsync("/api/report-template/signatories"))).EnumerateArray(), x => x.GetProperty("id").GetInt32() == id);
        var reportId = issued.GetProperty("meta").GetProperty("id").GetInt64();
        var again = (await ApiClient.Body(await reception.GetAsync($"/api/reports/{reportId}"))).GetProperty("snapshot").GetProperty("fixedSignatories");
        Assert.Contains(again.EnumerateArray(), x => x.GetProperty("id").GetInt32() == id);
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync(sigUrl)).StatusCode);
        Assert.True((await ApiClient.Body(await patho.GetAsync($"/api/reports/{reportId}/verify"))).GetProperty("intact").GetBoolean());

        // At most four at a time.
        var created = new List<int>();
        HttpResponseMessage res;
        while ((res = await mgr.PostAsync("/api/report-template/signatories", new { nameEn = "Extra " + Code("") })).IsSuccessStatusCode)
            created.Add((await ApiClient.Body(res)).GetProperty("id").GetInt32());
        Assert.Equal("report_signatories_full", await ApiClient.Code(res));
        Assert.Equal(4, (await ApiClient.Body(await mgr.GetAsync("/api/report-template/signatories"))).GetArrayLength());
        foreach (var x in created) await Delete(mgr, $"/api/report-template/signatories/{x}");
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
