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
    public int OrgA { get; private set; }
    public int OrgB { get; private set; }
    public int OrgAMainBranch { get; private set; }
    public int OrgASecondBranch { get; private set; }
    public int OrgBMainBranch { get; private set; }

    public DatabaseFixture()
    {
        var server = Environment.GetEnvironmentVariable("LIMS_TEST_SERVER") ?? "localhost";
        var name = $"LIMS_Test_{Guid.NewGuid():N}"[..18];
        ConnectionString = $"Server={server};Database={name};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True";
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

        var second = new Branch { OrganizationId = OrgA, Code = "CC1", NameEn = "Collection Centre 1" };
        db.Branches.Add(second);
        await db.SaveChangesAsync();

        OrgAMainBranch = await db.Branches.Where(b => b.OrganizationId == OrgA && b.IsMain).Select(b => b.Id).SingleAsync();
        OrgBMainBranch = await db.Branches.Where(b => b.OrganizationId == OrgB && b.IsMain).Select(b => b.Id).SingleAsync();
        OrgASecondBranch = second.Id;
    }

    private Api.ApiFactory? _api;

    /// <summary>The real API host running against this test database (created on first use).</summary>
    public Api.ApiFactory Api => _api ??= new Api.ApiFactory(this);

    public async Task DisposeAsync()
    {
        if (_api is not null) await _api.DisposeAsync();
        await using var db = System();
        await db.Database.EnsureDeletedAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
