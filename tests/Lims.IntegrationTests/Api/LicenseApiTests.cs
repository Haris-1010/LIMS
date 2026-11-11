using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lims.Api.Licensing;
using Lims.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lims.IntegrationTests.Api;

/// <summary>The real API with the licence check switched on, a test signing key and an empty licence folder of its own.</summary>
public sealed class LicensedApiFactory(DatabaseFixture fx) : WebApplicationFactory<Program>
{
    public static readonly RSA Signer = RSA.Create(3072);
    public static readonly RSA Stranger = RSA.Create(3072);
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "lims-license-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Lims", fx.AppConnectionString);
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetTempPath(), "lims-test-keys"));
        builder.UseSetting("Backup:Automatic", "false");
        builder.UseSetting("Backup:DataFolder", Path.Combine(Path.GetTempPath(), "lims-test-data-" + Guid.NewGuid().ToString("N")));
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<LicenseSettings>();
            s.AddSingleton(new LicenseSettings
            {
                Enforce = true,
                PublicKeyXml = Signer.ToXmlString(false),
                LicenseDirectory = Folder,
                ContentRoot = AppContext.BaseDirectory,
                CacheFor = TimeSpan.Zero,
            });
        });
    }

    public async Task<ApiClient> ClientAsync() =>
        await ApiClient.CreateAsync(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false }));

    /// <summary>A licence file exactly as the HART licence generator writes it.</summary>
    public static byte[] License(string installationId, string product = LicenseService.ProductName, string? expires = null, RSA? key = null,
        string customer = "Test Laboratory", Func<string, string>? tamper = null)
    {
        var payload = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            product,
            licenseId = Guid.NewGuid().ToString("D"),
            customerName = customer,
            installationId = installationId.ToLowerInvariant(),
            issuedUtc = DateTime.UtcNow.ToString("o"),
            expiresOnUtc = expires,
        });
        var signed = Encoding.UTF8.GetBytes(payload);
        var signature = (key ?? Signer).SignData(signed, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var sent = tamper is null ? signed : Encoding.UTF8.GetBytes(tamper(payload));
        return Encoding.UTF8.GetBytes($"{{\"payload\":\"{Convert.ToBase64String(sent)}\",\"signature\":\"{Convert.ToBase64String(signature)}\"}}\r\n");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (Directory.Exists(Folder)) Directory.Delete(Folder, true); } catch (IOException) { }
    }
}

[Collection(DatabaseCollection.Name)]
public sealed class LicenseApiTests(DatabaseFixture fx)
{
    private static async Task<HttpResponseMessage> Install(ApiClient c, byte[] bytes, bool withCsrf = true)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(file, "file", "license.json");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/license/install") { Content = form };
        if (withCsrf)
            req.Headers.Add("X-XSRF-TOKEN", (await ApiClient.Body(await c.GetAsync("/api/auth/csrf"))).GetProperty("token").GetString());
        return await c.Http.SendAsync(req);
    }

    private static async Task<string?> Reason(HttpResponseMessage res) =>
        (await ApiClient.Body(res)).TryGetProperty("reason", out var r) ? r.GetString() : null;

    [Fact]
    public async Task Without_a_license_every_api_call_is_refused_but_activation_works()
    {
        using var api = new LicensedApiFactory(fx);
        var c = await api.ClientAsync();   // the anti-forgery token is open without a licence

        var status = await ApiClient.Body(await c.GetAsync("/api/license"));
        Assert.False(status.GetProperty("isValid").GetBoolean());
        Assert.Equal("missing", status.GetProperty("reason").GetString());
        var id = status.GetProperty("installationId").GetString()!;
        Assert.Matches("^[0-9A-F]{64}$", id);

        // Signing in and every other API call: 403 license_required (the screens show activation instead).
        var me = await c.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Forbidden, me.StatusCode);
        Assert.Equal(LicenseEnforcementMiddleware.LicenseRequiredCode, await ApiClient.Code(me));
        var login = await c.LoginAsync("anyone", "whatever-1");
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("license_required", await ApiClient.Code(login));
        Assert.Equal("license_required", await ApiClient.Code(await c.GetAsync("/api/patients")));
        // Open: the health check, and the web app itself (it must load to show the activation screen).
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/health")).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await c.GetAsync("/orders/new")).StatusCode);
        Assert.False(Directory.Exists(api.Folder) && File.Exists(Path.Combine(api.Folder, "license.json")));

        // Installing needs the anti-forgery header like every other change.
        Assert.Equal("csrf_invalid", await ApiClient.Code(await Install(c, LicensedApiFactory.License(id), withCsrf: false)));

        var ok = await Install(c, LicensedApiFactory.License(id, customer: "City Diagnostic Lab"));
        ApiClient.Ok(ok);
        var installed = await ApiClient.Body(ok);
        Assert.True(installed.GetProperty("isValid").GetBoolean());
        Assert.Equal("City Diagnostic Lab", installed.GetProperty("customerName").GetString());
        Assert.Equal(JsonValueKind.Null, installed.GetProperty("expiresOnUtc").ValueKind);   // perpetual
        Assert.True(File.Exists(Path.Combine(api.Folder, "license.json")));
        Assert.Empty(Directory.GetFiles(api.Folder, "*.tmp"));

        // Now the API works normally: not signed in is 401, no longer 403.
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        var user = await fx.Api.CreateUserAsync(fx.OrgA, ["RECEPTION"]);
        ApiClient.Ok(await c.LoginAsync(user));
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Only_a_genuine_unexpired_HART_LIMS_license_for_this_server_is_accepted()
    {
        using var api = new LicensedApiFactory(fx);
        var c = await api.ClientAsync();
        var id = (await ApiClient.Body(await c.GetAsync("/api/license"))).GetProperty("installationId").GetString()!;
        var otherServer = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("another server")));
        var yesterday = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd");

        async Task Refused(byte[] file, string reason)
        {
            var res = await Install(c, file);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Equal("license_invalid", await ApiClient.Code(res));
            Assert.Equal(reason, await Reason(res));
        }

        await Refused(LicensedApiFactory.License(id, key: LicensedApiFactory.Stranger), "signature");           // someone else's key
        await Refused(LicensedApiFactory.License(id, tamper: p => p.Replace("Test Laboratory", "Other Lab")), "signature");   // edited after signing
        await Refused(LicensedApiFactory.License(id, product: "HART POS"), "product");                           // a POS licence
        await Refused(LicensedApiFactory.License(otherServer), "server");                                        // another server
        await Refused(LicensedApiFactory.License(id, expires: yesterday), "expired");
        await Refused(Encoding.UTF8.GetBytes("{\"payload\":\"not base64!\",\"signature\":\"x\"}"), "format");
        await Refused(Encoding.UTF8.GetBytes("<license/>"), "format");
        await Refused(new byte[17 * 1024], "too_large");
        Assert.False(File.Exists(Path.Combine(api.Folder, "license.json")));   // nothing refused was saved

        // Valid through the whole expiry day (UTC).
        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        var res = await Install(c, LicensedApiFactory.License(id, expires: today));
        ApiClient.Ok(res);
        Assert.Equal(today, (await ApiClient.Body(res)).GetProperty("expiresOnUtc").GetString()![..10]);
    }

    [Fact]
    public async Task A_license_that_has_expired_or_was_replaced_on_disk_blocks_the_api_again()
    {
        using var api = new LicensedApiFactory(fx);
        var c = await api.ClientAsync();
        var id = (await ApiClient.Body(await c.GetAsync("/api/license"))).GetProperty("installationId").GetString()!;
        ApiClient.Ok(await Install(c, LicensedApiFactory.License(id)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);

        // Someone copies an expired licence over the installed one: the next check catches it.
        await File.WriteAllBytesAsync(Path.Combine(api.Folder, "license.json"), LicensedApiFactory.License(id, expires: "2020-01-31"));
        var me = await c.GetAsync("/api/auth/me");
        Assert.Equal("license_required", await ApiClient.Code(me));
        Assert.Equal("expired", await Reason(me));
        var status = await ApiClient.Body(await c.GetAsync("/api/license"));
        Assert.Equal("expired", status.GetProperty("reason").GetString());
        Assert.Equal("Test Laboratory", status.GetProperty("customerName").GetString());

        // A file edited by hand fails the signature check.
        await File.WriteAllTextAsync(Path.Combine(api.Folder, "license.json"), "{\"payload\":\"e30=\",\"signature\":\"AAAA\"}");
        Assert.Equal("signature", await Reason(await c.GetAsync("/api/patients")));
    }

    [Fact]
    public async Task The_normal_test_environment_does_not_enforce_licensing()
    {
        var c = await fx.Api.ClientAsync();
        var status = await ApiClient.Body(await c.GetAsync("/api/license"));
        Assert.True(status.GetProperty("isValid").GetBoolean());
        Assert.Equal("not_enforced", status.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("/api/license", true)]
    [InlineData("/api/license/install", true)]
    [InlineData("/api/health", true)]
    [InlineData("/api/auth/csrf", true)]
    [InlineData("/orders/new", true)]
    [InlineData("/assets/index.js", true)]
    [InlineData("/api/auth/login", false)]
    [InlineData("/api/auth/me", false)]
    [InlineData("/api/licenses-elsewhere", false)]
    [InlineData("/API/PATIENTS", false)]
    public void The_gate_leaves_open_only_activation_health_and_the_web_app(string path, bool open) =>
        Assert.Equal(open, LicenseGate.IsOpen(new PathString(path)));
}
