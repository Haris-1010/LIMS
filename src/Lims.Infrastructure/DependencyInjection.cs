using Lims.Application.Abstractions;
using Lims.Domain.Security;
using Lims.Infrastructure.Organizations;
using Lims.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Lims.Infrastructure.Persistence;
using Lims.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lims.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Lims")
            ?? throw new InvalidOperationException("Connection string 'Lims' is not configured.");

        services.AddLimsDbContext(connectionString);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILabClock>(sp => new LabClock(sp.GetRequiredService<TimeProvider>(), config["Lab:TimeZone"]));
        // Empty by default (fail closed). Authentication fills it per request (Phase 3).
        services.AddScoped<RequestContext>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<IOrganizationProvisioner, OrganizationProvisioner>();

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>(); // PBKDF2 (ASP.NET Core Identity)
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<RoleService>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<Patients.PatientService>();
        services.AddScoped<Patients.DoctorService>();
        services.AddScoped<Catalog.CatalogService>();
        services.AddScoped<Catalog.ReferenceRangeService>();
        services.AddScoped<Samples.SampleService>();
        services.AddScoped<Samples.TransferService>();
        services.AddScoped<Inventory.InventoryService>();
        services.AddScoped<Dashboard.DashboardService>();
        services.AddScoped<Audit.AuditService>();
        services.AddScoped<Settings.SettingsService>();
        services.AddSingleton<Backup.BackupSettingsStore>();
        services.AddSingleton<Backup.BackupEngine>();
        services.AddSingleton<Backup.BackupStatusService>();
        services.AddSingleton<Backup.BackupSetupService>();
        services.AddScoped<Results.ResultLookup>();
        services.AddScoped<Results.ResultService>();
        services.AddScoped<Results.WorklistService>();
        services.AddScoped<Results.CriticalAlertService>();
        services.AddScoped<Results.ResultReportSource>();
        services.AddScoped<Reports.ReportTemplateService>();
        services.AddScoped<Reports.ReportService>();
        services.AddScoped<Reports.SignatureService>();
        services.AddScoped<Orders.BillingService>();
        services.AddScoped<Corporate.CorporateService>();
        services.AddScoped<Orders.OrderService>();
        return services;
    }

    /// <summary>
    /// No automatic retry strategy: the database is on the local network, and the save pipeline uses
    /// explicit transactions (order + invoice + audit) that a retrying strategy would not allow.
    /// </summary>
    public static IServiceCollection AddLimsDbContext(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<LimsDbContext>(o => o.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", "core");
            sql.MigrationsAssembly(typeof(LimsDbContext).Assembly.FullName);
            // Contains(list) as literal values instead of OPENJSON(@json): with OPENJSON SQL Server guesses the list size
            // and picks scans; with 550,000 orders the same predicate took 525 ms against 12 ms (docs/PERFORMANCE.md).
            sql.TranslateParameterizedCollectionsToConstants();
        }));
}
