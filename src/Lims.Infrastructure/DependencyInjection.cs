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
        services.AddScoped<Orders.BillingService>();
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
        }));
}
