using Lims.Application.Catalog;
using Lims.Application.Common;
using Lims.Domain.Catalog;
using Lims.Domain.Security;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Catalog;

/// <summary>Test catalog configuration (categories, tests, parameters, packages) for the caller's organisation.</summary>
public sealed class CatalogService(LimsDbContext db)
{
    private int OrgId => db.Request.OrganizationId ?? throw BusinessRuleException.NotFound("Organisation");

    // ---------- Reference lists ----------

    public Task<List<UnitDto>> UnitsAsync(CancellationToken ct) =>
        db.Units.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Code).Select(u => new UnitDto(u.Id, u.Code, u.Name)).ToListAsync(ct);

    public Task<List<SampleTypeDto>> SampleTypesAsync(CancellationToken ct) =>
        db.SampleTypes.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Id)
            .Select(s => new SampleTypeDto(s.Id, s.Code, s.NameEn, s.NameUr, s.Container, s.CapColour)).ToListAsync(ct);

    // ---------- Categories ----------

    public Task<List<CategoryDto>> CategoriesAsync(CancellationToken ct) =>
        db.TestCategories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn)
            .Select(c => new CategoryDto(c.Id, c.Code, c.NameEn, c.NameUr, c.SortOrder, c.IsActive,
                db.Tests.Count(t => t.CategoryId == c.Id), Convert.ToBase64String(c.RowVersion)))
            .ToListAsync(ct);

    public async Task<int> CreateCategoryAsync(CategoryInput input, CancellationToken ct)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.TestCategories.AnyAsync(c => c.Code == code, ct))
            throw BusinessRuleException.Conflict("category_code_taken", "A category with this code already exists.");
        var c = new TestCategory { OrganizationId = OrgId, Code = code, NameEn = input.NameEn.Trim(), NameUr = Blank(input.NameUr), SortOrder = input.SortOrder };
        db.TestCategories.Add(c);
        await db.SaveChangesAsync(ct);
        return c.Id;
    }

    public async Task UpdateCategoryAsync(int id, CategoryUpdate input, CancellationToken ct)
    {
        var c = await db.TestCategories.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Category");
        db.Entry(c).Property(x => x.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        c.NameEn = input.NameEn.Trim();
        c.NameUr = Blank(input.NameUr);
        c.SortOrder = input.SortOrder;
        c.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    // ---------- Tests ----------

    public async Task<PagedResult<TestListItem>> TestsAsync(TestQuery q, CancellationToken ct)
    {
        var query = db.Tests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(t => t.Code.Contains(s) || t.NameEn.Contains(s) || (t.NameUr != null && t.NameUr.Contains(s)) ||
                                     (t.ShortName != null && t.ShortName.Contains(s)));
        }
        if (q.CategoryId is int cat) query = query.Where(t => t.CategoryId == cat);
        if (q.DepartmentId is int dep) query = query.Where(t => t.DepartmentId == dep);
        if (q.IsActive is bool active) query = query.Where(t => t.IsActive == active);

        query = (q.Sort, q.Desc) switch
        {
            ("code", false) => query.OrderBy(t => t.Code),
            ("code", true) => query.OrderByDescending(t => t.Code),
            ("price", false) => query.OrderBy(t => t.Price),
            ("price", true) => query.OrderByDescending(t => t.Price),
            (_, true) => query.OrderByDescending(t => t.NameEn),
            _ => query.OrderBy(t => t.NameEn),
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip(q.Skip).Take(q.PageSize)
            .Select(t => new TestListItem(
                t.Id, t.Code, t.NameEn, t.NameUr, t.ShortName,
                db.TestCategories.Where(c => c.Id == t.CategoryId).Select(c => c.NameEn).First(),
                db.TestCategories.Where(c => c.Id == t.CategoryId).Select(c => c.NameUr).First(),
                db.Departments.Where(d => d.Id == t.DepartmentId).Select(d => d.NameEn).First(),
                db.Departments.Where(d => d.Id == t.DepartmentId).Select(d => d.NameUr).First(),
                db.SampleTypes.Where(s => s.Id == t.SampleTypeId).Select(s => s.NameEn).First(),
                db.SampleTypes.Where(s => s.Id == t.SampleTypeId).Select(s => s.NameUr).First(),
                t.Price, t.TurnaroundHours,
                t.Parameters.Count(p => p.IsActive),
                t.Parameters.Count(p => p.IsActive && p.Ranges.Any(r => r.Status == RangeStatus.Approved)),
                t.Parameters.SelectMany(p => p.Ranges).Count(r => r.Status == RangeStatus.Draft),
                t.IsActive))
            .ToListAsync(ct);
        return new PagedResult<TestListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<TestDetail> TestAsync(int id, CancellationToken ct)
    {
        var t = await db.Tests.AsNoTracking()
            .Include(x => x.Branches)
            .Include(x => x.Parameters).ThenInclude(p => p.Options)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Test");

        var paramIds = t.Parameters.Select(p => p.Id).ToList();
        var rangeCounts = await db.ReferenceRanges.AsNoTracking()
            .Where(r => paramIds.Contains(r.ParameterId) && r.Status != RangeStatus.Retired)
            .GroupBy(r => new { r.ParameterId, r.Status })
            .Select(g => new { g.Key.ParameterId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        var units = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Code, ct);

        int Count(int pid, RangeStatus s) => rangeCounts.Where(x => x.ParameterId == pid && x.Status == s).Sum(x => x.Count);

        var parameters = t.Parameters.OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new ParameterDto(p.Id, p.Code, p.NameEn, p.NameUr, p.ResultType, p.UnitId,
                p.UnitId is int u ? units.GetValueOrDefault(u) : null, p.DecimalPlaces, p.GroupName, p.SortOrder, p.IsActive,
                p.Options.OrderBy(o => o.SortOrder).Select(o => new OptionDto(o.ValueEn, o.ValueUr, o.IsAbnormal)).ToList(),
                Count(p.Id, RangeStatus.Approved), Count(p.Id, RangeStatus.Draft), Convert.ToBase64String(p.RowVersion)))
            .ToList();

        return new TestDetail(t.Id, t.Code, t.NameEn, t.NameUr, t.ShortName, t.CategoryId, t.DepartmentId, t.SampleTypeId,
            t.SampleVolume, t.CollectionNotes, t.Method, t.TurnaroundHours, t.Price, t.ReportNotes, t.SortOrder,
            t.AvailableAllBranches, t.Branches.Select(b => b.BranchId).ToList(), t.IsActive, parameters,
            Convert.ToBase64String(t.RowVersion));
    }

    public async Task<int> CreateTestAsync(TestInput input, CancellationToken ct)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.Tests.IgnoreQueryFilters().AnyAsync(t => t.OrganizationId == OrgId && t.Code == code, ct))
            throw BusinessRuleException.Conflict("test_code_taken", "A test with this code already exists.");
        if (input.Price != 0) RequirePricing();

        var test = new LabTest { OrganizationId = OrgId, Code = code, Price = input.Price };
        await ApplyAsync(test, input, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Tests.Add(test);
        await db.SaveChangesAsync(ct);
        await SetBranchesAsync(test, input, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return test.Id;
    }

    public async Task UpdateTestAsync(int id, TestUpdate input, CancellationToken ct)
    {
        var test = await db.Tests.Include(t => t.Branches).SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw BusinessRuleException.NotFound("Test");
        db.Entry(test).Property(t => t.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        if (input.Price != test.Price)
        {
            RequirePricing();
            test.Price = input.Price;
        }
        await ApplyAsync(test, input, ct);
        test.IsActive = input.IsActive;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SetBranchesAsync(test, input, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private void RequirePricing()
    {
        if (!db.Request.Can(Permissions.CatalogPricing))
            throw new BusinessRuleException("pricing_forbidden", "Changing prices requires the price management permission.", 403);
    }

    private async Task ApplyAsync(LabTest t, TestInput input, CancellationToken ct)
    {
        if (!await db.TestCategories.AnyAsync(c => c.Id == input.CategoryId, ct))
            throw new BusinessRuleException("category_invalid", "Choose a test category.");
        if (!await db.Departments.AnyAsync(d => d.Id == input.DepartmentId, ct))
            throw new BusinessRuleException("department_invalid", "Choose a department.");
        if (!await db.SampleTypes.AnyAsync(s => s.Id == input.SampleTypeId, ct))
            throw new BusinessRuleException("sample_type_invalid", "Choose a sample type.");

        t.NameEn = input.NameEn.Trim();
        t.NameUr = Blank(input.NameUr);
        t.ShortName = Blank(input.ShortName);
        t.CategoryId = input.CategoryId;
        t.DepartmentId = input.DepartmentId;
        t.SampleTypeId = input.SampleTypeId;
        t.SampleVolume = Blank(input.SampleVolume);
        t.CollectionNotes = Blank(input.CollectionNotes);
        t.Method = Blank(input.Method);
        t.TurnaroundHours = input.TurnaroundHours;
        t.ReportNotes = Blank(input.ReportNotes);
        t.SortOrder = input.SortOrder;
        t.AvailableAllBranches = input.AvailableAllBranches;
    }

    private async Task SetBranchesAsync(LabTest t, TestInput input, CancellationToken ct)
    {
        var wanted = input.AvailableAllBranches ? [] : input.BranchIds.Distinct().ToArray();
        if (!input.AvailableAllBranches && wanted.Length == 0)
            throw new BusinessRuleException("branch_required", "Choose at least one branch, or make the test available at all branches.");
        var valid = await db.Branches.CountAsync(b => wanted.Contains(b.Id), ct);
        if (valid != wanted.Length) throw new BusinessRuleException("branch_invalid", "One or more branches do not exist.");

        foreach (var b in t.Branches.Where(b => !wanted.Contains(b.BranchId)).ToList()) db.TestBranches.Remove(b);
        foreach (var id in wanted.Except(t.Branches.Select(b => b.BranchId)))
            db.TestBranches.Add(new TestBranch { OrganizationId = t.OrganizationId, TestId = t.Id, BranchId = id });
    }

    // ---------- Parameters ----------

    public async Task<int> AddParameterAsync(int testId, ParameterInput input, CancellationToken ct)
    {
        var test = await db.Tests.SingleOrDefaultAsync(t => t.Id == testId, ct) ?? throw BusinessRuleException.NotFound("Test");
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.TestParameters.AnyAsync(p => p.TestId == testId && p.Code == code, ct))
            throw BusinessRuleException.Conflict("parameter_code_taken", "This test already has a parameter with this code.");
        var p = new TestParameter { OrganizationId = test.OrganizationId, TestId = testId, Code = code };
        await ApplyAsync(p, input, ct);
        db.TestParameters.Add(p);
        await db.SaveChangesAsync(ct);
        return p.Id;
    }

    public async Task UpdateParameterAsync(int id, ParameterUpdate input, CancellationToken ct)
    {
        var p = await db.TestParameters.Include(x => x.Options).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Parameter");
        db.Entry(p).Property(x => x.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        if (input.ResultType != p.ResultType &&
            await db.ReferenceRanges.AnyAsync(r => r.ParameterId == id && r.Status != RangeStatus.Retired, ct))
            throw BusinessRuleException.Conflict("parameter_type_locked",
                "The result type can't change while the parameter has reference ranges. Retire them first.");
        await ApplyAsync(p, input, ct);
        p.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(TestParameter p, ParameterInput input, CancellationToken ct)
    {
        if (!Enum.IsDefined(input.ResultType)) throw new BusinessRuleException("result_type_invalid", "Choose a result type.");
        if (input.ResultType == ResultType.Numeric && input.UnitId is int u && !await db.Units.AnyAsync(x => x.Id == u, ct))
            throw new BusinessRuleException("unit_invalid", "Choose a unit.");

        p.NameEn = input.NameEn.Trim();
        p.NameUr = Blank(input.NameUr);
        p.ResultType = input.ResultType;
        p.UnitId = input.ResultType == ResultType.Numeric ? input.UnitId : null;
        p.DecimalPlaces = input.ResultType == ResultType.Numeric ? input.DecimalPlaces : 0;
        p.GroupName = Blank(input.GroupName);
        p.SortOrder = input.SortOrder;

        var options = input.ResultType == ResultType.Option
            ? input.Options.Where(o => !string.IsNullOrWhiteSpace(o.ValueEn)).ToList()
            : [];
        if (input.ResultType == ResultType.Option && options.Count < 2)
            throw new BusinessRuleException("options_required", "List at least two possible values.");
        if (options.Select(o => o.ValueEn.Trim().ToUpperInvariant()).Distinct().Count() != options.Count)
            throw new BusinessRuleException("options_duplicate", "Each value can appear only once.");

        var before = p.Options.OrderBy(o => o.SortOrder).Select(o => $"{o.ValueEn}{(o.IsAbnormal ? "*" : "")}").ToList();
        db.ParameterOptions.RemoveRange(p.Options);
        p.Options.Clear();
        var order = 0;
        foreach (var o in options)
            p.Options.Add(new ParameterOption { OrganizationId = p.OrganizationId, ValueEn = o.ValueEn.Trim(), ValueUr = Blank(o.ValueUr), IsAbnormal = o.IsAbnormal, SortOrder = ++order });
        var after = options.Select(o => $"{o.ValueEn.Trim()}{(o.IsAbnormal ? "*" : "")}").ToList();
        if (p.Id != 0 && !before.SequenceEqual(after))
            db.AddBusinessAudit("Parameter.OptionsChanged", nameof(TestParameter), p.Id, new { old = before, @new = after });
    }

    // ---------- Packages ----------

    public async Task<List<PackageDto>> PackagesAsync(CancellationToken ct)
    {
        var packages = await db.TestPackages.AsNoTracking()
            .Include(p => p.Tests).ThenInclude(pt => pt.Test)
            .OrderBy(p => p.NameEn).AsSplitQuery().ToListAsync(ct);
        return packages.Select(ToDto).ToList();
    }

    public async Task<int> CreatePackageAsync(PackageInput input, CancellationToken ct)
    {
        RequirePricing();
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.TestPackages.IgnoreQueryFilters().AnyAsync(p => p.OrganizationId == OrgId && p.Code == code, ct))
            throw BusinessRuleException.Conflict("package_code_taken", "A package with this code already exists.");
        var pkg = new TestPackage { OrganizationId = OrgId, Code = code };
        await ApplyAsync(pkg, input, ct);
        db.TestPackages.Add(pkg);
        await db.SaveChangesAsync(ct);
        return pkg.Id;
    }

    public async Task UpdatePackageAsync(int id, PackageUpdate input, CancellationToken ct)
    {
        RequirePricing();
        var pkg = await db.TestPackages.Include(p => p.Tests).SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw BusinessRuleException.NotFound("Package");
        db.Entry(pkg).Property(p => p.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        await ApplyAsync(pkg, input, ct);
        pkg.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(TestPackage pkg, PackageInput input, CancellationToken ct)
    {
        var ids = input.TestIds.Distinct().ToArray();
        if (ids.Length < 2) throw new BusinessRuleException("package_tests_required", "A package needs at least two tests.");
        if (await db.Tests.CountAsync(t => ids.Contains(t.Id) && t.IsActive, ct) != ids.Length)
            throw new BusinessRuleException("test_invalid", "One or more tests do not exist or are inactive.");

        pkg.NameEn = input.NameEn.Trim();
        pkg.NameUr = Blank(input.NameUr);
        pkg.Description = Blank(input.Description);
        pkg.Price = input.Price;

        var before = pkg.Tests.Select(t => t.TestId).Order().ToArray();
        foreach (var pt in pkg.Tests.Where(pt => !ids.Contains(pt.TestId)).ToList()) db.PackageTests.Remove(pt);
        foreach (var tid in ids.Except(before)) pkg.Tests.Add(new PackageTest { OrganizationId = pkg.OrganizationId, TestId = tid });
        if (pkg.Id != 0 && !before.SequenceEqual(ids.Order()))
            db.AddBusinessAudit("Package.TestsChanged", nameof(TestPackage), pkg.Id, new { old = before, @new = ids.Order() });
    }

    private static PackageDto ToDto(TestPackage p)
    {
        var tests = p.Tests.Where(t => t.Test is not null).Select(t => new PackageTestDto(t.TestId, t.Test!.Code, t.Test.NameEn, t.Test.NameUr, t.Test.Price))
            .OrderBy(t => t.NameEn).ToList();
        return new PackageDto(p.Id, p.Code, p.NameEn, p.NameUr, p.Description, p.Price, p.IsActive, tests, tests.Sum(t => t.Price),
            Convert.ToBase64String(p.RowVersion));
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
