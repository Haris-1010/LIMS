using System.Linq.Expressions;
using System.Reflection;
using Lims.Application.Abstractions;
using Lims.Domain.Audit;
using Lims.Domain.Catalog;
using Lims.Domain.Common;
using Lims.Domain.Core;
using Lims.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Persistence;

public sealed partial class LimsDbContext(DbContextOptions<LimsDbContext> options, RequestContext request, ILabClock clock)
    : DbContext(options)
{
    // ---- core
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<BusinessSetting> BusinessSettings => Set<BusinessSetting>();
    // ---- security
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();
    // ---- patient
    public DbSet<Domain.Patients.Patient> Patients => Set<Domain.Patients.Patient>();
    public DbSet<Domain.Patients.Doctor> Doctors => Set<Domain.Patients.Doctor>();
    // ---- orders & billing
    public DbSet<Domain.Orders.LabOrder> Orders => Set<Domain.Orders.LabOrder>();
    public DbSet<Domain.Orders.LabOrderItem> OrderItems => Set<Domain.Orders.LabOrderItem>();
    public DbSet<Domain.Billing.Invoice> Invoices => Set<Domain.Billing.Invoice>();
    public DbSet<Domain.Billing.InvoiceLine> InvoiceLines => Set<Domain.Billing.InvoiceLine>();
    public DbSet<Domain.Billing.Payment> Payments => Set<Domain.Billing.Payment>();
    public DbSet<Domain.Billing.Refund> Refunds => Set<Domain.Billing.Refund>();
    public DbSet<Domain.Billing.CashDrawerSession> CashDrawerSessions => Set<Domain.Billing.CashDrawerSession>();
    // ---- catalog reference data
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<SampleType> SampleTypes => Set<SampleType>();
    public DbSet<TestCategory> TestCategories => Set<TestCategory>();
    public DbSet<LabTest> Tests => Set<LabTest>();
    public DbSet<TestBranch> TestBranches => Set<TestBranch>();
    public DbSet<TestParameter> TestParameters => Set<TestParameter>();
    public DbSet<ParameterOption> ParameterOptions => Set<ParameterOption>();
    public DbSet<ReferenceRange> ReferenceRanges => Set<ReferenceRange>();
    public DbSet<TestPackage> TestPackages => Set<TestPackage>();
    public DbSet<PackageTest> PackageTests => Set<PackageTest>();
    // ---- audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public RequestContext Request => request;

    // Query filters reference these instance members so EF re-evaluates them per context instance.
    private bool IsSystemContext => request.IsSystem;
    private int? CurrentOrganizationId => request.OrganizationId;
    private bool CanAccessAllBranches => request.AllBranches;
    private int[] CurrentBranchIds => request.BranchIds;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // All stored timestamps are UTC. Mark them as such when read, so the API sends "…Z" and
        // clients convert to local time correctly (SQL Server datetime2 has no time-zone information).
        builder.Properties<DateTime>().HavePrecision(3).HaveConversion<UtcDateTimeConverter>();
        builder.Properties<DateTime?>().HavePrecision(3).HaveConversion<UtcNullableDateTimeConverter>();
        builder.Properties<decimal>().HavePrecision(18, 2);
        builder.Properties<decimal?>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LimsDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => t.BaseType is null))
        {
            var clr = entityType.ClrType;

            // Optimistic concurrency on every auditable entity.
            if (IsAuditable(clr))
                modelBuilder.Entity(clr).Property(nameof(AuditableEntity<int>.RowVersion)).IsRowVersion();

            // Nothing cascades: deleting a parent must never silently remove clinical or financial rows.
            foreach (var fk in entityType.GetForeignKeys())
                fk.DeleteBehavior = DeleteBehavior.Restrict;

            var filter = BuildFilter(clr);
            if (filter is not null)
                modelBuilder.Entity(clr).HasQueryFilter(filter);
        }
    }

    private static bool IsAuditable(Type t)
    {
        for (var b = t; b is not null; b = b.BaseType)
            if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(AuditableEntity<>))
                return true;
        return false;
    }

    /// <summary>Combines tenant, branch and soft-delete filters for an entity type (EF allows one filter per type).</summary>
    private LambdaExpression? BuildFilter(Type clr)
    {
        var parts = new List<LambdaExpression>();
        if (typeof(IBranchEntity).IsAssignableFrom(clr))
            parts.Add(Invoke(nameof(BranchFilter), clr));
        else if (typeof(ITenantEntity).IsAssignableFrom(clr))
            parts.Add(Invoke(nameof(TenantFilter), clr));
        if (typeof(ISoftDelete).IsAssignableFrom(clr))
            parts.Add(Invoke(nameof(SoftDeleteFilter), clr));
        if (parts.Count == 0) return null;

        var param = Expression.Parameter(clr, "e");
        var body = parts
            .Select(p => new ReplaceParameter(p.Parameters[0], param).Visit(p.Body)!)
            .Aggregate(Expression.AndAlso);
        return Expression.Lambda(body, param);
    }

    private LambdaExpression Invoke(string method, Type clr) =>
        (LambdaExpression)GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(clr).Invoke(this, null)!;

    private Expression<Func<T, bool>> TenantFilter<T>() where T : ITenantEntity =>
        e => IsSystemContext || e.OrganizationId == CurrentOrganizationId;

    private Expression<Func<T, bool>> BranchFilter<T>() where T : IBranchEntity =>
        e => IsSystemContext ||
             (e.OrganizationId == CurrentOrganizationId && (CanAccessAllBranches || CurrentBranchIds.Contains(e.BranchId)));

    private Expression<Func<T, bool>> SoftDeleteFilter<T>() where T : ISoftDelete => e => !e.IsDeleted;

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
