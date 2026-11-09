using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class AdminApiTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;

    private async Task<ApiClient> SignedIn(int org, params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(org, roles)));
        return c;
    }

    private async Task<int> RoleId(int org, string code)
    {
        await using var db = fx.System();
        return await db.Roles.Where(r => r.OrganizationId == org && r.Code == code).Select(r => r.Id).SingleAsync();
    }

    [Fact]
    public async Task Users_without_permission_get_403()
    {
        var c = await SignedIn(fx.OrgA, "RECEPTION");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync("/api/organization/branches", new { code = "X1", nameEn = "X" })).StatusCode);
        // Departments are readable by any signed-in user.
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/organization/departments")).StatusCode);
    }

    [Fact]
    public async Task Admin_creates_user_who_can_then_sign_in_and_must_change_password()
    {
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var username = "new." + Guid.NewGuid().ToString("N")[..6];
        var res = await admin.PostAsync("/api/users", new
        {
            username = username.ToUpperInvariant(), // stored lower-case
            displayNameEn = "New Receptionist",
            displayNameUr = "نئی ریسپشنسٹ",
            password = "Initial-pass-1",
            allBranches = false,
            branchIds = new[] { fx.OrgASecondBranch },
            defaultBranchId = fx.OrgASecondBranch,
            roleIds = new[] { await RoleId(fx.OrgA, "RECEPTION") },
        });
        ApiClient.Ok(res, HttpStatusCode.Created);
        var created = await ApiClient.Body(res);
        Assert.Equal(username, created.GetProperty("username").GetString());
        Assert.True(created.GetProperty("mustChangePassword").GetBoolean());

        var user = await Api.ClientAsync();
        ApiClient.Ok(await user.LoginAsync(username, "Initial-pass-1"));
        var me = await ApiClient.Body(await user.GetAsync("/api/auth/me"));
        Assert.True(me.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal("نئی ریسپشنسٹ", me.GetProperty("displayNameUr").GetString());

        // Audit trail records who created the user.
        await using var db = fx.System();
        var id = created.GetProperty("id").GetInt32();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityType == "User" && a.EntityId == id.ToString() && a.Action == "Insert" && a.UserId != null));
    }

    [Fact]
    public async Task Duplicate_username_and_invalid_input_return_coded_errors()
    {
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var existing = await Api.CreateUserAsync(fx.OrgB, ["CASHIER"]); // usernames are unique across organisations
        var role = await RoleId(fx.OrgA, "CASHIER");

        var dup = await admin.PostAsync("/api/users", new { username = existing, displayNameEn = "X", password = "Valid-pass-1", allBranches = true, roleIds = new[] { role } });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("username_taken", await ApiClient.Code(dup));

        var noRole = await admin.PostAsync("/api/users", new { username = "norole" + Guid.NewGuid().ToString("N")[..4], displayNameEn = "X", password = "Valid-pass-1", allBranches = true, roleIds = Array.Empty<int>() });
        Assert.Equal("role_required", await ApiClient.Code(noRole));

        var badName = await admin.PostAsync("/api/users", new { username = "a b", displayNameEn = "X", password = "Valid-pass-1", allBranches = true, roleIds = new[] { role } });
        Assert.Equal("username_invalid", await ApiClient.Code(badName));
    }

    [Fact]
    public async Task Admin_cannot_see_or_use_another_organisations_users_roles_or_branches()
    {
        var adminA = await SignedIn(fx.OrgA, "SYSADMIN");
        var userOfB = await Api.CreateUserAsync(fx.OrgB, ["CASHIER"]);
        int idOfB;
        await using (var db = fx.System())
            idOfB = await db.Users.Where(u => u.Username == userOfB).Select(u => u.Id).SingleAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await adminA.GetAsync($"/api/users/{idOfB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminA.PostAsync($"/api/users/{idOfB}/reset-password", new { newPassword = "Hijack-pass-1" })).StatusCode);

        var list = await ApiClient.Body(await adminA.GetAsync("/api/users?pageSize=100"));
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), u => u.GetProperty("username").GetString() == userOfB);

        var withForeignRole = await adminA.PostAsync("/api/users", new
        {
            username = "x" + Guid.NewGuid().ToString("N")[..8], displayNameEn = "X", password = "Valid-pass-1", allBranches = true,
            roleIds = new[] { await RoleId(fx.OrgB, "SYSADMIN") },
        });
        Assert.Equal("role_invalid", await ApiClient.Code(withForeignRole));

        var withForeignBranch = await adminA.PostAsync("/api/users", new
        {
            username = "y" + Guid.NewGuid().ToString("N")[..8], displayNameEn = "Y", password = "Valid-pass-1", allBranches = false,
            branchIds = new[] { fx.OrgBMainBranch }, roleIds = new[] { await RoleId(fx.OrgA, "CASHIER") },
        });
        Assert.Equal("branch_invalid", await ApiClient.Code(withForeignBranch));
    }

    [Fact]
    public async Task Role_permission_change_applies_to_signed_in_users_and_is_audited()
    {
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var create = await admin.PostAsync("/api/roles", new { code = "R" + Guid.NewGuid().ToString("N")[..6], nameEn = "Temp role", permissions = new[] { "dashboard.view" } });
        ApiClient.Ok(create);
        var roleId = (await ApiClient.Body(create)).GetProperty("id").GetInt32();

        await using (var db = fx.System())
        {
            var code = await db.Roles.Where(r => r.Id == roleId).Select(r => r.Code).SingleAsync();
            var member = await Api.ClientAsync();
            ApiClient.Ok(await member.LoginAsync(await Api.CreateUserAsync(fx.OrgA, [code])));
            Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/users")).StatusCode);

            var roles = await ApiClient.Body(await admin.GetAsync("/api/roles"));
            var row = roles.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == roleId);
            ApiClient.Ok(await admin.PutAsync($"/api/roles/{roleId}", new
            {
                nameEn = "Temp role", permissions = new[] { "dashboard.view", "users.manage" }, isActive = true,
                rowVersion = row.GetProperty("rowVersion").GetString(),
            }), HttpStatusCode.NoContent);

            // Same cookie, new permission: sessions refresh from the database.
            Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/users")).StatusCode);
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Role.PermissionsChanged" && a.EntityId == roleId.ToString()));
        }
    }

    [Fact]
    public async Task Stale_row_version_is_rejected_as_a_conflict()
    {
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var branches = await ApiClient.Body(await admin.GetAsync("/api/organization/branches"));
        var b = branches.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == fx.OrgASecondBranch);
        var version = b.GetProperty("rowVersion").GetString();

        ApiClient.Ok(await admin.PutAsync($"/api/organization/branches/{fx.OrgASecondBranch}", new { nameEn = "Collection Centre 1", phone = "111", isActive = true, rowVersion = version }), HttpStatusCode.NoContent);
        var stale = await admin.PutAsync($"/api/organization/branches/{fx.OrgASecondBranch}", new { nameEn = "Collection Centre 1", phone = "222", isActive = true, rowVersion = version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency", await ApiClient.Code(stale));
    }

    [Fact]
    public async Task Main_branch_cannot_be_deactivated()
    {
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var branches = await ApiClient.Body(await admin.GetAsync("/api/organization/branches"));
        var main = branches.EnumerateArray().Single(x => x.GetProperty("isMain").GetBoolean());
        var res = await admin.PutAsync($"/api/organization/branches/{main.GetProperty("id").GetInt32()}",
            new { nameEn = main.GetProperty("nameEn").GetString(), isActive = false, rowVersion = main.GetProperty("rowVersion").GetString() });
        Assert.Equal("main_branch_required", await ApiClient.Code(res));
    }

    [Fact]
    public async Task Organisation_cannot_remove_its_last_administrator()
    {
        // A fresh organisation whose only administrator tries to remove their own admin rights.
        int org;
        await using (var db = fx.System())
            org = await new Lims.Infrastructure.Services.OrganizationProvisioner(db)
                .ProvisionAsync(new("SOLO" + Guid.NewGuid().ToString("N")[..4], "Solo lab", null, "MAIN", "Main", null));

        var username = await Api.CreateUserAsync(org, ["SYSADMIN"]);
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(username));

        var me = await ApiClient.Body(await c.GetAsync("/api/auth/me"));
        var id = me.GetProperty("userId").GetInt32();
        var detail = await ApiClient.Body(await c.GetAsync($"/api/users/{id}"));
        var res = await c.PutAsync($"/api/users/{id}", new
        {
            displayNameEn = "Solo admin", allBranches = true, isActive = true,
            roleIds = new[] { await RoleId(org, "CASHIER") },
            rowVersion = detail.GetProperty("rowVersion").GetString(),
        });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("last_administrator", await ApiClient.Code(res));

        // Nothing was saved: the user is still an administrator.
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/users")).StatusCode);
    }
}
