using System.Text.Json;
using Lims.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public sealed class AuditAndConcurrencyTests(DatabaseFixture fx)
{
    [Fact]
    public async Task Update_writes_audit_row_with_old_and_new_values_and_stamps()
    {
        const int userId = 4242;
        await using var db = fx.Create(fx.UserOfA(userId));
        var dept = await db.Departments.SingleAsync(d => d.Code == "MICRO");
        var created = dept.CreatedAtUtc;
        var oldName = dept.NameEn;

        dept.NameEn = "Microbiology & Parasitology";
        dept.CreatedAtUtc = DateTime.UtcNow.AddYears(-5); // callers must not be able to rewrite creation time
        await db.SaveChangesAsync();

        await using var check = fx.System();
        var saved = await check.Departments.SingleAsync(d => d.Id == dept.Id);
        Assert.Equal(created, saved.CreatedAtUtc);
        Assert.Equal(userId, saved.UpdatedByUserId);
        Assert.NotNull(saved.UpdatedAtUtc);

        var log = await check.AuditLogs
            .Where(a => a.EntityType == "Department" && a.EntityId == dept.Id.ToString() && a.Action == "Update")
            .OrderByDescending(a => a.Id).FirstAsync();
        Assert.Equal(userId, log.UserId);
        Assert.Equal(fx.OrgA, log.OrganizationId);

        using var json = JsonDocument.Parse(log.Changes!);
        var name = json.RootElement.GetProperty("NameEn");
        Assert.Equal(oldName, name.GetProperty("old").GetString());
        Assert.Equal("Microbiology & Parasitology", name.GetProperty("new").GetString());
        Assert.False(json.RootElement.TryGetProperty("CreatedAtUtc", out _));
    }

    [Fact]
    public async Task Insert_audit_has_generated_id_and_never_contains_password_hash()
    {
        await using var db = fx.Create(fx.UserOfA());
        var user = new User
        {
            Username = "audit-" + Guid.NewGuid().ToString("N")[..8], DisplayNameEn = "Audit test",
            PasswordHash = "SECRET-HASH-VALUE",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await using var check = fx.System();
        var log = await check.AuditLogs.SingleAsync(a => a.EntityType == "User" && a.EntityId == user.Id.ToString());
        Assert.Equal("Insert", log.Action);
        Assert.DoesNotContain("SECRET-HASH-VALUE", log.Changes);
        Assert.DoesNotContain(user.SecurityStamp, log.Changes);
        Assert.Contains("\"Username\"", log.Changes);
    }

    [Fact]
    public async Task Failed_save_writes_no_audit_rows()
    {
        await using var db = fx.Create(fx.UserOfA());
        var before = await db.AuditLogs.CountAsync();
        db.Branches.Add(new() { Code = "MAIN", NameEn = "Duplicate code" }); // violates unique index
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        await using var check = fx.System();
        Assert.Equal(before, await check.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Concurrent_edits_are_detected()
    {
        await using var first = fx.Create(fx.UserOfA());
        await using var second = fx.Create(fx.UserOfA());
        var a = await first.Branches.SingleAsync(b => b.Id == fx.OrgASecondBranch);
        var b = await second.Branches.SingleAsync(x => x.Id == fx.OrgASecondBranch);

        a.Phone = "042-111-111";
        await first.SaveChangesAsync();

        b.Phone = "042-222-222";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
