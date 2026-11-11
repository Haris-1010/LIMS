using Lims.Domain.Audit;
using Lims.Domain.Billing;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Lims.Domain.Reports;
using Lims.Domain.Results;
using Lims.Domain.Samples;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

// Indexes added in Phase 13 after loading 550,000 orders and timing the screens (docs/PERFORMANCE.md). Each one was created in the load
// database first and kept only where it measurably helped.
//
// Why several of them lack the OrganizationId prefix that the older indexes have: every query carries the tenant filter
// (@IsSystemContext = 1 OR OrganizationId = @org), and with that OR SQL Server cannot seek on an index that starts with OrganizationId
// when the query's own condition is on another column (an order number, a patient id, a status). An index that starts with the searched
// column can be seeked, and OrganizationId rides along as an included column for the residual check.
//
// Names are explicit because EF merges two HasIndex calls over the same columns (see CLAUDE.md).

internal sealed class LabReportPerformanceIndexes : IEntityTypeConfiguration<LabReport>
{
    public void Configure(EntityTypeBuilder<LabReport> b)
    {
        b.HasIndex(x => new { x.IssuedAtUtc, x.Id }, "IX_LabReports_IssuedAtUtc_Id").IsDescending(true, true).IncludeProperties(x => new { x.OrganizationId, x.Status });
        b.HasIndex(x => x.PatientId, "IX_LabReports_PatientId").IncludeProperties(x => new { x.OrganizationId, x.Status });
        b.HasIndex(x => x.ReportNo, "IX_LabReports_ReportNo").IncludeProperties(x => new { x.OrganizationId, x.Status });
    }
}

internal sealed class TestResultPerformanceIndexes : IEntityTypeConfiguration<TestResult>
{
    public void Configure(EntityTypeBuilder<TestResult> b)
    {
        b.HasIndex(x => new { x.Status, x.ApprovedAtUtc, x.Id }, "IX_TestResults_Status_ApprovedAtUtc_Id").IsDescending(false, true, true).IncludeProperties(x => new { x.OrganizationId, x.OrderId });
        b.HasIndex(x => new { x.Status, x.VerifiedAtUtc }, "IX_TestResults_Status_VerifiedAtUtc").IncludeProperties(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrderId, x.Status }, "IX_TestResults_OrderId_Status").IncludeProperties(x => x.OrganizationId);
    }
}

internal sealed class InvoicePerformanceIndexes : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.HasIndex(x => x.OrderId, "IX_Invoices_OrderId").IncludeProperties(x => new { x.OrganizationId, x.BranchId });
        b.HasIndex(x => x.InvoiceNo, "IX_Invoices_InvoiceNo").IncludeProperties(x => new { x.OrganizationId, x.BranchId, x.OrderId });
    }
}

internal sealed class OrderPerformanceIndexes : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> b)
    {
        b.HasIndex(x => x.OrderNo, "IX_Orders_OrderNo").IncludeProperties(x => new { x.OrganizationId, x.BranchId });
        b.HasIndex(x => x.PatientId, "IX_Orders_PatientId").IncludeProperties(x => new { x.OrganizationId, x.BranchId });
        b.HasIndex(x => x.CreatedAtUtc, "IX_Orders_CreatedAtUtc").IncludeProperties(x => new { x.OrganizationId, x.BranchId, x.Status });
    }
}

internal sealed class SamplePerformanceIndexes : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> b)
    {
        b.HasIndex(x => x.Barcode, "IX_Samples_Barcode").IncludeProperties(x => new { x.OrganizationId, x.OriginBranchId, x.CurrentBranchId, x.DestinationBranchId });
        b.HasIndex(x => new { x.Status, x.ReceivedAtUtc }, "IX_Samples_Status_ReceivedAtUtc").IsDescending(false, true)
            .IncludeProperties(x => new { x.OrganizationId, x.OriginBranchId, x.CurrentBranchId, x.DestinationBranchId });
        // Looking up the replacement of a rejected sample happens on every sample view; only replacements carry a value, so the index is tiny.
        b.HasIndex(x => x.ReplacesSampleId, "IX_Samples_ReplacesSampleId").IncludeProperties(x => new { x.OrganizationId, x.OriginBranchId, x.CurrentBranchId, x.DestinationBranchId }).HasFilter("[ReplacesSampleId] IS NOT NULL");
        b.HasIndex(x => x.PatientId, "IX_Samples_PatientId").IncludeProperties(x => new { x.OrganizationId, x.OriginBranchId, x.CurrentBranchId, x.DestinationBranchId });
    }
}

internal sealed class PaymentPerformanceIndexes : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b) =>
        b.HasIndex(x => x.ReceivedAtUtc, "IX_Payments_ReceivedAtUtc").IsDescending(true).IncludeProperties(x => new { x.OrganizationId, x.BranchId, x.Method, x.Amount, x.InvoiceId });
}

internal sealed class AuditLogPerformanceIndexes : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b) =>
        b.HasIndex(x => new { x.OrganizationId, x.EntityType, x.Action, x.AtUtc }, "IX_AuditLogs_Org_EntityType_Action_AtUtc").IsDescending(false, false, false, true);
}

internal sealed class PatientPerformanceIndexes : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.HasIndex(x => x.CreatedAtUtc, "IX_Patients_CreatedAtUtc").IsDescending(true).IncludeProperties(x => new { x.OrganizationId, x.IsDeleted });
        b.HasIndex(x => x.PatientNo, "IX_Patients_PatientNo").IncludeProperties(x => new { x.OrganizationId, x.IsDeleted });
        b.HasIndex(x => x.Cnic, "IX_Patients_Cnic").IncludeProperties(x => new { x.OrganizationId, x.IsDeleted }).HasFilter("[Cnic] IS NOT NULL");
        b.HasIndex(x => x.Mobile, "IX_Patients_Mobile").IncludeProperties(x => new { x.OrganizationId, x.IsDeleted }).HasFilter("[Mobile] IS NOT NULL");
    }
}
