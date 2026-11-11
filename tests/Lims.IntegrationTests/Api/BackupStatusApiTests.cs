using System.Net;
using Lims.Application.Backup;
using Lims.Infrastructure.Backup;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task Without_saved_settings_backups_are_on_by_default_in_the_data_folder()
    {
        var data = Path.Combine(Path.GetTempPath(), "lims-test-data-" + Guid.NewGuid().ToString("N"));
        using var f = fx.Api.WithWebHostBuilder(b => b.UseSetting("Backup:DataFolder", data));
        var admin = await SignedIn(f, "SYSADMIN");
        var settings = await ApiClient.Body(await admin.GetAsync("/api/backup/settings"));
        Assert.True(settings.GetProperty("enabled").GetBoolean());
        Assert.Equal(Path.Combine(data, "backups"), settings.GetProperty("folder").GetString());
        Assert.Equal("Daily", settings.GetProperty("fullFrequency").GetString());
        Assert.Equal(15, settings.GetProperty("logMinutes").GetInt32());
        Assert.False(settings.GetProperty("schedulerRunning").GetBoolean());   // Backup:Automatic=false in tests

        var body = await ApiClient.Body(await admin.GetAsync("/api/backup/status"));
        Assert.True(body.GetProperty("configured").GetBoolean());
        Assert.False(body.GetProperty("healthy").GetBoolean());
        Assert.Equal(0, body.GetProperty("files").GetArrayLength());
    }

    [Fact]
    public async Task Settings_are_validated_and_only_the_administrator_may_change_them()
    {
        var data = Path.Combine(Path.GetTempPath(), "lims-test-data-" + Guid.NewGuid().ToString("N"));
        using var f = fx.Api.WithWebHostBuilder(b => b.UseSetting("Backup:DataFolder", data));
        try
        {
            var folder = Path.Combine(data, "my-backups");
            object Body(string time = "21:30", int log = 30, string? offsite = null, string? dir = null) =>
                new { enabled = true, folder = dir ?? folder, offsiteFolder = offsite, fullFrequency = "Weekly", fullTime = time, logMinutes = log, retentionDays = 7 };

            var reception = await SignedIn(f, "RECEPTION");
            Assert.Equal(HttpStatusCode.Forbidden, (await reception.PutAsync("/api/backup/settings", Body())).StatusCode);

            var admin = await SignedIn(f, "SYSADMIN");
            Assert.Equal("backup_time_invalid", await ApiClient.Code(await admin.PutAsync("/api/backup/settings", Body(time: "25:00"))));
            Assert.Equal("backup_log_interval_invalid", await ApiClient.Code(await admin.PutAsync("/api/backup/settings", Body(log: 3))));
            Assert.Equal("backup_folder_invalid", await ApiClient.Code(await admin.PutAsync("/api/backup/settings", Body(dir: @"relative\path"))));
            Assert.Equal("backup_offsite_invalid", await ApiClient.Code(await admin.PutAsync("/api/backup/settings", Body(offsite: folder))));

            ApiClient.Ok(await admin.PutAsync("/api/backup/settings", Body()));
            Assert.True(Directory.Exists(folder));   // the server creates the folder itself
            var saved = await ApiClient.Body(await admin.GetAsync("/api/backup/settings"));
            Assert.Equal(folder, saved.GetProperty("folder").GetString());
            Assert.Equal("Weekly", saved.GetProperty("fullFrequency").GetString());
            Assert.Equal("21:30", saved.GetProperty("fullTime").GetString());
            Assert.Equal(30, saved.GetProperty("logMinutes").GetInt32());

            var status = await ApiClient.Body(await admin.GetAsync("/api/backup/status"));
            Assert.Equal(folder, status.GetProperty("folder").GetString());
            Assert.Equal(7 * 24 + 2, status.GetProperty("summary").GetProperty("fullMaxAgeHours").GetInt32());
        }
        finally { if (Directory.Exists(data)) Directory.Delete(data, true); }
    }

    [Fact]
    public async Task Back_up_now_works_with_the_least_privileged_application_login()
    {
        var data = Path.Combine(Path.GetTempPath(), "lims-test-data-" + Guid.NewGuid().ToString("N"));
        using var f = fx.Api.WithWebHostBuilder(b => b.UseSetting("Backup:DataFolder", data));
        try
        {
            var admin = await SignedIn(f, "SYSADMIN");
            var folder = Path.Combine(data, "backups");
            ApiClient.Ok(await admin.PutAsync("/api/backup/settings",
                new { enabled = true, folder, offsiteFolder = Path.Combine(data, "offsite"), fullFrequency = "Daily", fullTime = "22:00", logMinutes = 15, retentionDays = 14 }));

            ApiClient.Ok(await admin.PostAsync("/api/backup/run"));
            Assert.Single(Directory.GetFiles(folder, "*_FULL.bak"));

            // The scheduler's other jobs, run the way it runs them.
            var engine = f.Services.GetRequiredService<BackupEngine>();
            var settings = f.Services.GetRequiredService<BackupSettingsStore>().Load();
            await engine.RunAsync(BackupJob.Log, settings, CancellationToken.None);
            Assert.Single(Directory.GetFiles(folder, "*_LOG.trn"));
            await engine.RunAsync(BackupJob.Offsite, settings, CancellationToken.None);
            Assert.Equal(2, Directory.GetFiles(Path.Combine(data, "offsite")).Length);
            await engine.RunAsync(BackupJob.Retention, settings, CancellationToken.None);
            Assert.Equal(2, Directory.GetFiles(folder).Count(n => n.EndsWith(".bak") || n.EndsWith(".trn")));   // nothing is old enough

            var state = engine.ReadState(folder);
            Assert.NotNull(state.LastFull);
            Assert.NotNull(state.LastLog);
            Assert.DoesNotContain(BackupSchedule.DueJobs(DateTime.Now, settings, state), j => j is BackupJob.Full or BackupJob.Log);

            var status = await ApiClient.Body(await admin.GetAsync("/api/backup/status"));
            Assert.True(status.GetProperty("folderReadable").GetBoolean());
            Assert.Equal(2, status.GetProperty("files").GetArrayLength());
            Assert.True(status.GetProperty("healthy").GetBoolean(), status.GetProperty("problems").ToString());
        }
        finally { if (Directory.Exists(data)) Directory.Delete(data, true); }
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

            using var f = fx.Api.WithWebHostBuilder(b => { b.UseSetting("Backup:Folder", dir); b.UseSetting("Backup:Database", "LIMS"); b.UseSetting("Backup:ExpectOffsite", "true"); });
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
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.PostAsync("/api/backup/status", new { })).StatusCode);
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
