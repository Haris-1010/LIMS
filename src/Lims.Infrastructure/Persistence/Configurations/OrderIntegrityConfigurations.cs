using Lims.Domain.Billing;
using Lims.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

/// <summary>
/// Database-level guards for enum-like columns, so even a bug or a manual SQL statement cannot store
/// a status, priority or payment method that does not exist.
/// </summary>
internal sealed class OrderIntegrityConfig : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> b) => b.ToTable("Orders", "lab", t =>
    {
        t.HasCheckConstraint("CK_Orders_Priority", "[Priority] IN (0,1)");
        t.HasCheckConstraint("CK_Orders_Status", "[Status] IN (0,1)");
    });
}

internal sealed class OrderItemIntegrityConfig : IEntityTypeConfiguration<LabOrderItem>
{
    public void Configure(EntityTypeBuilder<LabOrderItem> b) => b.ToTable("OrderItems", "lab", t =>
        t.HasCheckConstraint("CK_OrderItems_Status", "[Status] IN (0,10,20,30,40,90)"));
}

internal sealed class RefundIntegrityConfig : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> b) => b.ToTable("Refunds", "billing", t =>
        t.HasCheckConstraint("CK_Refunds_Method", "[Method] BETWEEN 1 AND 5"));
}
