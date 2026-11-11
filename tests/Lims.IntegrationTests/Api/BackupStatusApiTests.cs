using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lims.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class BackupStatusApiTests(DatabaseFixture fx)
{
    private async Task<ApiClient> SignedIn(WebApplicationFactory<Program> api, params string[] roles)
    {
        var c = await ApiClient.CreateAsync(api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }));
        ApiClient.Ok(await c.LoginAsync(await fx.Api.CreateUserAsync(fx.OrgA, roles)));
        return c;
    }

    [Fact]
    public async Task Only_holders_of_backup_manage_may_read_it_and_anonymous_is_refused()
    {
        var anon = await fx.Api.ClientAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/backup/status")).StatusCode);

        var reception = await SignedIn(fx.Api, "RECEPTION");
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/backup/status")).StatusCode);
        var manager = await SignedIn(fx.Api, "LABMGR");   // the Lab Manager role does not include backup.manage
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/backup/status")).StatusCode);

        var admin = await SignedIn(fx.Api, "SYSADMIN");
        ApiClient.Ok(await admin.GetAsync("/api/backup/status"));
    }

    [Fact]
    public async Task Without_a_configured_folder_the_page_says_so()
    {
        var admin = await SignedIn(fx.Api, "SYSADMIN");
        var body = await ApiClient.Body(await admin.GetAsync("/api/backup/status"));
        Assert.False(body.GetProperty("configured").GetBoolean());
        Assert.False(body.GetProperty("healthy").GetBoolean());
        Assert.Equal(0, body.GetProperty("files").GetArrayLength());
    }

    [Fact]
    public async Task Reads_the_folder_and_status_file_and_reports_problems()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lims-test-backups-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var now = DateTime.Now;
            string Name(DateTime t, string suffix) => $"LIMS_{t:yyyyMMdd_HHmmss}_{suffix}";
            File.WriteAllBytes(Path.Combine(dir, Name(now.AddHours(-3), "FULL.bak")), new byte[2048]);
            File.WriteAllBytes(Path.Combine(dir, Name(now.AddMinutes(-5), "LOG.trn")), new byte[512]);
            File.WriteAllText(Path.Combine(dir, "notes.txt"), "not a backup");
            string Stamp(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm:ss.fff");
            File.WriteAllText(Path.Combine(dir, "backup-status.json"),
                "{\"Full\":{\"lastSuccess\":\"" + Stamp(now.AddHours(-3)) + "\",\"lastDetail\":\"full ok\"}," +
                "\"Offsite\":{\"lastSuccess\":\"" + Stamp(now.AddHours(-40)) + "\",\"lastFailure\":\"" + Stamp(now.AddMinutes(-3)) + "\",\"lastFailureDetail\":\"share unreachable\"}}");

            using var f = fx.Api.WithWebHostBuilder(b => { b.UseSetting("Backup:Folder", dir); b.UseSetting("Backup:Database", "LIMS"); });
            var admin = await SignedIn(f, "SYSADMIN");
            var body = await ApiClient.Body(await admin.GetAsync("/api/backup/status"));

            Assert.True(body.GetProperty("configured").GetBoolean());
            Assert.True(body.GetProperty("folderReadable").GetBoolean());
            Assert.Equal(2, body.GetProperty("files").GetArrayLength());            // notes.txt and the status file are not backups
            Assert.Equal(2560, body.GetProperty("summary").GetProperty("totalBytes").GetInt64());

            var codes = body.GetProperty("problems").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToList();
            Assert.Contains("jobFailed:Offsite", codes);
            Assert.Contains("offsiteOld", codes);
            Assert.DoesNotContain("noFull", codes);
            Assert.DoesNotContain("fullOld", codes);
            Assert.DoesNotContain("logOld", codes);
            Assert.False(body.GetProperty("healthy").GetBoolean());
            Assert.Equal("share unreachable", body.GetProperty("problems").EnumerateArray().First(p => p.GetProperty("code").GetString() == "jobFailed:Offsite").GetProperty("detail").GetString());

            // The page only reads: it offers no way to change anything.
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.PostAsync("/api/backup/status", new { })).StatusCode);
            Assert.Contains((await admin.PostAsync("/api/backup/run", new { })).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed }); // no such endpoint
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task A_missing_or_unreadable_folder_is_reported_not_treated_as_empty()
    {
        using var f = fx.Api.WithWebHostBuilder(b => b.UseSetting("Backup:Folder", Path.Combine(Path.GetTempPath(), "lims-does-not-exist-" + Guid.NewGuid().ToString("N"))));
        var admin = await SignedIn(f, "SYSADMIN");
        var body = await ApiClient.Body(await admin.GetAsync("/api/backup/status"));
        Assert.True(body.GetProperty("configured").GetBoolean());
        Assert.False(body.GetProperty("folderReadable").GetBoolean());
        Assert.Equal("folderUnreadable", body.GetProperty("problems")[0].GetProperty("code").GetString());
    }
}
