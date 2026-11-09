using Lims.Application.Abstractions;
using Lims.Domain.Core;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Api.Cli;

/// <summary>
/// Administrative commands run on the server, not over HTTP:
///   dotnet Lims.Api.dll db migrate      apply pending migrations
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
                log.LogInformation("Applying {Count} migration(s): {Names}", pending.Count, string.Join(", ", pending));
                await db.Database.MigrateAsync();
                log.LogInformation("Database is up to date");
                return 0;

            case "seed-demo":
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
                log.LogError("Unknown command 'db {Command}'. Use: db migrate | db seed-demo | db create-admin <ORG> <username> [display name]", args[1]);
                return 1;
        }
    }
}
