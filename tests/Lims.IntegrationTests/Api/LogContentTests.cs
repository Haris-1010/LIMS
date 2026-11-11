using System.Net;
using System.Text;

namespace Lims.IntegrationTests.Api;

/// <summary>
/// Phase 13 log review: runs real requests that carry personal data and secrets with the production log settings
/// (Information level, file sink) and proves none of it reaches the log files.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class LogContentTests(DatabaseFixture fx)
{
    private static string ReadAllLogs(string dir)
    {
        var sb = new StringBuilder();
        foreach (var file in Directory.GetFiles(dir, "lims-*.log"))
        {
            using var s = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var r = new StreamReader(s);
            sb.Append(r.ReadToEnd());
        }
        return sb.ToString();
    }

    [Fact]
    public async Task Log_files_contain_no_patient_data_passwords_or_search_terms()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lims-test-logs-" + Guid.NewGuid().ToString("N"));
        string log;
        try
        {
            using (var f = fx.Api.WithWebHostBuilder(b => b.UseSetting("Logging:Path", dir)))
            {
                var username = await fx.Api.CreateUserAsync(fx.OrgA, ["RECEPTION"]);
                var c = await ApiClient.CreateAsync(f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true }));

                // Failed sign-ins: unknown user, wrong password.
                Assert.Equal(HttpStatusCode.Unauthorized, (await c.LoginAsync("no.such.user.markerx", "Wrong-Pass-MARKERP1")).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await c.LoginAsync(username, "Wrong-Pass-MARKERP2")).StatusCode);
                ApiClient.Ok(await c.LoginAsync(username));

                // Patient data in bodies, query strings and responses.
                ApiClient.Ok(await c.PostAsync("/api/patients", new
                {
                    fullName = "Zzyzx Markerson", sex = 2, mobile = "0300-5550199", cnic = "35202-9876543-1", ageYears = 41,
                    email = "marker.patient@example.com", address = "Marker Street 77", confirmNotDuplicate = true,
                }), HttpStatusCode.Created);
                ApiClient.Ok(await c.GetAsync("/api/patients?search=Zzyzx%20Markerson"));
                ApiClient.Ok(await c.GetAsync("/api/patients?search=35202-9876543-1"));
                ApiClient.Ok(await c.GetAsync("/api/patients/duplicates?fullName=Zzyzx%20Markerson&mobile=03005550199"));
                await c.PostAsync("/api/patients", new { fullName = "Zzyzx Bad", sex = 2, cnic = "99999-MARKERC-1", ageYears = 3 }); // validation failure

                // Password change.
                var change = await c.PostAsync("/api/auth/change-password", new { currentPassword = ApiFactory.Password, newPassword = "New-Pass-MARKERN1" });
                Assert.True(change.IsSuccessStatusCode, await change.Content.ReadAsStringAsync());
            } // disposing the host flushes the log

            log = ReadAllLogs(dir);
        }
        finally { if (Directory.Exists(dir)) try { Directory.Delete(dir, true); } catch (IOException) { } }

        // Not vacuous: requests are logged...
        Assert.Contains("/api/patients", log);
        Assert.Contains("/api/auth/login", log);
        // ...but none of the data is.
        foreach (var secret in new[]
        {
            "Zzyzx", "Markerson", "9876543", "5550199", "marker.patient", "Marker Street",
            "markerx", "MARKERP", "MARKERN", "MARKERC", ApiFactory.Password,
        })
            Assert.DoesNotContain(secret, log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sql_server_error_text_with_data_values_is_withheld_from_the_log()
    {
        // A real SqlException: its message quotes the duplicate value and the truncated value.
        Microsoft.Data.SqlClient.SqlException? dup = null, trunc = null;
        await using (var conn = new Microsoft.Data.SqlClient.SqlConnection(fx.ConnectionString))
        {
            await conn.OpenAsync();
            async Task Run(string sql) { await using var cmd = conn.CreateCommand(); cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync(); }
            await Run("CREATE TABLE #t (n nvarchar(5), u nvarchar(30) UNIQUE); INSERT #t VALUES (N'ok', N'35202-1234567-1')");
            dup = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => Run("INSERT #t VALUES (N'x', N'35202-1234567-1')"));
            trunc = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => Run("INSERT #t VALUES (N'Muhammad Ahmed Khan', N'b')"));
        }
        Assert.Contains("35202-1234567-1", dup!.Message);   // proves the raw message really does carry the value
        Assert.Contains("Muham", trunc!.Message);

        var captured = new Capture();
        using (var sink = new Lims.Api.Infrastructure.ScrubbingSink(captured))
        {
            var logger = new Serilog.LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
            logger.Error(new InvalidOperationException("saving failed", dup), "Unhandled exception for {Path}", "/api/patients");
            logger.Error(new AggregateException(trunc), "Second failure");
            logger.Error(new InvalidOperationException("plain failure without SQL"), "Third failure");
        }
        var log = captured.Text.ToString();
        Assert.DoesNotContain("35202", log);
        Assert.DoesNotContain("Muham", log);
        Assert.Contains("SQL Server error 2627", log);        // error number is kept
        Assert.Contains("SQL Server error 2628", log);
        Assert.Contains("plain failure without SQL", log);    // other exceptions are untouched
        Assert.Contains("LogContentTests", log);              // stack trace is kept
    }

    private sealed class Capture : Serilog.Core.ILogEventSink
    {
        public readonly StringBuilder Text = new();
        public void Emit(Serilog.Events.LogEvent e) => Text.AppendLine(e.RenderMessage()).AppendLine(e.Exception?.ToString());
    }
}
