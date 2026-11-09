using Lims.Application.Abstractions;
using Lims.Domain.Core;
using Lims.Domain.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public sealed class TenantIsolationTests(DatabaseFixture fx)
{
    [Fact]
    public async Task User_sees_only_their_organisations_rows()
    {
        await using var db = fx.Create(fx.UserOfA());
        var branches = await db.Branches.ToListAsync();
        Assert.NotEmpty(branches);
        Assert.All(branches, b => Assert.Equal(fx.OrgA, b.OrganizationId));
        Assert.All(await db.Roles.ToListAsync(), r => Assert.Equal(fx.OrgA, r.OrganizationId));
        Assert.All(await db.NumberSequences.ToListAsync(), s => Assert.Equal(fx.OrgA, s.OrganizationId));
    }

    [Fact]
    public async Task Another_organisations_row_cannot_be_read_by_id()
    {
        await using var db = fx.Create(fx.UserOfA());
        Assert.Null(await db.Branches.FirstOrDefaultAsync(b => b.Id == fx.OrgBMainBranch));
    }

    [Fact]
    public async Task Context_without_organisation_sees_nothing()
    {
        await using var db = fx.Create(new RequestContext()); // e.g. unauthenticated request
        Assert.Equal(0, await db.Branches.CountAsync());
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Roles.CountAsync());
        // Global reference data is still available.
        Assert.True(await db.Units.AnyAsync());
    }

    [Fact]
    public async Task New_rows_get_the_callers_organisation()
    {
        await using var db = fx.Create(fx.UserOfA());
        var dept = new Department { Code = "T" + Guid.NewGuid().ToString("N")[..6], NameEn = "Test dept" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        Assert.Equal(fx.OrgA, dept.OrganizationId);
    }

    [Fact]
    public async Task Writing_a_row_for_another_organisation_is_rejected()
    {
        await using var db = fx.Create(fx.UserOfA());
        db.Departments.Add(new Department { OrganizationId = fx.OrgB, Code = "HACK", NameEn = "Injected" });
        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Modifying_another_organisations_row_is_rejected()
    {
        Branch foreign;
        await using (var sys = fx.System())
            foreign = await sys.Branches.AsNoTracking().SingleAsync(b => b.Id == fx.OrgBMainBranch);

        await using var db = fx.Create(fx.UserOfA());
        foreign.NameEn = "Hijacked";
        db.Branches.Update(foreign);
        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Moving_a_keyed_row_to_another_organisation_is_rejected()
    {
        // OrganizationId is part of the (OrganizationId, Id) key, so EF refuses before the guard runs.
        await using var db = fx.Create(fx.UserOfA());
        var dept = await db.Departments.FirstAsync();
        dept.OrganizationId = fx.OrgB;
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Moving_any_row_to_another_organisation_is_rejected_by_the_guard()
    {
        await using var db = fx.Create(fx.UserOfA());
        var setting = await db.BusinessSettings.FirstAsync();
        setting.OrganizationId = fx.OrgB;
        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Saving_without_an_organisation_is_rejected()
    {
        await using var db = fx.Create(new RequestContext());
        db.Departments.Add(new Department { Code = "NOORG", NameEn = "No org" });
        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_rejects_linking_a_user_to_another_organisations_branch()
    {
        // Bypass EF entirely: the composite foreign key must stop this even for raw SQL.
        await using var db = fx.Create(fx.UserOfA());
        var user = new User { Username = "iso-" + Guid.NewGuid().ToString("N")[..8], DisplayNameEn = "Isolation", PasswordHash = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO security.UserBranches (OrganizationId, UserId, BranchId) VALUES ({0}, {1}, {2})",
            fx.OrgA, user.Id, fx.OrgBMainBranch));
        Assert.Contains("FK_UserBranches_Branches_OrganizationId_BranchId", ex.Message);

        // …and the same link inside the user's own organisation works.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO security.UserBranches (OrganizationId, UserId, BranchId) VALUES ({0}, {1}, {2})",
            fx.OrgA, user.Id, fx.OrgASecondBranch);
    }

    [Fact]
    public async Task Database_rejects_assigning_another_organisations_role()
    {
        int roleOfB;
        await using (var sys = fx.System())
            roleOfB = await sys.Roles.Where(r => r.OrganizationId == fx.OrgB).Select(r => r.Id).FirstAsync();

        await using var db = fx.Create(fx.UserOfA());
        var user = new User { Username = "role-" + Guid.NewGuid().ToString("N")[..8], DisplayNameEn = "Role test", PasswordHash = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // EF path: the composite FK uses the user's OrganizationId, so the database refuses B's role.
        user.Roles.Add(new UserRole { RoleId = roleOfB });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("FK_UserRoles_Roles_OrganizationId_RoleId", ex.InnerException!.Message);
    }
}
