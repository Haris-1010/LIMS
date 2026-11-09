using System.Net;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class AuthApiTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;

    [Fact]
    public async Task Unauthenticated_requests_get_401_and_health_is_public()
    {
        var c = await Api.ClientAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public async Task Login_sets_secure_cookie_and_me_returns_session()
    {
        var username = await Api.CreateUserAsync(fx.OrgA, ["RECEPTION"]);
        var c = await Api.ClientAsync();
        var res = await c.LoginAsync(username);
        ApiClient.Ok(res);

        var cookie = res.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("lims.session="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var me = await ApiClient.Body(await c.GetAsync("/api/auth/me"));
        Assert.Equal(username, me.GetProperty("username").GetString());
        Assert.Equal(fx.OrgA, me.GetProperty("organizationId").GetInt32());
        var perms = me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains("patients.create", perms);
        Assert.DoesNotContain("users.manage", perms);
        Assert.False(me.TryGetProperty("securityStamp", out _));
    }

    [Fact]
    public async Task Wrong_password_and_unknown_user_give_the_same_answer()
    {
        var username = await Api.CreateUserAsync(fx.OrgA, ["CASHIER"]);
        var c = await Api.ClientAsync();
        var wrong = await c.LoginAsync(username, "Wrong-pass-123");
        var unknown = await c.LoginAsync("nobody-" + Guid.NewGuid().ToString("N")[..6], "Wrong-pass-123");

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal("invalid_credentials", await ApiClient.Code(wrong));
        Assert.Equal("invalid_credentials", await ApiClient.Code(unknown));
    }

    [Fact]
    public async Task Five_failures_lock_the_account_even_for_the_right_password()
    {
        var username = await Api.CreateUserAsync(fx.OrgA, ["CASHIER"]);
        var c = await Api.ClientAsync();
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.LoginAsync(username, "Wrong-pass-123")).StatusCode);

        var fifth = await c.LoginAsync(username, "Wrong-pass-123");
        Assert.Equal((HttpStatusCode)423, fifth.StatusCode);
        Assert.Equal("locked_out", await ApiClient.Code(fifth));

        var correct = await c.LoginAsync(username);
        Assert.Equal((HttpStatusCode)423, correct.StatusCode);

        await using var db = fx.System();
        var history = await db.LoginHistory.Where(h => h.UsernameAttempted == username).ToListAsync();
        Assert.Equal(6, history.Count);
        Assert.All(history, h => Assert.False(h.Succeeded));
    }

    [Fact]
    public async Task State_changing_requests_without_csrf_token_are_rejected()
    {
        var username = await Api.CreateUserAsync(fx.OrgA, ["SYSADMIN"]);
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(username));

        var res = await c.PostAsync("/api/auth/change-password",
            new { currentPassword = ApiFactory.Password, newPassword = "Another-pass-99" }, withCsrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Changing_password_ends_other_sessions_but_keeps_this_one()
    {
        var username = await Api.CreateUserAsync(fx.OrgA, ["TECHNICIAN"]);
        var first = await Api.ClientAsync();
        var second = await Api.ClientAsync();
        ApiClient.Ok(await first.LoginAsync(username));
        ApiClient.Ok(await second.LoginAsync(username));

        ApiClient.Ok(await first.PostAsync("/api/auth/change-password",
            new { currentPassword = ApiFactory.Password, newPassword = "Brand-new-pass-7" }));

        ApiClient.Ok(await first.GetAsync("/api/auth/me"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/auth/me")).StatusCode);

        // Old password no longer works; new one does.
        var fresh = await Api.ClientAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.LoginAsync(username)).StatusCode);
        ApiClient.Ok(await fresh.LoginAsync(username, "Brand-new-pass-7"));
    }

    [Fact]
    public async Task Weak_new_password_is_rejected_with_code()
    {
        var username = await Api.CreateUserAsync(fx.OrgA, ["TECHNICIAN"]);
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(username));
        var res = await c.PostAsync("/api/auth/change-password", new { currentPassword = ApiFactory.Password, newPassword = "allletters" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("password_needs_letter_and_digit", await ApiClient.Code(res));
    }

    [Fact]
    public async Task Deactivated_user_is_signed_out()
    {
        var username = await Api.CreateUserAsync(fx.OrgA, ["CASHIER"]);
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(username));

        await using (var db = fx.System())
            await db.Users.Where(u => u.Username == username).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Api.ClientAsync()).LoginAsync(username)).StatusCode);
    }

    [Fact]
    public async Task Branch_switch_is_limited_to_permitted_branches()
    {
        var username = await Api.CreateUserAsync(fx.OrgA, ["RECEPTION"], allBranches: false, branchIds: [fx.OrgASecondBranch]);
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(username));

        var me = await ApiClient.Body(await c.GetAsync("/api/auth/me"));
        Assert.Equal(fx.OrgASecondBranch, me.GetProperty("activeBranchId").GetInt32());
        Assert.Single(me.GetProperty("branches").EnumerateArray());

        var other = await c.PostAsync("/api/auth/active-branch", new { branchId = fx.OrgAMainBranch });
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        var all = await c.PostAsync("/api/auth/active-branch", new { branchId = (int?)null });
        Assert.Equal(HttpStatusCode.Forbidden, all.StatusCode);
    }
}
