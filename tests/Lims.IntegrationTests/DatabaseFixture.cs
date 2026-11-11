using Lims.Application.Abstractions;
using Lims.Domain.Core;
using Lims.Infrastructure.Persistence;
using Lims.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests;

/// <summary>
/// Creates a brand-new SQL Server database for the test run (LIMS_Test_xxxxxxxx), applies all
/// migrations exactly as production would, provisions two organisations, and drops the database
/// afterwards. Override the server with the LIMS_TEST_SERVER environment variable.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; }

    /// <summary>
    /// What the API under test connects with: a SQL login holding only the permissions of
    /// db/scripts/Grant-LimsAppPermissions.sql (as production does). Set LIMS_TEST_ADMIN_API=1 to use the admin connection instead.
    /// </summary>
    public string AppConnectionString { get; private set; }
    private string? _appLogin;
    private readonly string _server;
    public int OrgA { get; private set; }
    public int OrgB { get; private set; }
    public int OrgAMainBranch { get; private set; }
    public int OrgASecondBranch { get; private set; }
    public int OrgBMainBranch { get; private set; }

    public DatabaseFixture()
    {
        var server = Environment.GetEnvironmentVariable("LIMS_TEST_SERVER") ?? "localhost";
        var name = $"LIMS_Test_{Guid.NewGuid():N}"[..18];
        _server = server;
        ConnectionString = $"Server={server};Database={name};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True";
        AppConnectionString = ConnectionString;
    }

    private async Task CreateRestrictedLoginAsync(LimsDbContext db)
    {
        if (Environment.GetEnvironmentVariable("LIMS_TEST_ADMIN_API") == "1") return;
        var dbName = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
        _appLogin = "lims_app_" + dbName[^8..];
        var password = "Aa1-" + Guid.NewGuid().ToString("N");
        await db.Database.ExecuteSqlRawAsync($"CREATE LOGIN [{_appLogin}] WITH PASSWORD = '{password}', CHECK_POLICY = OFF");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "db", "scripts", "Grant-LimsAppPermissions.sql"))) dir = dir.Parent;
        var script = File.ReadAllText(Path.Combine(dir!.FullName, "db", "scripts", "Grant-LimsAppPermissions.sql")).Replace("$(AppLogin)", _appLogin);
        foreach (var batch in global::System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$", global::System.Text.RegularExpressions.RegexOptions.Multiline))
            if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);

        AppConnectionString = $"Server={_server};Database={dbName};User Id={_appLogin};Password={password};TrustServerCertificate=True;Encrypt=True";
    }

    public LimsDbContext Create(RequestContext request)
    {
        var options = new DbContextOptionsBuilder<LimsDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "core"))
            .Options;
        return new LimsDbContext(options, request, new LabClock(TimeProvider.System));
    }

    public LimsDbContext System() => Create(RequestContext.System());

    /// <summary>A signed-in user of organisation A limited to its main branch.</summary>
    public RequestContext UserOfA(int userId = 1001) => RequestContext.ForUser(OrgA, userId, false, OrgAMainBranch);

    public RequestContext UserOfB(int userId = 2001) => RequestContext.ForUser(OrgB, userId, false, OrgBMainBranch);

    public async Task InitializeAsync()
    {
        await using var db = System();
        await db.Database.MigrateAsync();

        var provisioner = new OrganizationProvisioner(db);
        OrgA = await provisioner.ProvisionAsync(new("LABA", "Lab A", "لیب اے", "MAIN", "Main A", "مین اے"));
        OrgB = await provisioner.ProvisionAsync(new("LABB", "Lab B", null, "MAIN", "Main B", null));

        // The test laboratories have separate reviewers: separation of duties and the critical read-back are switched on
        // (a new organisation defaults to a one-person laboratory, where both are relaxed).
        foreach (var org in new[] { OrgA, OrgB })
        {
            db.BusinessSettings.Add(new BusinessSetting { OrganizationId = org, Key = "Results.AllowSelfVerification", Value = "false" });
            db.BusinessSettings.Add(new BusinessSetting { OrganizationId = org, Key = "Results.BlockApprovalUntilCriticalNotified", Value = "true" });
        }

        var second = new Branch { OrganizationId = OrgA, Code = "CC1", NameEn = "Collection Centre 1" };
        db.Branches.Add(second);
        await db.SaveChangesAsync();

        OrgAMainBranch = await db.Branches.Where(b => b.OrganizationId == OrgA && b.IsMain).Select(b => b.Id).SingleAsync();
        OrgBMainBranch = await db.Branches.Where(b => b.OrganizationId == OrgB && b.IsMain).Select(b => b.Id).SingleAsync();
        OrgASecondBranch = second.Id;

        await CreateRestrictedLoginAsync(db);
    }

    private Api.ApiFactory? _api;

    /// <summary>The real API host running against this test database (created on first use).</summary>
    public Api.ApiFactory Api => _api ??= new Api.ApiFactory(this);

    public async Task DisposeAsync()
    {
        if (_api is not null) await _api.DisposeAsync();
        await using var db = System();
        await db.Database.EnsureDeletedAsync();
        if (_appLogin is not null)
        {
            await using var master = new Microsoft.Data.SqlClient.SqlConnection(
                new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" }.ConnectionString);
            await master.OpenAsync();
            await using var cmd = master.CreateCommand();
            cmd.CommandText = $"DROP LOGIN [{_appLogin}]";
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
