using Lims.Application.Abstractions;
using Lims.Domain.Core;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Api.Cli;

/// <summary>
/// Administrative commands run on the server, not over HTTP:
///   dotnet Lims.Api.dll db migrate      take a full backup (when there is something to migrate), apply pending migrations,
///                                       then add the starter test catalog to any organisation that has not had it (--no-starter-catalog skips)
///   dotnet Lims.Api.dll db backup [--folder X] [--log]   take a full (or log) backup now, as the automatic backups do
///   dotnet Lims.Api.dll db seed-demo    create a demo organisation (development/training only)
/// </summary>
public static class DatabaseCommands
{
    public static bool IsCommand(string[] args) => args.Length >= 2 && args[0] == "db";

    /// <summary>
    /// Creates a System Administrator with access to all branches and a random temporary password,
    /// printed once. The user must change it at first sign-in. Use for the first account of an
    /// organisation, or to regain access if every administrator is locked out.
    /// </summary>
    private static async Task<int> CreateAdminAsync(WebApplication app, LimsDbContext systemDb, string[] args, ILogger log)
    {
        if (args.Length < 4)
        {
            log.LogError("Usage: db create-admin <ORG-CODE> <username> [display name]");
            return 1;
        }
        var orgCode = args[2].ToUpperInvariant();
        var orgId = await systemDb.Organizations.Where(o => o.Code == orgCode).Select(o => (int?)o.Id).SingleOrDefaultAsync();
        if (orgId is null)
        {
            log.LogError("Organisation {Code} not found", orgCode);
            return 1;
        }

        // Act as a normal (non-system) caller inside that organisation, so tenant rules apply.
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<RequestContext>();
        ctx.OrganizationId = orgId;
        var db = scope.ServiceProvider.GetRequiredService<LimsDbContext>();
        var adminRole = await db.Roles.Where(r => r.Code == "SYSADMIN").Select(r => r.Id).SingleAsync();

        var password = Application.Security.PasswordPolicy.GenerateTemporary();
        var users = scope.ServiceProvider.GetRequiredService<Lims.Infrastructure.Security.UserService>();
        try
        {
            await users.CreateAsync(new Application.Security.UserCreate
            {
                Username = args[3],
                DisplayNameEn = args.Length > 4 ? string.Join(' ', args[4..]) : "System Administrator",
                Password = password,
                AllBranches = true,
                RoleIds = [adminRole],
            }, CancellationToken.None);
        }
        catch (Application.Common.BusinessRuleException ex)
        {
            log.LogError("Could not create administrator: {Message}", ex.Message);
            return 1;
        }

        // Printed to the console only (not to the log files).
        Console.WriteLine();
        Console.WriteLine($"  Administrator '{args[3].ToLowerInvariant()}' created for {orgCode}.");
        Console.WriteLine($"  Temporary password: {password}");
        Console.WriteLine("  It must be changed at first sign-in.");
        Console.WriteLine();
        return 0;
    }

    /// <summary>
    /// Adds the starter test catalog (common tests, parameters, draft reference ranges) to one organisation, once.
    /// Skipped with --no-starter-catalog. See <see cref="Lims.Infrastructure.Catalog.StarterCatalog"/>.
    /// </summary>
    private static async Task AddStarterCatalogAsync(WebApplication app, int orgId, string orgCode, ILogger log)
    {
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<RequestContext>();
        ctx.BecomeSystem();
        ctx.OrganizationId = orgId;
        var result = await scope.ServiceProvider.GetRequiredService<Lims.Infrastructure.Catalog.StarterCatalogSeeder>().SeedAsync(orgId);
        if (!result.AlreadyAdded)
            log.LogInformation("Organisation {Code}: starter catalog added ({Tests} tests, {Parameters} parameters, {Ranges} reference ranges waiting for approval; {Skipped} skipped because the code is already used). Set prices and approve the ranges in the catalog screens",
                orgCode, result.Tests, result.Parameters, result.Ranges, result.SkippedTests);
    }

    /// <summary>Takes a backup with the same engine and settings as the automatic backups. Waits if one is already running.</summary>
    private static async Task<int> BackupAsync(WebApplication app, string[] args, ILogger log)
    {
        var store = app.Services.GetRequiredService<Lims.Infrastructure.Backup.BackupSettingsStore>();
        var engine = app.Services.GetRequiredService<Lims.Infrastructure.Backup.BackupEngine>();
        var i = Array.IndexOf(args, "--folder");
        var folder = i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : null;
        var job = args.Contains("--log") ? Application.Backup.BackupJob.Log : Application.Backup.BackupJob.Full;
        try
        {
            var result = await engine.RunAsync(job, store.Load(), CancellationToken.None, waitIfBusy: true, folderOverride: folder);
            log.LogInformation("Backup done: {Detail} in {Folder}", result.Message, folder ?? store.Load().Folder);
            return 0;
        }
        catch (Application.Common.BusinessRuleException ex)
        {
            log.LogError("Backup failed: {Reason}", ex.Message);
            return 1;
        }
    }

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RequestContext>().BecomeSystem();
        var db = scope.ServiceProvider.GetRequiredService<LimsDbContext>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Lims.Cli");

        switch (args[1])
        {
            case "migrate":
                var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
                // Before changing an existing database, back it up (skipped for a new database, or with --no-backup
                // when the caller has just taken one, as Deploy-Lims.ps1 does).
                if (pending.Count > 0 && !args.Contains("--no-backup") && await db.Database.CanConnectAsync()
                    && (await db.Database.GetAppliedMigrationsAsync()).Any())
                {
                    log.LogInformation("Backing up the database before migrating");
                    if (await BackupAsync(app, args, log) != 0)
                    {
                        log.LogError("Migration stopped because the backup failed. Fix the backup, or run again with --no-backup to migrate without one");
                        return 1;
                    }
                }
                log.LogInformation("Applying {Count} migration(s): {Names}", pending.Count, string.Join(", ", pending));
                await db.Database.MigrateAsync();
                log.LogInformation("Database is up to date");
                if (!args.Contains("--no-starter-catalog"))
                    foreach (var o in await db.Organizations.Select(o => new { o.Id, o.Code }).ToListAsync())
                        await AddStarterCatalogAsync(app, o.Id, o.Code, log);
                return 0;

            case "seed-demo":
                // Demo data must never be created on a production database by mistake.
                if (!app.Environment.IsDevelopment() && !args.Contains("--allow-demo"))
                {
                    log.LogError("seed-demo creates demo data and is for development/training only. " +
                        "Run with ASPNETCORE_ENVIRONMENT=Development, or add --allow-demo if this is a training database");
                    return 1;
                }
                if (await db.Organizations.AnyAsync())
                {
                    log.LogWarning("Organisations already exist; demo data not created");
                    return 0;
                }
                var provisioner = scope.ServiceProvider.GetRequiredService<IOrganizationProvisioner>();
                var orgId = await provisioner.ProvisionAsync(new OrganizationSetup(
                    "DEMO", "Demo Diagnostic Laboratory", "ڈیمو ڈائیگنوسٹک لیبارٹری", "MAIN", "Main Laboratory", "مرکزی لیبارٹری"));
                db.Branches.AddRange(
                    new Branch { OrganizationId = orgId, Code = "CC-A", NameEn = "Collection Centre A", NameUr = "کلیکشن سینٹر اے" },
                    new Branch { OrganizationId = orgId, Code = "CC-B", NameEn = "Collection Centre B", NameUr = "کلیکشن سینٹر بی" });
                await db.SaveChangesAsync();
                log.LogInformation("Demo organisation created with id {OrgId}", orgId);
                return 0;

            case "create-org":
                // Real (non-demo) organisation for a production installation: db create-org <CODE> "<English name>" ["<Urdu name>"]
                if (args.Length < 4 || !System.Text.RegularExpressions.Regex.IsMatch(args[2], "^[A-Za-z0-9]{2,10}$"))
                {
                    log.LogError("Usage: db create-org <CODE (2-10 letters or digits)> \"<English name>\" [\"<Urdu name>\"]");
                    return 1;
                }
                var newCode = args[2].ToUpperInvariant();
                if (await db.Organizations.AnyAsync(o => o.Code == newCode))
                {
                    log.LogError("Organisation {Code} already exists", newCode);
                    return 1;
                }
                var newOrgId = await scope.ServiceProvider.GetRequiredService<IOrganizationProvisioner>().ProvisionAsync(new OrganizationSetup(
                    newCode, args[3], args.Length > 4 && !args[4].StartsWith("--") ? args[4] : null, "MAIN", "Main Laboratory", "مرکزی لیبارٹری"));
                log.LogInformation("Organisation {Code} created with id {OrgId}. Next: db create-admin {Code} <username>", newCode, newOrgId, newCode);
                if (!args.Contains("--no-starter-catalog"))
                    await AddStarterCatalogAsync(app, newOrgId, newCode, log);
                return 0;

            case "backup":
                return await BackupAsync(app, args, log);

            case "seed-load":
                // Load-test data (Phase 13). Only on a database named LIMS_Load*; see LoadDataSeeder.
                return await LoadDataSeeder.RunAsync(app, db, args, log);

            case "create-admin":
                return await CreateAdminAsync(app, db, args, log);

            case "plan-samples":
                // Plans samples (with barcodes) for orders created before samples existed. Safe to run repeatedly.
                foreach (var targetOrg in await db.Organizations.Select(o => o.Id).ToListAsync())
                {
                    using var orgScope = app.Services.CreateScope();
                    var orgCtx = orgScope.ServiceProvider.GetRequiredService<RequestContext>();
                    orgCtx.BecomeSystem();
                    orgCtx.OrganizationId = targetOrg;
                    var planned = await orgScope.ServiceProvider.GetRequiredService<Lims.Infrastructure.Samples.SampleService>().BackfillAsync(CancellationToken.None);
                    log.LogInformation("Organisation {OrgId}: samples planned for {Count} order(s)", targetOrg, planned);
                }
                return 0;

            default:
                log.LogError("Unknown command 'db {Command}'. Use: db migrate [--no-backup] [--no-starter-catalog] | db backup [--folder X] [--log] | db seed-demo | db create-org <CODE> <name> [urdu name] [--no-starter-catalog] | db create-admin <ORG> <username> [display name]", args[1]);
                return 1;
        }
    }
}
