using Lims.Domain.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public sealed class SchemaAndSeedTests(DatabaseFixture fx)
{
    [Fact]
    public async Task Fresh_database_has_all_migrations_applied()
    {
        await using var db = fx.System();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Reference_data_is_seeded_and_Urdu_text_round_trips()
    {
        await using var db = fx.System();
        Assert.Equal(Permissions.All.Count, await db.Permissions.CountAsync());
        Assert.Equal(42, await db.Units.CountAsync(u => u.Id < 1000));
        Assert.Equal(16, await db.SampleTypes.CountAsync(s => s.Id < 1000));
        Assert.Equal("خون (EDTA)", (await db.SampleTypes.SingleAsync(s => s.Code == "WB-EDTA")).NameUr);
        Assert.Equal("µmol/L", (await db.Units.SingleAsync(u => u.Id == 11)).Code);
    }

    [Fact]
    public async Task Administrator_added_units_start_after_reserved_ids()
    {
        await using var db = fx.System();
        var unit = new Lims.Domain.Catalog.Unit { Code = "test-unit-" + Guid.NewGuid().ToString("N")[..6], Name = "Test" };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        Assert.True(unit.Id >= 1000);
    }

    [Fact]
    public async Task Provisioning_creates_a_complete_organisation()
    {
        // Organisation B: other tests add rows to A, B is left as provisioned.
        await using var db = fx.Create(fx.UserOfB());
        Assert.Equal(1, await db.Branches.CountAsync(b => b.IsMain));
        Assert.Equal(6, await db.Departments.CountAsync());
        Assert.Equal(DefaultRoles.All.Count, await db.Roles.CountAsync());
        Assert.Equal(Lims.Domain.Core.SequenceTypes.All.Length, await db.NumberSequences.CountAsync());
        Assert.Equal("PKR", (await db.BusinessSettings.SingleAsync(s => s.Key == "Currency")).Value);

        var admin = await db.Roles.Include(r => r.Permissions).SingleAsync(r => r.Code == "SYSADMIN");
        Assert.Equal(Permissions.All.Count, admin.Permissions.Count);
    }

    [Fact]
    public async Task Branch_code_is_unique_within_an_organisation()
    {
        await using var db = fx.Create(fx.UserOfA());
        db.Branches.Add(new() { Code = "MAIN", NameEn = "Duplicate" });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("IX_Branches_OrganizationId_Code", ex.InnerException!.Message);
    }

    [Fact]
    public async Task Only_one_main_branch_per_organisation()
    {
        await using var db = fx.Create(fx.UserOfA());
        db.Branches.Add(new() { Code = "MAIN2", NameEn = "Second main", IsMain = true });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("UX_Branches_OneMainPerOrganization", ex.InnerException!.Message);
    }

    [Fact]
    public async Task Check_constraint_rejects_invalid_sequence_padding()
    {
        await using var db = fx.System();
        var ex = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE core.NumberSequences SET Padding = 0 WHERE OrganizationId = {0}", fx.OrgA));
        Assert.Contains("CK_NumberSequences_Padding", ex.Message);
    }
}
