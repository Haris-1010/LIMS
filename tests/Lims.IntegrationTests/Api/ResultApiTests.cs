using System.Net;
using System.Text.Json;
using Lims.Domain.Results;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

/// <summary>Result entry, verification, approval, amendment, critical alerts and worklists through the real API.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ResultApiTests(DatabaseFixture fx)
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

    /// <summary>Tests: HB numeric with an approved range (10–20, critical 5/30); DRAFT numeric whose range is still a draft; OPT option; MIX numeric + text.</summary>
    private sealed record Fix(int Hb, int HbParam, int Draft, int DraftParam, int Opt, int OptParam, int OptPositive, int OptNegative, int Mix, int MixNum, int MixText, int Dept, int OtherDept, int PatientId);

    private async Task<Fix> Setup()
    {
        var admin = await A("SYSADMIN");
        var cat = (await ApiClient.Body(await admin.PostAsync("/api/catalog/categories", new { code = Code("C"), nameEn = "Cat" }))).GetProperty("id").GetInt32();
        var depts = await ApiClient.Body(await admin.GetAsync("/api/organization/departments"));
        var dept = depts[0].GetProperty("id").GetInt32();
        var other = depts[1].GetProperty("id").GetInt32();
        async Task<int> Test(int d = 0) => (await ApiClient.Body(await admin.PostAsync("/api/catalog/tests",
            new { code = Code("T"), nameEn = "Fixture", categoryId = cat, departmentId = d == 0 ? dept : d, sampleTypeId = 1, turnaroundHours = 24, price = 100 }))).GetProperty("id").GetInt32();
        async Task<int> Num(int test, bool approve)
        {
            var pid = (await ApiClient.Body(await admin.PostAsync($"/api/catalog/tests/{test}/parameters", new { code = Code("P"), nameEn = "Analyte", resultType = 1, unitId = 10, decimalPlaces = 1 }))).GetProperty("id").GetInt32();
            var range = (await ApiClient.Body(await admin.PostAsync($"/api/catalog/parameters/{pid}/ranges", new { sex = 0, ageFromDays = 0, low = 10, high = 20, criticalLow = 5, criticalHigh = 30, source = "Fixture" }))).GetProperty("id").GetInt32();
            if (approve) ApiClient.Ok(await admin.PostAsync($"/api/catalog/ranges/{range}/approve"), HttpStatusCode.NoContent);
            return pid;
        }
        var hb = await Test(); var hbP = await Num(hb, true);
        var draft = await Test(); var draftP = await Num(draft, false);
        var opt = await Test();
        var optP = (await ApiClient.Body(await admin.PostAsync($"/api/catalog/tests/{opt}/parameters", new
        {
            code = Code("Q"), nameEn = "Screen", resultType = 2,
            options = new[] { new { valueEn = "Negative", valueUr = "منفی", isAbnormal = false }, new { valueEn = "Positive", valueUr = "مثبت", isAbnormal = true } },
        }))).GetProperty("id").GetInt32();
        await using var sys = fx.System();
        var options = await sys.Set<Lims.Domain.Catalog.ParameterOption>().IgnoreQueryFilters().Where(o => o.ParameterId == optP).ToListAsync();
        int OptId(string v) => options.Single(o => o.ValueEn == v).Id;
        var mix = await Test(); var mixNum = await Num(mix, true);
        var mixText = (await ApiClient.Body(await admin.PostAsync($"/api/catalog/tests/{mix}/parameters", new { code = Code("N"), nameEn = "Comment", resultType = 3 }))).GetProperty("id").GetInt32();
        var patient = (await ApiClient.Body(await admin.PostAsync("/api/patients", new { fullName = "Result Patient " + Code(""), sex = 1, ageYears = 40, confirmNotDuplicate = true }))).GetProperty("id").GetInt32();
        return new Fix(hb, hbP, draft, draftP, opt, optP, OptId("Positive"), OptId("Negative"), mix, mixNum, mixText, dept, other, patient);
    }

    /// <summary>Registers an order for one test, collects and receives its sample at the main branch; returns the ordered test id.</summary>
    private async Task<(long ItemId, long SampleId, long OrderId)> ReceivedItem(Fix f, int testId, bool urgent = false)
    {
        var reception = await A("RECEPTION");
        var order = await ApiClient.Body(await reception.PostAsync("/api/orders", new { idempotencyKey = Guid.NewGuid(), patientId = f.PatientId, priority = urgent ? 1 : 0, items = new object[] { new { testId } } }));
        var orderId = order.GetProperty("id").GetInt64();
        var itemId = order.GetProperty("items")[0].GetProperty("id").GetInt64();
        var sid = (await ApiClient.Body(await reception.GetAsync($"/api/orders/{orderId}/samples")))[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await (await A("PHLEBOTOMY")).PostAsync($"/api/samples/{sid}/collect", Confirmed));
        ApiClient.Ok(await (await A("TECHNICIAN")).PostAsync($"/api/samples/{sid}/receive"));
        return (itemId, sid, orderId);
    }

    private static object Draft(string? rowVersion, params (int Param, string? Value, int? Option)[] values) => new
    {
        rowVersion, comment = (string?)null,
        values = values.Select(v => new { parameterId = v.Param, value = v.Value, optionId = v.Option }).ToArray(),
    };

    private static async Task<JsonElement> Entry(ApiClient c, long item)
    {
        var res = await c.GetAsync($"/api/results/items/{item}");
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    private static string Rv(JsonElement entry) => entry.GetProperty("current").GetProperty("rowVersion").GetString()!;
    private static long Rid(JsonElement entry) => entry.GetProperty("current").GetProperty("id").GetInt64();
    private static int FlagOf(JsonElement entry, int param) => entry.GetProperty("values").EnumerateArray().Single(v => v.GetProperty("parameterId").GetInt32() == param).GetProperty("flag").GetInt32();

    /// <summary>Saves a draft with one numeric value and submits it.</summary>
    private async Task<JsonElement> EnterAndSubmit(ApiClient tech, long item, int param, string value)
    {
        var saved = await tech.PutAsync($"/api/results/items/{item}/draft", Draft(null, (param, value, null)));
        ApiClient.Ok(saved);
        var e = await ApiClient.Body(saved);
        var res = await tech.PostAsync($"/api/results/{Rid(e)}/submit", new { rowVersion = Rv(e) });
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    private static async Task<JsonElement> Do(ApiClient c, string path, object body)
    {
        var res = await c.PostAsync(path, body);
        ApiClient.Ok(res);
        return await ApiClient.Body(res);
    }

    [Fact]
    public async Task Full_path_enter_submit_verify_approve_updates_the_test_status_and_history()
    {
        var f = await Setup();
        var (item, _, orderId) = await ReceivedItem(f, f.Hb);
        var tech = await A("TECHNICIAN");
        var patho = await A("PATHOLOGIST");

        var saved = await ApiClient.Body(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(null, (f.HbParam, "15", null))));
        Assert.Equal(0, saved.GetProperty("current").GetProperty("status").GetInt32());
        Assert.Equal(1, FlagOf(saved, f.HbParam));                      // normal, from the approved range
        Assert.Equal(30, saved.GetProperty("itemStatus").GetInt32());   // in progress

        var submitted = await Do(tech, $"/api/results/{Rid(saved)}/submit", new { rowVersion = Rv(saved) });
        Assert.Equal(10, submitted.GetProperty("current").GetProperty("status").GetInt32());
        // Entry is locked once submitted.
        Assert.Equal("result_invalid_state", await ApiClient.Code(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(Rv(submitted), (f.HbParam, "16", null)))));

        var verified = await Do(patho, $"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted), comment = "Looks right" });
        Assert.Equal(20, verified.GetProperty("current").GetProperty("status").GetInt32());
        var approved = await Do(patho, $"/api/results/{Rid(verified)}/approve", new { rowVersion = Rv(verified) });
        Assert.Equal(40, approved.GetProperty("itemStatus").GetInt32());   // completed
        Assert.Equal(new[] { "Submit", "Verify", "Approve" }, approved.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("step").GetString()!).ToArray());

        var card = await ApiClient.Body(await patho.GetAsync($"/api/orders/{orderId}/results"));
        Assert.Equal(30, card[0].GetProperty("resultStatus").GetInt32());
        Assert.True(card[0].GetProperty("hasReleased").GetBoolean());

        // The audit trail recorded the result and its values.
        await using var db = fx.System();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityType == "TestResult" && a.EntityId == Rid(saved).ToString()));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityType == "TestResultValue"));
    }

    [Fact]
    public async Task Patient_history_returns_only_approved_numeric_values()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Hb);
        var tech = await A("TECHNICIAN");
        var patho = await A("PATHOLOGIST");
        var saved = await ApiClient.Body(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(null, (f.HbParam, "15", null))));
        var submitted = await Do(tech, $"/api/results/{Rid(saved)}/submit", new { rowVersion = Rv(saved) });
        var verified = await Do(patho, $"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted) });
        await Do(patho, $"/api/results/{Rid(verified)}/approve", new { rowVersion = Rv(verified) });

        var response = await patho.GetAsync($"/api/patients/{f.PatientId}/history?months=3");
        var history = await ApiClient.Body(response);
        ApiClient.Ok(response);
        var point = Assert.Single(history.GetProperty("points").EnumerateArray());
        Assert.Equal(f.HbParam, point.GetProperty("parameterId").GetInt32());
        Assert.Equal(15m, point.GetProperty("value").GetDecimal());
        Assert.Equal(1, point.GetProperty("flag").GetInt32());
    }

    [Fact]
    public async Task Flags_come_only_from_approved_ranges()
    {
        var f = await Setup();
        var tech = await A("TECHNICIAN");
        async Task<int> Flag(int test, int param, string value)
        {
            var (item, _, _) = await ReceivedItem(f, test);
            return FlagOf(await ApiClient.Body(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(null, (param, value, null)))), param);
        }
        Assert.Equal(2, await Flag(f.Hb, f.HbParam, "9.9"));      // low
        Assert.Equal(3, await Flag(f.Hb, f.HbParam, "20.5"));     // high
        Assert.Equal(4, await Flag(f.Hb, f.HbParam, "4"));        // critical low
        Assert.Equal(5, await Flag(f.Hb, f.HbParam, "31"));       // critical high
        Assert.Equal(0, await Flag(f.Draft, f.DraftParam, "999")); // only a draft range exists: never flagged
    }

    [Fact]
    public async Task Option_values_are_checked_against_the_parameter_and_flag_abnormal_options()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Opt);
        var tech = await A("TECHNICIAN");
        Assert.Equal("result_option_invalid", await ApiClient.Code(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(null, (f.OptParam, null, 999999)))));
        var e = await ApiClient.Body(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(null, (f.OptParam, null, f.OptPositive))));
        Assert.Equal(6, FlagOf(e, f.OptParam));
        Assert.Equal("Positive", e.GetProperty("values")[0].GetProperty("valueText").GetString());
        e = await ApiClient.Body(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(Rv(e), (f.OptParam, null, f.OptNegative))));
        Assert.Equal(1, FlagOf(e, f.OptParam));
    }

    [Fact]
    public async Task Invalid_values_are_refused_with_clear_codes_and_incomplete_results_cannot_be_submitted()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Mix);
        var tech = await A("TECHNICIAN");
        string Url() => $"/api/results/items/{item}/draft";
        Assert.Equal("result_value_invalid", await ApiClient.Code(await tech.PutAsync(Url(), Draft(null, (f.MixNum, "abc", null)))));
        Assert.Equal("result_value_invalid", await ApiClient.Code(await tech.PutAsync(Url(), Draft(null, (f.MixNum, "1,5", null)))));
        Assert.Equal("result_value_decimals", await ApiClient.Code(await tech.PutAsync(Url(), Draft(null, (f.MixNum, "1.1234567", null)))));
        Assert.Equal("result_value_range", await ApiClient.Code(await tech.PutAsync(Url(), Draft(null, (f.MixNum, "1000000000000", null)))));
        Assert.Equal("result_text_too_long", await ApiClient.Code(await tech.PutAsync(Url(), Draft(null, (f.MixText, new string('x', 501), null)))));
        Assert.Equal("result_parameter_invalid", await ApiClient.Code(await tech.PutAsync(Url(), Draft(null, (f.HbParam, "5", null)))));
        Assert.Equal("result_duplicate_parameter", await ApiClient.Code(await tech.PutAsync(Url(), Draft(null, (f.MixNum, "1", null), (f.MixNum, "2", null)))));

        // Urdu digits are understood; the stored value is a plain number.
        var e = await ApiClient.Body(await tech.PutAsync(Url(), Draft(null, (f.MixNum, "۱۲٫۵", null))));
        Assert.Equal(12.5m, e.GetProperty("values")[0].GetProperty("valueNumeric").GetDecimal());

        // One of two parameters is missing.
        var res = await tech.PostAsync($"/api/results/{Rid(e)}/submit", new { rowVersion = Rv(e) });
        Assert.Equal("result_incomplete", await ApiClient.Code(res));
        e = await ApiClient.Body(await tech.PutAsync(Url(), Draft(Rv(e), (f.MixText, "Note", null))));
        ApiClient.Ok(await tech.PostAsync($"/api/results/{Rid(e)}/submit", new { rowVersion = Rv(e) }));
    }

    [Fact]
    public async Task Critical_values_raise_an_alert_that_blocks_approval_until_reported_with_read_back()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Hb);
        var tech = await A("TECHNICIAN");
        var patho = await A("PATHOLOGIST");
        var submitted = await EnterAndSubmit(tech, item, f.HbParam, "4");
        Assert.Equal(4, FlagOf(submitted, f.HbParam));
        var alert = submitted.GetProperty("alerts")[0];
        Assert.Equal(0, alert.GetProperty("status").GetInt32());
        Assert.False(alert.GetProperty("isHigh").GetBoolean());
        Assert.Equal(5m, alert.GetProperty("limit").GetDecimal());

        var verified = await Do(patho, $"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted) });
        Assert.Equal("critical_alert_open", await ApiClient.Code(await patho.PostAsync($"/api/results/{Rid(verified)}/approve", new { rowVersion = Rv(verified) })));

        var id = alert.GetProperty("id").GetInt64();
        Assert.Equal("read_back_required", await ApiClient.Code(await tech.PostAsync($"/api/critical-alerts/{id}/notify", new { notifiedTo = "Dr Khan", method = 1, readBackConfirmed = false })));
        Assert.Equal(HttpStatusCode.BadRequest, (await tech.PostAsync($"/api/critical-alerts/{id}/notify", new { notifiedTo = " ", method = 1, readBackConfirmed = true })).StatusCode);
        Assert.Equal("notify_method_invalid", await ApiClient.Code(await tech.PostAsync($"/api/critical-alerts/{id}/notify", new { notifiedTo = "Dr Khan", method = 9, readBackConfirmed = true })));
        var done = await ApiClient.Body(await tech.PostAsync($"/api/critical-alerts/{id}/notify", new { notifiedTo = "Dr Khan", method = 1, readBackConfirmed = true, comment = "Called" }));
        Assert.Equal(1, done.GetProperty("status").GetInt32());
        Assert.True(done.GetProperty("readBackConfirmed").GetBoolean());
        Assert.Equal("alert_already_notified", await ApiClient.Code(await tech.PostAsync($"/api/critical-alerts/{id}/notify", new { notifiedTo = "Dr Khan", method = 1, readBackConfirmed = true })));

        var approved = await Do(patho, $"/api/results/{Rid(verified)}/approve", new { rowVersion = Rv(verified) });
        Assert.Equal(40, approved.GetProperty("itemStatus").GetInt32());

        // Reception does not see alerts at all.
        Assert.Equal(HttpStatusCode.Forbidden, (await (await A("RECEPTION")).GetAsync("/api/critical-alerts")).StatusCode);
    }

    [Fact]
    public async Task The_person_who_entered_a_result_cannot_verify_or_approve_it()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Hb);
        var patho = await A("PATHOLOGIST");               // holds enter, verify and approve
        var submitted = await EnterAndSubmit(patho, item, f.HbParam, "15");
        Assert.True(submitted.GetProperty("actions").GetProperty("isOwnEntry").GetBoolean());
        Assert.False(submitted.GetProperty("actions").GetProperty("verify").GetBoolean());
        Assert.Equal("result_self_review", await ApiClient.Code(await patho.PostAsync($"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted) })));

        // Someone else verifies; the enterer still cannot approve it.
        var verified = await Do(await A("TECHNICIAN"), $"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted) });
        Assert.Equal("result_self_review", await ApiClient.Code(await patho.PostAsync($"/api/results/{Rid(verified)}/approve", new { rowVersion = Rv(verified) })));
        // A pathologist who did not enter it may approve a result a technician verified, and may even verify and approve themselves.
        ApiClient.Ok(await (await A("PATHOLOGIST")).PostAsync($"/api/results/{Rid(verified)}/approve", new { rowVersion = Rv(verified) }));
    }

    [Fact]
    public async Task Returning_a_result_requires_a_reason_and_reopens_it_for_entry()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Hb);
        var tech = await A("TECHNICIAN");
        var submitted = await EnterAndSubmit(tech, item, f.HbParam, "15");
        var verifier = await A("TECHNICIAN");
        Assert.Equal(HttpStatusCode.BadRequest, (await verifier.PostAsync($"/api/results/{Rid(submitted)}/return", new { reason = "", rowVersion = Rv(submitted) })).StatusCode);
        var back = await Do(verifier, $"/api/results/{Rid(submitted)}/return", new { reason = "Value looks transposed", rowVersion = Rv(submitted) });
        Assert.Equal(0, back.GetProperty("current").GetProperty("status").GetInt32());
        Assert.Equal("Return", back.GetProperty("history").EnumerateArray().Last().GetProperty("step").GetString());

        // Anyone with entry rights can correct it and submit again; it is then a new hand-over.
        var other = await A("TECHNICIAN");
        var fixedUp = await ApiClient.Body(await other.PutAsync($"/api/results/items/{item}/draft", Draft(Rv(back), (f.HbParam, "51", null))));
        Assert.Equal(5, FlagOf(fixedUp, f.HbParam));
        ApiClient.Ok(await other.PostAsync($"/api/results/{Rid(fixedUp)}/submit", new { rowVersion = Rv(fixedUp) }));
        // A returned result that was submitted once cannot be discarded: its history stays.
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsyncCompat($"/api/results/items/{item}/draft")).StatusCode);
    }

    [Fact]
    public async Task A_stale_version_cannot_be_verified_or_approved_and_parallel_reviews_have_one_winner()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Hb);
        var tech = await A("TECHNICIAN");
        var submitted = await EnterAndSubmit(tech, item, f.HbParam, "15");
        var staleRv = Rv(submitted);

        // The reviewer looked at version A; meanwhile it was returned, changed and submitted again.
        var reviewer = await A("TECHNICIAN");
        var back = await Do(reviewer, $"/api/results/{Rid(submitted)}/return", new { reason = "Recheck", rowVersion = staleRv });
        var edited = await ApiClient.Body(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(Rv(back), (f.HbParam, "17", null))));
        var again = await Do(tech, $"/api/results/{Rid(edited)}/submit", new { rowVersion = Rv(edited) });
        var res = await reviewer.PostAsync($"/api/results/{Rid(again)}/verify", new { rowVersion = staleRv });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        // Two reviewers click at the same moment on the current version.
        var a = await A("TECHNICIAN"); var b = await A("TECHNICIAN");
        var results = await Task.WhenAll(
            a.PostAsync($"/api/results/{Rid(again)}/verify", new { rowVersion = Rv(again) }),
            b.PostAsync($"/api/results/{Rid(again)}/verify", new { rowVersion = Rv(again) }));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task A_released_result_is_locked_and_can_only_change_through_a_reasoned_amendment()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Hb);
        var tech = await A("TECHNICIAN");
        var patho = await A("PATHOLOGIST");
        var submitted = await EnterAndSubmit(tech, item, f.HbParam, "15");
        var verified = await Do(patho, $"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted) });
        var approved = await Do(patho, $"/api/results/{Rid(verified)}/approve", new { rowVersion = Rv(verified) });
        var releasedId = Rid(approved);

        // Nothing can edit it.
        Assert.Equal("result_already_released", await ApiClient.Code(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(null, (f.HbParam, "99", null)))));
        Assert.Equal("result_invalid_state", await ApiClient.Code(await patho.PostAsync($"/api/results/{releasedId}/submit", new { rowVersion = Rv(approved) })));
        // Amendment needs its own permission and a reason.
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync($"/api/results/{releasedId}/amend", new { reason = "x", rowVersion = Rv(approved) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await patho.PostAsync($"/api/results/{releasedId}/amend", new { reason = "", rowVersion = Rv(approved) })).StatusCode);

        var amending = await Do(patho, $"/api/results/{releasedId}/amend", new { reason = "Analyser rerun gave 18.2", rowVersion = Rv(approved) });
        Assert.Equal(2, amending.GetProperty("current").GetProperty("version").GetInt32());
        Assert.Equal(40, amending.GetProperty("itemStatus").GetInt32());                       // the released version stays valid
        Assert.Equal(30, amending.GetProperty("released").GetProperty("status").GetInt32());
        Assert.Equal("amendment_in_progress", await ApiClient.Code(await patho.PostAsync($"/api/results/{releasedId}/amend", new { reason = "again", rowVersion = Rv(approved) })));

        var edited = await ApiClient.Body(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(Rv(amending), (f.HbParam, "18.2", null))));
        var s2 = await Do(tech, $"/api/results/{Rid(edited)}/submit", new { rowVersion = Rv(edited) });
        var v2 = await Do(patho, $"/api/results/{Rid(s2)}/verify", new { rowVersion = Rv(s2) });
        var a2 = await Do(patho, $"/api/results/{Rid(v2)}/approve", new { rowVersion = Rv(v2) });
        Assert.Equal(40, a2.GetProperty("itemStatus").GetInt32());

        await using var db = fx.System();
        var versions = await db.TestResults.IgnoreQueryFilters().Where(r => r.OrderItemId == item).OrderBy(r => r.Version).Select(r => new { r.Version, r.Status, r.AmendmentReason }).ToListAsync();
        Assert.Equal([ResultStatus.Superseded, ResultStatus.Approved], versions.Select(v => v.Status));
        Assert.Equal("Analyser rerun gave 18.2", versions[1].AmendmentReason);
        var kept = await db.TestResultValues.IgnoreQueryFilters().Where(v => v.ResultId == releasedId).Select(v => v.ValueNumeric).SingleAsync();
        Assert.Equal(15m, kept);                                                                // the old value is never overwritten
    }

    [Fact]
    public async Task An_amendment_that_was_only_started_can_be_abandoned_and_the_release_stays_intact()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Hb);
        var tech = await A("TECHNICIAN");
        var patho = await A("PATHOLOGIST");
        var submitted = await EnterAndSubmit(tech, item, f.HbParam, "15");
        var verified = await Do(patho, $"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted) });
        var approved = await Do(patho, $"/api/results/{Rid(verified)}/approve", new { rowVersion = Rv(verified) });
        var amending = await Do(patho, $"/api/results/{Rid(approved)}/amend", new { reason = "Started by mistake", rowVersion = Rv(approved) });
        Assert.True(amending.GetProperty("actions").GetProperty("discard").GetBoolean());

        ApiClient.Ok(await patho.DeleteAsyncCompat($"/api/results/items/{item}/draft"), HttpStatusCode.NoContent);
        var after = await Entry(patho, item);
        Assert.Equal(30, after.GetProperty("current").GetProperty("status").GetInt32());     // the release is untouched
        Assert.Equal(40, after.GetProperty("itemStatus").GetInt32());
        Assert.True(after.GetProperty("actions").GetProperty("amend").GetBoolean());          // and can be amended again

        await using var db = fx.System();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Result.AmendmentDiscarded"));
        var second = await Do(patho, $"/api/results/{Rid(approved)}/amend", new { reason = "Second attempt", rowVersion = Rv(after) });

        // Once an amendment has been submitted it can no longer be discarded.
        var edited = await ApiClient.Body(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(Rv(second), (f.HbParam, "17", null))));
        ApiClient.Ok(await tech.PostAsync($"/api/results/{Rid(edited)}/submit", new { rowVersion = Rv(edited) }));
        Assert.Equal(HttpStatusCode.NotFound, (await tech.DeleteAsyncCompat($"/api/results/items/{item}/draft")).StatusCode);   // no draft left to discard
    }

    [Fact]
    public async Task Entry_needs_a_received_sample_at_the_working_branch()
    {
        var f = await Setup();
        // Collected but not yet received.
        var reception = await A("RECEPTION");
        var order = await ApiClient.Body(await reception.PostAsync("/api/orders", new { idempotencyKey = Guid.NewGuid(), patientId = f.PatientId, priority = 0, items = new object[] { new { testId = f.Hb } } }));
        var itemId = order.GetProperty("items")[0].GetProperty("id").GetInt64();
        var sid = (await ApiClient.Body(await reception.GetAsync($"/api/orders/{order.GetProperty("id").GetInt64()}/samples")))[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await (await A("PHLEBOTOMY")).PostAsync($"/api/samples/{sid}/collect", Confirmed));
        var tech = await A("TECHNICIAN");
        Assert.Equal("result_sample_not_ready", await ApiClient.Code(await tech.PutAsync($"/api/results/items/{itemId}/draft", Draft(null, (f.HbParam, "15", null)))));

        // Received, but the user works at another branch.
        ApiClient.Ok(await tech.PostAsync($"/api/samples/{sid}/receive"));
        var elsewhere = await At(fx.OrgASecondBranch, fx.OrgA, "TECHNICIAN");
        Assert.Equal("sample_not_at_branch", await ApiClient.Code(await elsewhere.PutAsync($"/api/results/items/{itemId}/draft", Draft(null, (f.HbParam, "15", null)))));
        ApiClient.Ok(await tech.PutAsync($"/api/results/items/{itemId}/draft", Draft(null, (f.HbParam, "15", null))));
    }

    [Fact]
    public async Task A_sample_with_saved_results_cannot_be_rejected_until_the_draft_is_discarded()
    {
        var f = await Setup();
        var (item, sid, _) = await ReceivedItem(f, f.Hb);
        var tech = await A("TECHNICIAN");
        ApiClient.Ok(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(null, (f.HbParam, "15", null))));
        var reasons = await ApiClient.Body(await tech.GetAsync("/api/samples/rejection-reasons"));
        var reason = reasons[0].GetProperty("id").GetInt32();
        Assert.Equal("sample_has_results", await ApiClient.Code(await tech.PostAsync($"/api/samples/{sid}/reject", new { reasonId = reason })));

        ApiClient.Ok(await tech.DeleteAsyncCompat($"/api/results/items/{item}/draft"), HttpStatusCode.NoContent);
        var entry = await Entry(tech, item);
        Assert.Equal(20, entry.GetProperty("itemStatus").GetInt32());                           // back to received
        ApiClient.Ok(await tech.PostAsync($"/api/samples/{sid}/reject", new { reasonId = reason }));
    }

    [Fact]
    public async Task Pending_queue_follows_the_workflow_and_each_queue_needs_its_own_permission()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Hb, urgent: true);
        var tech = await A("TECHNICIAN");
        var pending = await ApiClient.Body(await tech.GetAsync("/api/results/pending?urgentOnly=true&pageSize=100"));
        Assert.Contains(pending.GetProperty("items").EnumerateArray(), i => i.GetProperty("orderItemId").GetInt64() == item);

        var submitted = await EnterAndSubmit(tech, item, f.HbParam, "15");
        pending = await ApiClient.Body(await tech.GetAsync("/api/results/pending?pageSize=100"));
        Assert.DoesNotContain(pending.GetProperty("items").EnumerateArray(), i => i.GetProperty("orderItemId").GetInt64() == item);

        var queue = await ApiClient.Body(await tech.GetAsync("/api/results/queue?stage=verification&pageSize=100"));
        var row = queue.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("resultId").GetInt64() == Rid(submitted));
        Assert.True(row.GetProperty("isOwnEntry").GetBoolean());
        // A technician has no approve permission: the approval queue is closed to them.
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.GetAsync("/api/results/queue?stage=approval")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await A("PATHOLOGIST")).GetAsync("/api/results/queue?stage=approval")).StatusCode);
    }

    [Fact]
    public async Task Worklists_batch_received_tests_by_department_and_hold_each_test_once()
    {
        var f = await Setup();
        var (item1, _, _) = await ReceivedItem(f, f.Hb);
        var (item2, _, _) = await ReceivedItem(f, f.Opt);
        var tech = await A("TECHNICIAN");

        Assert.Equal("worklist_empty", await ApiClient.Code(await tech.PostAsync("/api/worklists", new { departmentId = f.Dept, orderItemIds = Array.Empty<long>() })));
        Assert.Equal("department_invalid", await ApiClient.Code(await tech.PostAsync("/api/worklists", new { departmentId = 999999, orderItemIds = new[] { item1 } })));
        Assert.Equal("worklist_item_invalid", await ApiClient.Code(await tech.PostAsync("/api/worklists", new { departmentId = f.OtherDept, orderItemIds = new[] { item1 } })));
        Assert.Equal("worklist_item_invalid", await ApiClient.Code(await tech.PostAsync("/api/worklists", new { departmentId = f.Dept, orderItemIds = new[] { item1, 999999L } })));

        var wl = await ApiClient.Body(await tech.PostAsync("/api/worklists", new { departmentId = f.Dept, orderItemIds = new[] { item1, item2 }, notes = "Morning run" }));
        Assert.Matches(@"^WL-", wl.GetProperty("worklist").GetProperty("worklistNo").GetString());
        Assert.Equal(2, wl.GetProperty("items").GetArrayLength());
        Assert.Contains(wl.GetProperty("items").EnumerateArray(), i => i.GetProperty("parameterCodes").GetArrayLength() == 1);

        // The same test cannot be on a second open worklist, even when two requests race.
        Assert.Equal("item_on_worklist", await ApiClient.Code(await tech.PostAsync("/api/worklists", new { departmentId = f.Dept, orderItemIds = new[] { item1 } })));
        var wlId = wl.GetProperty("worklist").GetProperty("id").GetInt64();
        var pending = await ApiClient.Body(await tech.GetAsync("/api/results/pending?pageSize=100"));
        Assert.Equal(wl.GetProperty("worklist").GetProperty("worklistNo").GetString(), pending.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("orderItemId").GetInt64() == item1).GetProperty("onWorklist").GetString());

        var closed = await ApiClient.Body(await tech.PostAsync($"/api/worklists/{wlId}/close"));
        Assert.Equal(1, closed.GetProperty("worklist").GetProperty("status").GetInt32());
        Assert.Equal("worklist_closed", await ApiClient.Code(await tech.PostAsync($"/api/worklists/{wlId}/close")));
        ApiClient.Ok(await tech.PostAsync("/api/worklists", new { departmentId = f.Dept, orderItemIds = new[] { item1 } }));   // free again

        Assert.Equal(HttpStatusCode.Forbidden, (await (await A("RECEPTION")).GetAsync("/api/worklists")).StatusCode);
    }

    [Fact]
    public async Task Results_are_hidden_from_other_organisations_and_from_branches_without_a_link()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Hb);
        var tech = await A("TECHNICIAN");
        var submitted = await EnterAndSubmit(tech, item, f.HbParam, "15");

        var otherOrg = await At(fx.OrgBMainBranch, fx.OrgB, "SYSADMIN");
        Assert.Equal(HttpStatusCode.NotFound, (await otherOrg.GetAsync($"/api/results/items/{item}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherOrg.GetAsync($"/api/results/{Rid(submitted)}")).StatusCode);

        var username = await Api.CreateUserAsync(fx.OrgA, ["PATHOLOGIST"], allBranches: false, branchIds: [fx.OrgASecondBranch]);
        var limited = await Api.ClientAsync();
        ApiClient.Ok(await limited.LoginAsync(username));
        ApiClient.Ok(await limited.PostAsync("/api/auth/active-branch", new { branchId = fx.OrgASecondBranch }));
        await limited.RefreshCsrfAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await limited.GetAsync($"/api/results/items/{item}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await limited.GetAsync($"/api/results/{Rid(submitted)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await limited.PostAsync($"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted) })).StatusCode);
    }

    [Fact]
    public async Task Every_result_endpoint_refuses_anonymous_and_token_less_requests_and_roles_stay_in_their_lane()
    {
        var anon = await Api.ClientAsync();
        foreach (var url in new[] { "/api/results/pending", "/api/results/queue", "/api/results/items/1", "/api/results/1", "/api/results/1/history", "/api/worklists", "/api/worklists/1", "/api/critical-alerts", "/api/critical-alerts/open-count", "/api/orders/1/results" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(url)).StatusCode);

        var user = await A("SYSADMIN");
        foreach (var url in new[] { "/api/results/1/submit", "/api/results/1/verify", "/api/results/1/approve", "/api/results/1/return", "/api/results/1/amend", "/api/worklists", "/api/worklists/1/close", "/api/critical-alerts/1/notify" })
            Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsync(url, new { }, withCsrf: false)).StatusCode);

        var f = await Setup();
        var (item, _, orderId) = await ReceivedItem(f, f.Hb);
        var tech = await A("TECHNICIAN");
        var submitted = await EnterAndSubmit(tech, item, f.HbParam, "15");

        var reception = await A("RECEPTION");            // no results.* permission
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync($"/api/results/items/{item}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync($"/api/orders/{orderId}/results")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PutAsync($"/api/results/items/{item}/draft", Draft(null))).StatusCode);

        var accountant = await A("ACCOUNTANT");
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("/api/results/pending")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync($"/api/results/{Rid(submitted)}/approve", new { rowVersion = Rv(submitted) })).StatusCode);   // verify, not approve
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsync($"/api/results/{Rid(submitted)}/amend", new { reason = "x", rowVersion = Rv(submitted) })).StatusCode);
        var phleb = await A("PHLEBOTOMY");
        Assert.Equal(HttpStatusCode.Forbidden, (await phleb.PostAsync($"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted) })).StatusCode);
    }

    [Fact]
    public async Task The_database_itself_refuses_inconsistent_results()
    {
        var f = await Setup();
        var (item, _, _) = await ReceivedItem(f, f.Hb);
        var submitted = await EnterAndSubmit(await A("TECHNICIAN"), item, f.HbParam, "15");
        await using var db = fx.System();

        // A second version in progress for the same test.
        var dup = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO lab.TestResults (OrganizationId,OrderItemId,OrderId,SampleId,PatientId,OrderBranchId,LabBranchId,Version,Status,AmendmentReason,CreatedAtUtc) " +
            "SELECT OrganizationId,OrderItemId,OrderId,SampleId,PatientId,OrderBranchId,LabBranchId,2,0,'x',CreatedAtUtc FROM lab.TestResults WHERE Id = {0}", Rid(submitted)));
        Assert.Contains("UX_TestResults_OneInProgress", (dup.InnerException ?? dup).Message);

        // Verified without a verifier.
        var half = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE lab.TestResults SET Status = 20 WHERE Id = {0}", Rid(submitted)));
        Assert.Contains("CK_TestResults_Verified", (half.InnerException ?? half).Message);

        // A value that is both a number and text.
        var both = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE lab.TestResultValues SET ValueText = 'x' WHERE ResultId = {0}", Rid(submitted)));
        Assert.Contains("CK_TestResultValues_OneKind", (both.InnerException ?? both).Message);
    }

    /// <summary>
    /// Editing an option parameter must keep the ids of options that stay (results reference ParameterOptions by id),
    /// must allow dropping an unused option, and must refuse dropping an option a result already used with 409 option_in_use.
    /// </summary>
    [Fact]
    public async Task Editing_an_option_parameter_keeps_ids_and_refuses_removing_an_option_in_use()
    {
        var f = await Setup();
        var admin = await A("SYSADMIN");
        var (item, _, _) = await ReceivedItem(f, f.Opt);
        var tech = await A("TECHNICIAN");

        // A stored result value references the "Positive" option.
        var e = await ApiClient.Body(await tech.PutAsync($"/api/results/items/{item}/draft", Draft(null, (f.OptParam, null, f.OptPositive))));
        Assert.Equal("Positive", e.GetProperty("values")[0].GetProperty("valueText").GetString());

        async Task<JsonElement> Param()
        {
            var detail = await ApiClient.Body(await admin.GetAsync($"/api/catalog/tests/{f.Opt}"));
            return detail.GetProperty("parameters").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == f.OptParam);
        }
        async Task<HttpResponseMessage> Save(params (string En, string? Ur, bool Ab)[] opts)
        {
            var p = await Param();
            return await admin.PutAsync($"/api/catalog/parameters/{f.OptParam}", new
            {
                code = p.GetProperty("code").GetString(),
                nameEn = "Screen",
                resultType = 2,
                isActive = true,
                rowVersion = p.GetProperty("rowVersion").GetString(),
                options = opts.Select(o => new { valueEn = o.En, valueUr = o.Ur, isAbnormal = o.Ab }).ToArray(),
            });
        }

        // Saving the same options again used to recreate every option row and hit the FK (500).
        ApiClient.Ok(await Save(("Negative", "منفی", false), ("Positive", "مثبت", true)), HttpStatusCode.NoContent);

        // The stored result still points at the original option id.
        e = await ApiClient.Body(await tech.GetAsync($"/api/results/items/{item}"));
        Assert.Equal(f.OptPositive, e.GetProperty("values")[0].GetProperty("optionId").GetInt32());
        await using (var db = fx.System())
            Assert.True(await db.Set<Lims.Domain.Catalog.ParameterOption>().IgnoreQueryFilters().AnyAsync(o => o.Id == f.OptPositive));

        // Dropping an unused option and adding a new one is allowed.
        ApiClient.Ok(await Save(("Positive", "مثبت", true), ("Indeterminate", "غیر متعین", true)), HttpStatusCode.NoContent);

        // An option a result already used can't be removed:409 with the translated code instead of a 500.
        var conflict = await Save(("Indeterminate", "غیر متعین", true), ("Negative", "منفی", false));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("option_in_use", await ApiClient.Code(conflict));

        // The used option survived the refused edit and the result still resolves.
        e = await ApiClient.Body(await tech.GetAsync($"/api/results/items/{item}"));
        Assert.Equal(f.OptPositive, e.GetProperty("values")[0].GetProperty("optionId").GetInt32());
        Assert.Equal("Positive", e.GetProperty("values")[0].GetProperty("valueText").GetString());
    }
}

internal static class ResultClientExtensions
{
    /// <summary>The shared test client has no DELETE helper; the CSRF header is read from the same client state.</summary>
    public static async Task<HttpResponseMessage> DeleteAsyncCompat(this ApiClient c, string url)
    {
        var token = (await ApiClient.Body(await c.GetAsync("/api/auth/csrf"))).GetProperty("token").GetString();
        var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add("X-XSRF-TOKEN", token);
        return await c.Http.SendAsync(req);
    }
}
