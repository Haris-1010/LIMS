using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

/// <summary>Catalog workflow through the real API. Range numbers are arbitrary fixtures, not clinical values.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CatalogApiTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;

    private async Task<ApiClient> SignedIn(int org, params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(org, roles)));
        return c;
    }

    private static string Code(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<(int categoryId, int departmentId)> Basics(ApiClient admin)
    {
        var cat = await ApiClient.Body(await admin.PostAsync("/api/catalog/categories", new { code = Code("C"), nameEn = "Test category" }));
        var depts = await ApiClient.Body(await admin.GetAsync("/api/organization/departments"));
        return (cat.GetProperty("id").GetInt32(), depts[0].GetProperty("id").GetInt32());
    }

    private async Task<JsonElement> CreateTest(ApiClient admin, decimal price = 500)
    {
        var (cat, dept) = await Basics(admin);
        var res = await admin.PostAsync("/api/catalog/tests", new
        {
            code = Code("T"), nameEn = "Fixture test", categoryId = cat, departmentId = dept, sampleTypeId = 2, turnaroundHours = 24, price,
        });
        ApiClient.Ok(res, HttpStatusCode.Created);
        return await ApiClient.Body(res);
    }

    private async Task<int> AddNumericParameter(ApiClient admin, int testId)
    {
        var res = await admin.PostAsync($"/api/catalog/tests/{testId}/parameters", new { code = Code("P"), nameEn = "Analyte", resultType = 1, unitId = 10, decimalPlaces = 1 });
        ApiClient.Ok(res);
        return (await ApiClient.Body(res)).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Test_with_parameters_and_options_round_trips()
    {
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var test = await CreateTest(admin);
        var id = test.GetProperty("id").GetInt32();
        await AddNumericParameter(admin, id);
        ApiClient.Ok(await admin.PostAsync($"/api/catalog/tests/{id}/parameters", new
        {
            code = "QUAL", nameEn = "Screen", resultType = 2,
            options = new[] { new { valueEn = "Negative", valueUr = "منفی", isAbnormal = false }, new { valueEn = "Positive", valueUr = "مثبت", isAbnormal = true } },
        }));

        var detail = await ApiClient.Body(await admin.GetAsync($"/api/catalog/tests/{id}"));
        var parameters = detail.GetProperty("parameters");
        Assert.Equal(2, parameters.GetArrayLength());
        var qual = parameters.EnumerateArray().Single(p => p.GetProperty("code").GetString() == "QUAL");
        Assert.Equal(2, qual.GetProperty("options").GetArrayLength());
        Assert.Equal("مثبت", qual.GetProperty("options")[1].GetProperty("valueUr").GetString());

        // Option parameters need at least two values.
        var bad = await admin.PostAsync($"/api/catalog/tests/{id}/parameters", new { code = "ONE", nameEn = "One", resultType = 2, options = new[] { new { valueEn = "Only" } } });
        Assert.Equal("options_required", await ApiClient.Code(bad));
    }

    [Fact]
    public async Task Range_lifecycle_draft_approve_replace_retire()
    {
        var admin = await SignedIn(fx.OrgA, "LABMGR");          // catalog.manage + clinical
        var test = await CreateTest(admin);
        var pid = await AddNumericParameter(admin, test.GetProperty("id").GetInt32());

        var draft = await ApiClient.Body(await admin.PostAsync($"/api/catalog/parameters/{pid}/ranges", new { sex = 0, ageFromDays = 0, low = 10, high = 20 }));
        var rangeId = draft.GetProperty("id").GetInt32();

        // Approval requires a recorded source.
        var noSource = await admin.PostAsync($"/api/catalog/ranges/{rangeId}/approve");
        Assert.Equal("range_source_required", await ApiClient.Code(noSource));

        var ranges = await ApiClient.Body(await admin.GetAsync($"/api/catalog/parameters/{pid}/ranges"));
        ApiClient.Ok(await admin.PutAsync($"/api/catalog/ranges/{rangeId}", new
        {
            sex = 0, ageFromDays = 0, low = 10, high = 20, source = "Fixture source", rowVersion = ranges[0].GetProperty("rowVersion").GetString(),
        }), HttpStatusCode.NoContent);
        ApiClient.Ok(await admin.PostAsync($"/api/catalog/ranges/{rangeId}/approve"), HttpStatusCode.NoContent);

        // Approved ranges are immutable.
        ranges = await ApiClient.Body(await admin.GetAsync($"/api/catalog/parameters/{pid}/ranges"));
        var edit = await admin.PutAsync($"/api/catalog/ranges/{rangeId}", new { sex = 0, ageFromDays = 0, low = 11, high = 20, rowVersion = ranges[0].GetProperty("rowVersion").GetString() });
        Assert.Equal("range_not_draft", await ApiClient.Code(edit));

        // A second overlapping range can't be approved alongside it…
        var clash = await ApiClient.Body(await admin.PostAsync($"/api/catalog/parameters/{pid}/ranges", new { sex = 2, ageFromDays = 0, low = 12, high = 22, source = "Fixture" }));
        var clashRes = await admin.PostAsync($"/api/catalog/ranges/{clash.GetProperty("id").GetInt32()}/approve");
        Assert.Equal("range_overlap", await ApiClient.Code(clashRes));

        // …but a draft that replaces it can, and the old one is retired in the same step.
        var replacement = await ApiClient.Body(await admin.PostAsync($"/api/catalog/parameters/{pid}/ranges", new { sex = 0, ageFromDays = 0, low = 11, high = 21, source = "Fixture v2", replacesRangeId = rangeId }));
        ApiClient.Ok(await admin.PostAsync($"/api/catalog/ranges/{replacement.GetProperty("id").GetInt32()}/approve"), HttpStatusCode.NoContent);

        await using var db = fx.System();
        var all = await db.ReferenceRanges.Where(r => r.ParameterId == pid).ToListAsync();
        Assert.Equal(Lims.Domain.Catalog.RangeStatus.Retired, all.Single(r => r.Id == rangeId).Status);
        var current = all.Single(r => r.Status == Lims.Domain.Catalog.RangeStatus.Approved);
        Assert.Equal(11m, current.Low);
        Assert.NotNull(current.ApprovedByUserId);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "ReferenceRange.Approved" && a.EntityId == current.Id.ToString()));
    }

    [Fact]
    public async Task Only_clinical_permission_can_approve_ranges()
    {
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var test = await CreateTest(admin);
        var pid = await AddNumericParameter(admin, test.GetProperty("id").GetInt32());
        var draft = await ApiClient.Body(await admin.PostAsync($"/api/catalog/parameters/{pid}/ranges", new { sex = 0, ageFromDays = 0, low = 1, high = 2, source = "Fixture" }));
        var id = draft.GetProperty("id").GetInt32();

        var technician = await SignedIn(fx.OrgA, "TECHNICIAN");       // catalog.view only
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsync($"/api/catalog/ranges/{id}/approve")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsync($"/api/catalog/parameters/{pid}/ranges", new { sex = 0, ageFromDays = 0, low = 1, high = 2 })).StatusCode);

        var pathologist = await SignedIn(fx.OrgA, "PATHOLOGIST");     // catalog.clinical
        var pending = await ApiClient.Body(await pathologist.GetAsync("/api/catalog/ranges/pending"));
        Assert.Contains(pending.EnumerateArray(), p => p.GetProperty("range").GetProperty("id").GetInt32() == id);
        ApiClient.Ok(await pathologist.PostAsync($"/api/catalog/ranges/{id}/approve"), HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Approve_many_approves_each_draft_and_reports_the_ones_that_fail()
    {
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var test = await CreateTest(admin);
        var p1 = await AddNumericParameter(admin, test.GetProperty("id").GetInt32());
        var p2 = await AddNumericParameter(admin, test.GetProperty("id").GetInt32());
        async Task<int> Draft(int pid, int sex, string? source) =>
            (await ApiClient.Body(await admin.PostAsync($"/api/catalog/parameters/{pid}/ranges", new { sex, ageFromDays = 0, low = 1, high = 2, source }))).GetProperty("id").GetInt32();

        var male = await Draft(p1, 1, "Fixture");
        var female = await Draft(p1, 2, "Fixture");
        var any = await Draft(p2, 0, "Fixture");
        var overlapsAny = await Draft(p2, 1, "Fixture");    // approved after "any" in the same batch -> overlap
        var noSource = await Draft(p2, 2, null);

        // Clinical permission is required, as for a single approval.
        var technician = await SignedIn(fx.OrgA, "TECHNICIAN");
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsync("/api/catalog/ranges/approve-many", new { ids = new[] { male } })).StatusCode);

        var pathologist = await SignedIn(fx.OrgA, "PATHOLOGIST");
        var result = await ApiClient.Body(await pathologist.PostAsync("/api/catalog/ranges/approve-many", new { ids = new[] { male, female, any, overlapsAny, noSource } }));
        Assert.Equal(3, result.GetProperty("approved").GetInt32());
        var failed = result.GetProperty("failed").EnumerateArray().ToDictionary(f => f.GetProperty("id").GetInt32(), f => f.GetProperty("code").GetString());
        Assert.Equal(2, failed.Count);
        Assert.Equal("range_overlap", failed[overlapsAny]);
        Assert.Equal("range_source_required", failed[noSource]);

        await using var db = fx.System();
        var all = await db.ReferenceRanges.Where(r => r.ParameterId == p1 || r.ParameterId == p2).ToDictionaryAsync(r => r.Id);
        Assert.All(new[] { male, female, any }, id => Assert.Equal(Lims.Domain.Catalog.RangeStatus.Approved, all[id].Status));
        Assert.All(new[] { overlapsAny, noSource }, id => Assert.Equal(Lims.Domain.Catalog.RangeStatus.Draft, all[id].Status));
        Assert.Equal(3, await db.AuditLogs.CountAsync(a => a.Action == "ReferenceRange.Approved" && (a.EntityId == male.ToString() || a.EntityId == female.ToString() || a.EntityId == any.ToString())));

        // Ranges of another organisation are not found, so they are reported as failures and left alone.
        var otherAdmin = await SignedIn(fx.OrgB, "SYSADMIN");
        var otherTest = await CreateTest(otherAdmin);
        var otherPid = await AddNumericParameter(otherAdmin, otherTest.GetProperty("id").GetInt32());
        var foreign = (await ApiClient.Body(await otherAdmin.PostAsync($"/api/catalog/parameters/{otherPid}/ranges", new { sex = 0, ageFromDays = 0, low = 1, high = 2, source = "Fixture" }))).GetProperty("id").GetInt32();
        var cross = await ApiClient.Body(await pathologist.PostAsync("/api/catalog/ranges/approve-many", new { ids = new[] { foreign } }));
        Assert.Equal(0, cross.GetProperty("approved").GetInt32());
        Assert.Equal("not_found", cross.GetProperty("failed")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Inconsistent_range_is_rejected_on_entry()
    {
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var test = await CreateTest(admin);
        var pid = await AddNumericParameter(admin, test.GetProperty("id").GetInt32());
        var res = await admin.PostAsync($"/api/catalog/parameters/{pid}/ranges", new { sex = 0, ageFromDays = 0, low = 30, high = 20 });
        Assert.Equal("range_low_above_high", await ApiClient.Code(res));
    }

    [Fact]
    public async Task Prices_need_the_pricing_permission()
    {
        // A role with catalog.manage but without catalog.pricing.
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var role = await ApiClient.Body(await admin.PostAsync("/api/roles", new { code = Code("R"), nameEn = "Catalog editor", permissions = new[] { "catalog.view", "catalog.manage" } }));
        string roleCode;
        await using (var db = fx.System())
            roleCode = await db.Roles.Where(r => r.Id == role.GetProperty("id").GetInt32()).Select(r => r.Code).SingleAsync();
        var editor = await SignedIn(fx.OrgA, roleCode);

        var (cat, dept) = await Basics(admin);
        var priced = await editor.PostAsync("/api/catalog/tests", new { code = Code("T"), nameEn = "Priced", categoryId = cat, departmentId = dept, sampleTypeId = 1, price = 100 });
        Assert.Equal(HttpStatusCode.Forbidden, priced.StatusCode);
        Assert.Equal("pricing_forbidden", await ApiClient.Code(priced));

        // Without a price the editor can create the test; the accountant sets the price.
        ApiClient.Ok(await editor.PostAsync("/api/catalog/tests", new { code = Code("T"), nameEn = "Unpriced", categoryId = cat, departmentId = dept, sampleTypeId = 1, price = 0 }), HttpStatusCode.Created);
    }

    [Fact]
    public async Task Packages_bundle_tests_and_report_the_individual_total()
    {
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var t1 = (await CreateTest(admin, 400)).GetProperty("id").GetInt32();
        var t2 = (await CreateTest(admin, 600)).GetProperty("id").GetInt32();

        var tooSmall = await admin.PostAsync("/api/catalog/packages", new { code = Code("K"), nameEn = "Too small", price = 100, testIds = new[] { t1 } });
        Assert.Equal("package_tests_required", await ApiClient.Code(tooSmall));

        var code = Code("K");
        ApiClient.Ok(await admin.PostAsync("/api/catalog/packages", new { code, nameEn = "Fixture package", price = 850, testIds = new[] { t1, t2 } }));
        var packages = await ApiClient.Body(await admin.GetAsync("/api/catalog/packages"));
        var pkg = packages.EnumerateArray().Single(p => p.GetProperty("code").GetString() == code);
        Assert.Equal(1000m, pkg.GetProperty("testsTotal").GetDecimal());
        Assert.Equal(850m, pkg.GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task Catalog_is_isolated_between_organisations()
    {
        var adminA = await SignedIn(fx.OrgA, "SYSADMIN");
        var test = await CreateTest(adminA);
        var adminB = await SignedIn(fx.OrgB, "SYSADMIN");
        Assert.Equal(HttpStatusCode.NotFound, (await adminB.GetAsync($"/api/catalog/tests/{test.GetProperty("id").GetInt32()}")).StatusCode);
        var pid = test.GetProperty("id").GetInt32();
        var addToForeign = await adminB.PostAsync($"/api/catalog/tests/{pid}/parameters", new { code = "X", nameEn = "X", resultType = 1 });
        Assert.Equal(HttpStatusCode.NotFound, addToForeign.StatusCode);
    }
}
