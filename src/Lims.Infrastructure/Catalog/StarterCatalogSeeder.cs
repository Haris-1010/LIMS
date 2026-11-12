using Lims.Domain.Catalog;
using Lims.Domain.Core;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Catalog;

public sealed record StarterCatalogResult(bool AlreadyAdded, int Tests, int Parameters, int Ranges, int SkippedTests);

/// <summary>
/// Adds <see cref="StarterCatalog"/> to one organisation, once. Runs from the server commands (<c>db migrate</c> for every
/// organisation, <c>db create-org</c> for the new one) in the system context, so every query here is scoped by
/// <c>OrganizationId</c> itself. A test whose code the organisation already uses (even deleted) is left alone; when the
/// catalog has been added once (marker setting), nothing is added again, so tests the laboratory removed stay removed.
/// </summary>
public sealed class StarterCatalogSeeder(LimsDbContext db)
{
    public async Task<int> SeedRateListAsync(int orgId, CancellationToken ct = default)
    {
        var marker = await db.BusinessSettings.SingleOrDefaultAsync(
            s => s.OrganizationId == orgId && s.BranchId == null && s.Key == RateListCatalog.MarkerKey, ct);
        if (marker?.Value == RateListCatalog.Version)
            return 0;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Code, u => u.Id, ct);
        var sampleTypes = await db.SampleTypes.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Id, ct);
        var departments = await db.Departments.Where(d => d.OrganizationId == orgId).ToDictionaryAsync(d => d.Code, ct);
        if (!departments.TryGetValue("CPATH", out var department))
        {
            department = new Department { OrganizationId = orgId, Code = "CPATH", NameEn = "Clinical Pathology", NameUr = "کلینیکل پیتھالوجی", SortOrder = 100 };
            db.Departments.Add(department);
            await db.SaveChangesAsync(ct);
        }

        var category = await db.TestCategories.SingleOrDefaultAsync(c => c.OrganizationId == orgId && c.Code == "RATE-LIST", ct);
        if (category is null)
        {
            category = new TestCategory { OrganizationId = orgId, Code = "RATE-LIST", NameEn = "Rate list tests", NameUr = "ریٹ لسٹ ٹیسٹ", SortOrder = 1000 };
            db.TestCategories.Add(category);
            await db.SaveChangesAsync(ct);
        }

        var tests = await db.Tests.IgnoreQueryFilters().Where(t => t.OrganizationId == orgId)
            .Include(t => t.Parameters)
            .ToListAsync(ct);
        var usedCodes = tests.Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextSort = tests.Select(t => (int?)t.SortOrder).Max() ?? 0;
        var existingRates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["2893"] = "BG", ["1304"] = "ESR", ["1401"] = "FBS", ["2800"] = "RBS", ["4802"] = "HBA1C",
            ["1509"] = "LFT", ["1618"] = "RFT", ["573"] = "ELEC", ["575"] = "LIPID", ["2704"] = "UA",
            ["1705"] = "CA", ["4103"] = "TSH", ["4111"] = "B12", ["4606"] = "FERR",
        };
        var added = 0;
        var canonicalRates = RateListCatalog.Rates
            .GroupBy(r => r.SourceCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToArray();
        foreach (var rate in canonicalRates)
        {
            if (existingRates.TryGetValue(rate.SourceCode, out var existingCode))
            {
                var existing = tests.FirstOrDefault(t => t.Code == existingCode);
                if (existing is not null && existing.Price == 0)
                    existing.Price = rate.Price;
                continue;
            }

            var code = $"TZ-{rate.Row.PadLeft(4, '0')}";
            if (!usedCodes.Add(code)) continue;
            var sampleCode = RateListCatalog.SampleTypeCode(rate.Specimen);
            if (!sampleTypes.TryGetValue(sampleCode, out var sampleTypeId))
                sampleTypeId = sampleTypes["SERUM"];
            var importedTest = new LabTest
            {
                OrganizationId = orgId,
                Code = code,
                NameEn = rate.Name.Length > 200 ? rate.Name[..200] : rate.Name,
                ShortName = rate.SourceCode.Length <= 30 ? rate.SourceCode : null,
                CategoryId = category.Id,
                DepartmentId = department.Id,
                SampleTypeId = sampleTypeId,
                TurnaroundHours = 24,
                Price = rate.Price,
                SortOrder = nextSort += 10,
                AvailableAllBranches = true,
            };
            db.Tests.Add(importedTest);
            tests.Add(importedTest);
            added++;
        }

        ReconcileRateList(tests);
        AddKnownParameters(orgId, tests, units);
        if (marker is null)
            db.BusinessSettings.Add(new BusinessSetting { OrganizationId = orgId, Key = RateListCatalog.MarkerKey, Value = RateListCatalog.Version });
        else
            marker.Value = RateListCatalog.Version;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return added;
    }

    private static void ReconcileRateList(IReadOnlyCollection<LabTest> tests)
    {
        var imported = tests.Where(t => t.Code.StartsWith("TZ-", StringComparison.OrdinalIgnoreCase)).ToList();
        var ratesByCode = RateListCatalog.Rates
            .GroupBy(r => r.SourceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Row, StringComparer.OrdinalIgnoreCase);

        foreach (var group in imported
            .Where(t => RateListCatalog.Rates.Any(r => t.Code.EndsWith(r.Row.PadLeft(4, '0'), StringComparison.OrdinalIgnoreCase)))
            .GroupBy(t => RateListCatalog.Rates.First(r => t.Code.Equals($"TZ-{r.Row.PadLeft(4, '0')}", StringComparison.OrdinalIgnoreCase)).SourceCode,
                StringComparer.OrdinalIgnoreCase))
        {
            var canonicalRow = ratesByCode[group.Key].PadLeft(4, '0');
            foreach (var duplicate in group.Where(t => !t.Code.Equals($"TZ-{canonicalRow}", StringComparison.OrdinalIgnoreCase)))
            {
                duplicate.IsActive = false;
                duplicate.IsDeleted = true;
                duplicate.DeletedAtUtc ??= DateTime.UtcNow;
            }
        }
    }

    private static void AddKnownParameters(
        int orgId,
        IReadOnlyCollection<LabTest> tests,
        IReadOnlyDictionary<string, int> units)
    {
        var templates = StarterCatalog.Tests
            .SelectMany(t => t.Parameters.Select(p => (Test: t.Code, Parameter: p)))
            .ToDictionary(x => $"{x.Test}:{x.Parameter.Code}", x => x.Parameter, StringComparer.OrdinalIgnoreCase);
        var parameterTemplates = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["1351"] = ["CBC:*"],
            ["1301"] = ["CBC:HB"], ["1302"] = ["CBC:WBC"], ["1311"] = ["CBC:PLT"],
            ["1304"] = ["ESR:ESR"],
            ["1501"] = ["LFT:TBIL"], ["9987"] = ["LFT:DBIL"], ["1503"] = ["LFT:ALT"],
            ["1504"] = ["LFT:AST"], ["1505"] = ["LFT:ALP"], ["1508"] = ["LFT:ALB"],
            ["12360"] = ["RFT:CREAT"],
            ["1701"] = ["ELEC:NA"], ["1702"] = ["ELEC:K"],
            ["1902"] = ["LIPID:CHOL"], ["1903"] = ["LIPID:TG"], ["1904"] = ["LIPID:HDL"], ["1905"] = ["LIPID:LDL"],
            ["2704"] = ["UA:UA"], ["4103"] = ["TSH:TSH"], ["4111"] = ["B12:B12"], ["4606"] = ["FERR:FERR"],
        };
        var sourceByTestCode = RateListCatalog.Rates.ToDictionary(r => $"TZ-{r.Row.PadLeft(4, '0')}", r => r.SourceCode,
            StringComparer.OrdinalIgnoreCase);

        foreach (var test in tests.Where(t => t.IsActive && !t.IsDeleted && t.Parameters.Count == 0))
        {
            if (!sourceByTestCode.TryGetValue(test.Code, out var sourceCode) ||
                !parameterTemplates.TryGetValue(sourceCode, out var requested))
                continue;

            var selected = requested.SelectMany(key =>
            {
                if (key.EndsWith(":*", StringComparison.Ordinal))
                    return templates.Where(t => t.Key.StartsWith(key[..^1], StringComparison.OrdinalIgnoreCase)).Select(t => t.Value);
                return templates.TryGetValue(key, out var parameter) ? [parameter] : [];
            }).ToArray();

            var sort = 0;
            foreach (var source in selected)
            {
                var parameter = new TestParameter
                {
                    OrganizationId = orgId,
                    Code = source.Code,
                    NameEn = source.En,
                    ResultType = source.Type,
                    UnitId = source.Unit is null ? null : units.GetValueOrDefault(source.Unit),
                    DecimalPlaces = source.Decimals,
                    GroupName = source.Group,
                    SortOrder = sort += 10,
                };
                var optionSort = 0;
                foreach (var option in source.Options)
                    parameter.Options.Add(new ParameterOption
                    {
                        OrganizationId = orgId, ValueEn = option.En, ValueUr = option.Ur,
                        IsAbnormal = option.Abnormal, SortOrder = optionSort += 10,
                    });
                foreach (var range in source.Ranges)
                    parameter.Ranges.Add(new ReferenceRange
                    {
                        OrganizationId = orgId,
                        Sex = range.Sex,
                        AgeFromDays = range.AllAges ? 0 : StarterCatalog.AdultFromDays,
                        Low = range.Low,
                        High = range.High,
                        CriticalLow = range.CriticalLow,
                        CriticalHigh = range.CriticalHigh,
                        DisplayText = range.Text,
                        Source = StarterCatalog.RangeSource,
                        Status = RangeStatus.Draft,
                    });
                test.Parameters.Add(parameter);
            }
        }
    }

    public async Task<StarterCatalogResult> SeedAsync(int orgId, CancellationToken ct = default)
    {
        if (await db.BusinessSettings.AnyAsync(s => s.OrganizationId == orgId && s.BranchId == null && s.Key == StarterCatalog.MarkerKey, ct))
            return new StarterCatalogResult(true, 0, 0, 0, 0);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Code, u => u.Id, ct);
        var sampleTypes = await db.SampleTypes.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Id, ct);

        // Departments and categories: reuse the organisation's own by code, create the missing ones.
        var departments = await db.Departments.Where(d => d.OrganizationId == orgId).ToDictionaryAsync(d => d.Code, ct);
        var departmentNames = new Dictionary<string, (string En, string Ur)>
        {
            ["HAEM"] = ("Haematology", "ہیماٹولوجی"),
            ["CHEM"] = ("Clinical Chemistry", "کلینیکل کیمسٹری"),
            ["IMMU"] = ("Immunology & Serology", "امیونولوجی اور سیرولوجی"),
            ["CPATH"] = ("Clinical Pathology", "کلینیکل پیتھالوجی"),
        };
        foreach (var code in StarterCatalog.Tests.Select(t => t.Department).Distinct().Where(c => !departments.ContainsKey(c)))
        {
            var (en, ur) = departmentNames[code];
            var d = new Department { OrganizationId = orgId, Code = code, NameEn = en, NameUr = ur, SortOrder = 100 };
            db.Departments.Add(d);
            departments[code] = d;
        }

        var categories = await db.TestCategories.Where(c => c.OrganizationId == orgId).ToDictionaryAsync(c => c.Code, ct);
        var sort = categories.Count == 0 ? 0 : categories.Values.Max(c => c.SortOrder);
        foreach (var c in StarterCatalog.Categories.Where(c => !categories.ContainsKey(c.Code)))
        {
            var category = new TestCategory { OrganizationId = orgId, Code = c.Code, NameEn = c.En, NameUr = c.Ur, SortOrder = sort += 10 };
            db.TestCategories.Add(category);
            categories[c.Code] = category;
        }
        await db.SaveChangesAsync(ct);

        // IgnoreQueryFilters: a deleted test still owns its code. Scoped by OrganizationId.
        var usedCodes = (await db.Tests.IgnoreQueryFilters().Where(t => t.OrganizationId == orgId).Select(t => t.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var testSort = await db.Tests.IgnoreQueryFilters().Where(t => t.OrganizationId == orgId).MaxAsync(t => (int?)t.SortOrder, ct) ?? 0;

        int tests = 0, parameters = 0, ranges = 0, skipped = 0;
        foreach (var t in StarterCatalog.Tests)
        {
            if (usedCodes.Contains(t.Code)) { skipped++; continue; }
            var test = new LabTest
            {
                OrganizationId = orgId, Code = t.Code, NameEn = t.En, NameUr = t.Ur, ShortName = t.Short,
                CategoryId = categories[t.Category].Id, DepartmentId = departments[t.Department].Id, SampleTypeId = sampleTypes[t.SampleType],
                TurnaroundHours = t.TurnaroundHours, Price = 0, SortOrder = testSort += 10, AvailableAllBranches = true,
            };
            var pSort = 0;
            foreach (var p in t.Parameters)
            {
                var parameter = new TestParameter
                {
                    OrganizationId = orgId, Code = p.Code, NameEn = p.En, ResultType = p.Type,
                    UnitId = p.Unit is null ? null : units[p.Unit], DecimalPlaces = p.Decimals, GroupName = p.Group, SortOrder = pSort += 10,
                };
                var oSort = 0;
                foreach (var o in p.Options)
                    parameter.Options.Add(new ParameterOption { OrganizationId = orgId, ValueEn = o.En, ValueUr = o.Ur, IsAbnormal = o.Abnormal, SortOrder = oSort += 10 });
                foreach (var r in p.Ranges)
                {
                    parameter.Ranges.Add(new ReferenceRange
                    {
                        OrganizationId = orgId, Sex = r.Sex, AgeFromDays = r.AllAges ? 0 : StarterCatalog.AdultFromDays, AgeToDays = null,
                        Low = r.Low, High = r.High, CriticalLow = r.CriticalLow, CriticalHigh = r.CriticalHigh, DisplayText = r.Text,
                        Source = StarterCatalog.RangeSource, Status = RangeStatus.Draft,
                    });
                    ranges++;
                }
                test.Parameters.Add(parameter);
                parameters++;
            }
            db.Tests.Add(test);
            tests++;
        }

        db.BusinessSettings.Add(new BusinessSetting { OrganizationId = orgId, Key = StarterCatalog.MarkerKey, Value = StarterCatalog.Version });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new StarterCatalogResult(false, tests, parameters, ranges, skipped);
    }
}
