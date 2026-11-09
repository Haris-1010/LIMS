using Lims.Application.Common;
using Lims.Application.Organization;
using Lims.Domain.Core;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Organizations;

/// <summary>Organisation profile, branches and departments for the caller's organisation.</summary>
public sealed class OrganizationService(LimsDbContext db)
{
    private int OrgId => db.Request.OrganizationId ?? throw BusinessRuleException.NotFound("Organisation");

    public async Task<OrganizationDto> GetAsync(CancellationToken ct) =>
        await db.Organizations.AsNoTracking().Where(o => o.Id == OrgId)
            .Select(o => new OrganizationDto(o.Id, o.Code, o.NameEn, o.NameUr, o.Phone, o.Email, o.AddressEn, o.AddressUr,
                Convert.ToBase64String(o.RowVersion)))
            .SingleAsync(ct);

    public async Task UpdateAsync(OrganizationUpdate input, CancellationToken ct)
    {
        var org = await db.Organizations.SingleAsync(o => o.Id == OrgId, ct);
        db.Entry(org).Property(o => o.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        org.NameEn = input.NameEn.Trim();
        org.NameUr = Blank(input.NameUr);
        org.Phone = Blank(input.Phone);
        org.Email = Blank(input.Email);
        org.AddressEn = Blank(input.AddressEn);
        org.AddressUr = Blank(input.AddressUr);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Branches ----------

    public Task<List<BranchDto>> ListBranchesAsync(CancellationToken ct) =>
        db.Branches.AsNoTracking()
            .OrderByDescending(b => b.IsMain).ThenBy(b => b.NameEn)
            .Select(b => new BranchDto(b.Id, b.Code, b.NameEn, b.NameUr, b.Phone, b.Email, b.AddressEn, b.AddressUr,
                b.IsMain, b.IsActive,
                db.UserBranches.Count(ub => ub.BranchId == b.Id && ub.User!.IsActive),
                Convert.ToBase64String(b.RowVersion)))
            .ToListAsync(ct);

    public async Task<int> CreateBranchAsync(BranchCreate input, CancellationToken ct)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.Branches.AnyAsync(b => b.Code == code, ct))
            throw BusinessRuleException.Conflict("branch_code_taken", "A branch with this code already exists.");
        var branch = new Branch { OrganizationId = OrgId, Code = code };
        Apply(branch, input);
        db.Branches.Add(branch);
        await db.SaveChangesAsync(ct);
        return branch.Id;
    }

    public async Task UpdateBranchAsync(int id, BranchUpdate input, CancellationToken ct)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(b => b.Id == id, ct) ?? throw BusinessRuleException.NotFound("Branch");
        db.Entry(branch).Property(b => b.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        if (branch.IsMain && !input.IsActive)
            throw new BusinessRuleException("main_branch_required", "The main laboratory cannot be deactivated.");
        Apply(branch, input);
        branch.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Branch b, BranchEdit input)
    {
        b.NameEn = input.NameEn.Trim();
        b.NameUr = Blank(input.NameUr);
        b.Phone = Blank(input.Phone);
        b.Email = Blank(input.Email);
        b.AddressEn = Blank(input.AddressEn);
        b.AddressUr = Blank(input.AddressUr);
    }

    // ---------- Departments ----------

    public Task<List<DepartmentDto>> ListDepartmentsAsync(CancellationToken ct) =>
        db.Departments.AsNoTracking().OrderBy(d => d.SortOrder).ThenBy(d => d.NameEn)
            .Select(d => new DepartmentDto(d.Id, d.Code, d.NameEn, d.NameUr, d.SortOrder, d.IsActive, Convert.ToBase64String(d.RowVersion)))
            .ToListAsync(ct);

    public async Task<int> CreateDepartmentAsync(DepartmentCreate input, CancellationToken ct)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.Departments.AnyAsync(d => d.Code == code, ct))
            throw BusinessRuleException.Conflict("department_code_taken", "A department with this code already exists.");
        var dept = new Department { OrganizationId = OrgId, Code = code, NameEn = input.NameEn.Trim(), NameUr = Blank(input.NameUr), SortOrder = input.SortOrder };
        db.Departments.Add(dept);
        await db.SaveChangesAsync(ct);
        return dept.Id;
    }

    public async Task UpdateDepartmentAsync(int id, DepartmentUpdate input, CancellationToken ct)
    {
        var dept = await db.Departments.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw BusinessRuleException.NotFound("Department");
        db.Entry(dept).Property(d => d.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        dept.NameEn = input.NameEn.Trim();
        dept.NameUr = Blank(input.NameUr);
        dept.SortOrder = input.SortOrder;
        dept.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
