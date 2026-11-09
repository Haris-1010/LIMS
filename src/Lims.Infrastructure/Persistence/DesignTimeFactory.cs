using Lims.Application.Abstractions;
using Lims.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lims.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef` to create migrations; never at runtime.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<LimsDbContext>
{
    public LimsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("LIMS_DESIGN_CONNECTION")
            ?? "Server=localhost;Database=LIMS_Dev;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True";
        var options = new DbContextOptionsBuilder<LimsDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "core"))
            .Options;
        return new LimsDbContext(options, RequestContext.System(), new LabClock(TimeProvider.System));
    }
}
