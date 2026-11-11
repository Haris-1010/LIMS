using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lims.Application.Abstractions;
using Lims.Application.Security;
using Lims.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lims.IntegrationTests.Api;

/// <summary>Runs the real API (cookies, antiforgery, authorization) against the test database.</summary>
public sealed class ApiFactory(DatabaseFixture fx) : WebApplicationFactory<Program>
{
    public const string Password = "Test-pass-2026";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Lims", fx.AppConnectionString);
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetTempPath(), "lims-test-keys"));
        // Re-check sessions against the database on every request so deactivation/password tests are immediate.
        builder.UseSetting("Auth:RevalidateSeconds", "0");
        builder.UseSetting("Auth:LoginAttemptsPerMinute", "10000"); // all test clients share one address
        // No automatic backups of test databases; backup settings go to a folder of this factory's own.
        builder.UseSetting("Backup:Automatic", "false");
        builder.UseSetting("Backup:DataFolder", BackupDataFolder);
    }

    public string BackupDataFolder { get; } = Path.Combine(Path.GetTempPath(), "lims-test-data-" + Guid.NewGuid().ToString("N"));

    /// <summary>A browser-like client: keeps cookies, does not follow redirects.</summary>
    public async Task<ApiClient> ClientAsync() =>
        await ApiClient.CreateAsync(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false }));

    /// <summary>Creates a user directly in the database (as an administrator of the given organisation would).</summary>
    public async Task<string> CreateUserAsync(int orgId, string[] roleCodes, bool allBranches = true, int[]? branchIds = null,
        bool mustChangePassword = false)
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<RequestContext>();
        ctx.OrganizationId = orgId;
        var db = scope.ServiceProvider.GetRequiredService<Lims.Infrastructure.Persistence.LimsDbContext>();
        var roleIds = await db.Roles.Where(r => roleCodes.Contains(r.Code)).Select(r => r.Id).ToArrayAsync();
        var username = "u" + Guid.NewGuid().ToString("N")[..10];
        await scope.ServiceProvider.GetRequiredService<UserService>().CreateAsync(new UserCreate
        {
            Username = username, DisplayNameEn = "Test " + username, Password = Password,
            AllBranches = allBranches, BranchIds = branchIds ?? [], RoleIds = roleIds,
        }, default);
        if (!mustChangePassword)
            await db.Users.Where(u => u.Username == username).ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, false));
        return username;
    }
}

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public HttpClient Http { get; }
    private string? _csrf;

    private ApiClient(HttpClient http) => Http = http;

    public static async Task<ApiClient> CreateAsync(HttpClient http)
    {
        var c = new ApiClient(http);
        await c.RefreshCsrfAsync();
        return c;
    }

    public async Task RefreshCsrfAsync()
    {
        var res = await Http.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        _csrf = res.GetProperty("token").GetString();
    }

    public async Task<HttpResponseMessage> LoginAsync(string username, string password = ApiFactory.Password)
    {
        var res = await PostAsync("/api/auth/login", new { username, password });
        if (res.IsSuccessStatusCode) await RefreshCsrfAsync(); // token is bound to the signed-in user
        return res;
    }

    public Task<HttpResponseMessage> GetAsync(string url) => Http.GetAsync(url);

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null, bool withCsrf = true) =>
        Send(HttpMethod.Post, url, body, withCsrf);

    public Task<HttpResponseMessage> PutAsync(string url, object body) => Send(HttpMethod.Put, url, body, true);

    private Task<HttpResponseMessage> Send(HttpMethod method, string url, object? body, bool withCsrf)
    {
        var req = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body ?? new { }) };
        if (withCsrf && _csrf is not null) req.Headers.Add("X-XSRF-TOKEN", _csrf);
        return Http.SendAsync(req);
    }

    public static async Task<JsonElement> Body(HttpResponseMessage res) =>
        JsonSerializer.Deserialize<JsonElement>(await res.Content.ReadAsStringAsync(), Json);

    public static async Task<string?> Code(HttpResponseMessage res) =>
        (await Body(res)).TryGetProperty("code", out var c) ? c.GetString() : null;

    public static void Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        if (res.StatusCode != expected)
            throw new Xunit.Sdk.XunitException($"Expected {expected} but got {res.StatusCode}: {res.Content.ReadAsStringAsync().Result}");
    }
}
