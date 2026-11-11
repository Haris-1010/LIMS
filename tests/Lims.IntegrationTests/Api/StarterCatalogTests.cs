using System.Net;
using Lims.Domain.Catalog;
using Lims.Infrastructure.Catalog;
using Lims.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

/// <summary>The starter test catalog (common tests with draft ranges) and the one-person-laboratory setting defaults.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class StarterCatalogTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;

    private async Task<int> NewOrg()
    {
        await using var db = fx.System();
        return await new OrganizationProvisioner(db).ProvisionAsync(new("ST" + Guid.NewGuid().ToString("N")[..6], "Starter lab", null, "MAIN", "Main", null));
    }

    private async Task<StarterCatalogResult> Seed(int org)
    {
        await using var db = fx.System();
        return await new StarterCatalogSeeder(db).SeedAsync(org);
    }

    private async Task<ApiClient> SignedIn(int org, params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(org, roles)));
        return c;
    }

    // ---------------------------------------------------------------- the data itself (no database)

    [Fact]
    public void Starter_data_is_consistent()
    {
        var tests = StarterCatalog.Tests;
        Assert.Equal(tests.Length, tests.Select(t => t.Code).Distinct().Count());
        var categories = StarterCatalog.Categories.Select(c => c.Code).ToHashSet();
        foreach (var t in tests)
        {
            Assert.True(t.Code.Length <= 20, t.Code);
            Assert.Contains(t.Category, categories);
            Assert.NotEmpty(t.Parameters);
            Assert.Equal(t.Parameters.Length, t.Parameters.Select(p => p.Code).Distinct().Count());
            foreach (var p in t.Parameters)
            {
                var where = $"{t.Code}/{p.Code}";
                if (p.Type == ResultType.Option)
                {
                    Assert.True(p.Options.Length >= 2, where);
                    Assert.Equal(p.Options.Length, p.Options.Select(o => o.En).Distinct().Count());
                }
                else Assert.Empty(p.Options);

                var ranges = p.Ranges.Select(r => new ReferenceRange
                {
                    Sex = r.Sex, AgeFromDays = r.AllAges ? 0 : StarterCatalog.AdultFromDays, Low = r.Low, High = r.High,
                    CriticalLow = r.CriticalLow, CriticalHigh = r.CriticalHigh, DisplayText = r.Text, Source = StarterCatalog.RangeSource,
                }).ToList();
                foreach (var r in ranges)
                    Assert.True(ReferenceRules.Validate(r, p.Type) is null, $"{where}: {ReferenceRules.Validate(r, p.Type)}");
                // All of a parameter's ranges can be approved together: none overlaps another.
                for (var i = 0; i < ranges.Count; i++)
                    for (var j = i + 1; j < ranges.Count; j++)
                        Assert.False(ReferenceRules.Overlaps(ranges[i], ranges[j]), where);
                // An option's "normal" text is one of its options, so the report reads consistently.
                if (p.Type == ResultType.Option)
                    foreach (var r in p.Ranges) Assert.Contains(r.Text, p.Options.Select(o => o.En));
            }
        }
        Assert.True(StarterCatalog.RangeSource.Length <= 300);
    }

    // ---------------------------------------------------------------- seeding an organisation

    [Fact]
    public async Task Starter_catalog_is_added_once_with_every_range_waiting_for_approval()
    {
        var org = await NewOrg();
        var first = await Seed(org);
        Assert.False(first.AlreadyAdded);
        Assert.Equal(StarterCatalog.Tests.Length, first.Tests);
        Assert.Equal(StarterCatalog.Tests.Sum(t => t.Parameters.Length), first.Parameters);
        Assert.Equal(StarterCatalog.Tests.Sum(t => t.Parameters.Sum(p => p.Ranges.Length)), first.Ranges);

        await using (var db = fx.System())
        {
            var ranges = await db.ReferenceRanges.Where(r => r.OrganizationId == org).ToListAsync();
            Assert.Equal(first.Ranges, ranges.Count);
            Assert.All(ranges, r => Assert.Equal(RangeStatus.Draft, r.Status));
            Assert.All(ranges, r => Assert.Equal(StarterCatalog.RangeSource, r.Source));
            var cbc = await db.Tests.Include(t => t.Parameters).SingleAsync(t => t.OrganizationId == org && t.Code == "CBC");
            Assert.Equal(0m, cbc.Price);
            Assert.True(cbc.IsActive);
            Assert.Equal(14, cbc.Parameters.Count);
            var hbUnit = cbc.Parameters.Single(p => p.Code == "HB").UnitId;
            Assert.Equal("g/dL", await db.Units.Where(u => u.Id == hbUnit).Select(u => u.Code).SingleAsync());
            // Another organisation's catalog is untouched.
            Assert.False(await db.Tests.AnyAsync(t => t.OrganizationId == fx.OrgB && t.Code == "LIPID"));
        }

        // Running again (every db migrate does) adds nothing, even after a starter test was deleted.
        await using (var db = fx.System())
            await db.Tests.Where(t => t.OrganizationId == org && t.Code == "ESR").ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDeleted, true));
        var second = await Seed(org);
        Assert.True(second.AlreadyAdded);
        await using (var db = fx.System())
            Assert.Equal(first.Tests, await db.Tests.IgnoreQueryFilters().CountAsync(t => t.OrganizationId == org));
    }

    [Fact]
    public async Task A_test_code_the_laboratory_already_uses_is_left_alone()
    {
        var org = await NewOrg();
        await using (var db = fx.System())
        {
            var dept = await db.Departments.FirstAsync(d => d.OrganizationId == org);
            var cat = new TestCategory { OrganizationId = org, Code = "OWN", NameEn = "Own" };
            db.TestCategories.Add(cat);
            await db.SaveChangesAsync();
            db.Tests.Add(new LabTest { OrganizationId = org, Code = "CBC", NameEn = "Our own CBC", CategoryId = cat.Id, DepartmentId = dept.Id, SampleTypeId = 1, Price = 650 });
            await db.SaveChangesAsync();
        }
        var result = await Seed(org);
        Assert.Equal(1, result.SkippedTests);
        Assert.Equal(StarterCatalog.Tests.Length - 1, result.Tests);
        await using (var db = fx.System())
        {
            var cbc = await db.Tests.Include(t => t.Parameters).SingleAsync(t => t.OrganizationId == org && t.Code == "CBC");
            Assert.Equal("Our own CBC", cbc.NameEn);
            Assert.Empty(cbc.Parameters);
        }
    }

    [Fact]
    public async Task A_pathologist_can_approve_all_starter_ranges_at_once_and_results_are_then_flagged_by_them()
    {
        var org = await NewOrg();
        var seeded = await Seed(org);
        var patho = await SignedIn(org, "PATHOLOGIST");
        var pending = await ApiClient.Body(await patho.GetAsync("/api/catalog/ranges/pending"));
        Assert.Equal(seeded.Ranges, pending.GetArrayLength());

        var ids = pending.EnumerateArray().Select(p => p.GetProperty("range").GetProperty("id").GetInt32()).ToArray();
        var res = await ApiClient.Body(await patho.PostAsync("/api/catalog/ranges/approve-many", new { ids }));
        Assert.Equal(seeded.Ranges, res.GetProperty("approved").GetInt32());
        Assert.Equal(0, res.GetProperty("failed").GetArrayLength());
        Assert.Equal(0, (await ApiClient.Body(await patho.GetAsync("/api/catalog/ranges/pending"))).GetArrayLength());

        await using var db = fx.System();
        var hb = await db.TestParameters.Include(p => p.Ranges).SingleAsync(p => p.OrganizationId == org && p.Test!.Code == "CBC" && p.Code == "HB");
        var maleAdult = ReferenceRules.Select(hb.Ranges, Lims.Domain.Patients.Sex.Male, 40 * 365);
        Assert.Equal(ResultFlag.Low, ReferenceRules.Evaluate(11.5m, maleAdult));
        Assert.Equal(ResultFlag.CriticalLow, ReferenceRules.Evaluate(6.5m, maleAdult));
        Assert.Null(ReferenceRules.Select(hb.Ranges, Lims.Domain.Patients.Sex.Male, 5 * 365));   // no child range: not flagged
    }

    // ---------------------------------------------------------------- one-person laboratory defaults

    [Fact]
    public async Task A_new_laboratory_allows_self_verification_and_does_not_block_approval_on_critical_read_back()
    {
        var org = await NewOrg();
        var admin = await SignedIn(org, "SYSADMIN");
        var settings = (await ApiClient.Body(await admin.GetAsync("/api/settings"))).EnumerateArray()
            .ToDictionary(s => s.GetProperty("key").GetString()!, s => s.GetProperty("value").GetString());
        Assert.Equal("true", settings["Results.AllowSelfVerification"]);
        Assert.Equal("false", settings["Results.BlockApprovalUntilCriticalNotified"]);
    }

    [Fact]
    public async Task In_a_one_person_laboratory_the_same_user_enters_verifies_and_approves_a_critical_result()
    {
        var org = await NewOrg();
        await Seed(org);
        int branch, fbs, glucose;
        await using (var db = fx.System())
        {
            branch = await db.Branches.Where(b => b.OrganizationId == org && b.IsMain).Select(b => b.Id).SingleAsync();
            fbs = await db.Tests.Where(t => t.OrganizationId == org && t.Code == "FBS").Select(t => t.Id).SingleAsync();
            glucose = await db.TestParameters.Where(p => p.OrganizationId == org && p.TestId == fbs).Select(p => p.Id).SingleAsync();
        }

        var solo = await Api.ClientAsync();
        ApiClient.Ok(await solo.LoginAsync(await Api.CreateUserAsync(org, ["SYSADMIN", "PATHOLOGIST", "TECHNICIAN", "RECEPTION", "PHLEBOTOMY"])));
        ApiClient.Ok(await solo.PostAsync("/api/auth/active-branch", new { branchId = branch }));
        await solo.RefreshCsrfAsync();

        var pending = await ApiClient.Body(await solo.GetAsync("/api/catalog/ranges/pending"));
        var ids = pending.EnumerateArray().Select(p => p.GetProperty("range").GetProperty("id").GetInt32()).ToArray();
        ApiClient.Ok(await solo.PostAsync("/api/catalog/ranges/approve-many", new { ids }));

        var patient = (await ApiClient.Body(await solo.PostAsync("/api/patients", new { fullName = "Solo Patient " + Guid.NewGuid().ToString("N")[..6], sex = 1, ageYears = 40, confirmNotDuplicate = true }))).GetProperty("id").GetInt32();
        var order = await ApiClient.Body(await solo.PostAsync("/api/orders", new { idempotencyKey = Guid.NewGuid(), patientId = patient, priority = 0, items = new object[] { new { testId = fbs } } }));
        var orderId = order.GetProperty("id").GetInt64();
        var item = order.GetProperty("items")[0].GetProperty("id").GetInt64();
        var sample = (await ApiClient.Body(await solo.GetAsync($"/api/orders/{orderId}/samples")))[0].GetProperty("id").GetInt64();
        ApiClient.Ok(await solo.PostAsync($"/api/samples/{sample}/collect", new { identityConfirmed = true }));
        ApiClient.Ok(await solo.PostAsync($"/api/samples/{sample}/receive"));

        // 30 mg/dL is below the starter critical limit (40): an alert is raised, but approval is not blocked.
        var saved = await ApiClient.Body(await solo.PutAsync($"/api/results/items/{item}/draft",
            new { rowVersion = (string?)null, comment = (string?)null, values = new[] { new { parameterId = glucose, value = "30", optionId = (int?)null } } }));
        static string Rv(System.Text.Json.JsonElement e) => e.GetProperty("current").GetProperty("rowVersion").GetString()!;
        static long Rid(System.Text.Json.JsonElement e) => e.GetProperty("current").GetProperty("id").GetInt64();
        var submitted = await ApiClient.Body(await solo.PostAsync($"/api/results/{Rid(saved)}/submit", new { rowVersion = Rv(saved) }));
        Assert.Equal((int)ResultFlag.CriticalLow, submitted.GetProperty("values")[0].GetProperty("flag").GetInt32());
        Assert.True(submitted.GetProperty("actions").GetProperty("verify").GetBoolean());

        var verified = await ApiClient.Body(await solo.PostAsync($"/api/results/{Rid(submitted)}/verify", new { rowVersion = Rv(submitted) }));
        var approved = await solo.PostAsync($"/api/results/{Rid(verified)}/approve", new { rowVersion = Rv(verified) });
        ApiClient.Ok(approved);
    }
}
