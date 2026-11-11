using System.Net;
using System.Reflection;
using Lims.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

namespace Lims.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class SecurityHardeningTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;

    /// <summary>
    /// Actions that need only a signed-in user, with no particular permission (everyone may see or change their own
    /// session). Adding to this list is a deliberate security decision; a new action that is missing from here and has
    /// no [HasPermission] or [AllowAnonymous] fails the test below.
    /// </summary>
    private static readonly HashSet<string> SignedInOnly =
    [
        "AuthController.Me", "AuthController.ChangePassword", "AuthController.ActiveBranch", "AuthController.Preferences",
        "OrganizationController.Departments", // read-only lookup list used by dropdowns for any signed-in user
    ];

    [Fact]
    public void Every_controller_action_declares_its_access()
    {
        var offenders = new List<string>();
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var type in controllers)
        {
            var classAnon = type.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            var classPerm = type.GetCustomAttributes<AuthorizeAttribute>(true).Any();
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (m.GetCustomAttributes<HttpMethodAttribute>().Any() is false) continue; // not an action
                var declared = classAnon || classPerm
                    || m.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                    || m.GetCustomAttributes<AuthorizeAttribute>(true).Any(a => a is HasPermissionAttribute);
                if (!declared && !SignedInOnly.Contains($"{type.Name}.{m.Name}"))
                    offenders.Add($"{type.Name}.{m.Name}");
            }
        }
        Assert.True(offenders.Count == 0, "Actions without [HasPermission] or [AllowAnonymous]: " + string.Join(", ", offenders));
    }

    [Fact]
    public async Task Api_responses_carry_security_headers_and_are_not_cacheable()
    {
        var c = await Api.ClientAsync();
        var res = await c.GetAsync("/api/health");
        Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        var csp = res.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
        Assert.DoesNotContain("http:", csp);
        Assert.Contains("camera=()", res.Headers.GetValues("Permissions-Policy").Single());
    }

    [Fact]
    public async Task Without_https_setting_there_is_no_redirect_or_hsts()
    {
        var res = await (await Api.ClientAsync()).GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.False(res.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task With_https_setting_http_is_redirected_and_cookie_is_secure()
    {
        using var https = Api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Security:RequireHttps", "true");
            b.UseSetting("HTTPS_PORT", "443");
            b.UseSetting("Auth:RequireHttpsCookie", "true");
        });
        var username = await Api.CreateUserAsync(fx.OrgA, ["RECEPTION"]);

        var plain = https.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var redirect = await plain.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, redirect.StatusCode);
        Assert.StartsWith("https://", redirect.Headers.Location?.ToString());

        var secure = https.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), HandleCookies = true, AllowAutoRedirect = false,
        });
        var client = await ApiClient.CreateAsync(secure);
        var login = await client.LoginAsync(username);
        ApiClient.Ok(login);
        Assert.Contains("secure", login.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("lims.session=")),
            StringComparison.OrdinalIgnoreCase);
        Assert.True(login.Headers.Contains("Strict-Transport-Security") ||
                    (await client.GetAsync("/api/health")).Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Application_login_cannot_change_audit_log_stock_ledger_or_schema()
    {
        if (fx.AppConnectionString == fx.ConnectionString) return; // LIMS_TEST_ADMIN_API=1: not applicable
        await using var conn = new Microsoft.Data.SqlClient.SqlConnection(fx.AppConnectionString);
        await conn.OpenAsync();

        async Task<int> Run(string sql)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            return await cmd.ExecuteNonQueryAsync();
        }
        async Task Denied(string sql)
        {
            var ex = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => Run(sql));
            Assert.Contains(ex.Number, new[] { 229, 262, 1088, 3701 }); // permission denied (data, create, alter, drop)
        }

        await Denied("UPDATE audit.AuditLogs SET Action = Action WHERE 1 = 0");
        await Denied("DELETE FROM audit.AuditLogs WHERE 1 = 0");
        await Denied("UPDATE inventory.StockMovements SET Reason = Reason WHERE 1 = 0");
        await Denied("DELETE FROM inventory.StockMovements WHERE 1 = 0");
        await Denied("DELETE FROM core.__EFMigrationsHistory WHERE 1 = 0");
        await Denied("CREATE TABLE core.ShouldNotExist (Id int)");
        await Denied("DROP TABLE audit.AuditLogs");
        await Denied("ALTER TABLE patient.Patients ADD Hacked int NULL");

        // Normal work is still allowed.
        Assert.Equal(0, await Run("DELETE FROM patient.Doctors WHERE 1 = 0"));
    }

    [Fact]
    public async Task Data_protection_keys_are_encrypted_when_dpapi_is_enabled()
    {
        if (!OperatingSystem.IsWindows()) return;
        var keys = Path.Combine(Path.GetTempPath(), "lims-test-keys-dpapi-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var f = Api.WithWebHostBuilder(b =>
            {
                b.UseSetting("DataProtection:KeysPath", keys);
                b.UseSetting("DataProtection:ProtectWithDpapi", "true");
            });
            var username = await Api.CreateUserAsync(fx.OrgA, ["RECEPTION"]);
            var client = await ApiClient.CreateAsync(f.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }));
            ApiClient.Ok(await client.LoginAsync(username));
            ApiClient.Ok(await client.GetAsync("/api/auth/me"));

            var file = Directory.GetFiles(keys, "key-*.xml").Single();
            var xml = await File.ReadAllTextAsync(file);
            Assert.Contains("encryptedKey", xml);
            Assert.DoesNotContain("masterKey", xml);
        }
        finally { if (Directory.Exists(keys)) Directory.Delete(keys, true); }
    }

    [Fact]
    public async Task General_rate_limit_returns_429_after_the_limit()
    {
        using var limited = Api.WithWebHostBuilder(b => b.UseSetting("RateLimit:RequestsPerMinute", "5"));
        var http = limited.CreateClient();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 8; i++) codes.Add((await http.GetAsync("/api/health")).StatusCode);
        Assert.Equal(5, codes.Count(c => c == HttpStatusCode.OK));
        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.TooManyRequests));
    }
}
