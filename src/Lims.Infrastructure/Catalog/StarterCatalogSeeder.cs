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
