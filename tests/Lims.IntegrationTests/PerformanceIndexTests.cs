using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests;

/// <summary>The indexes found necessary by the Phase 13 load test (550,000 orders) exist after a normal migration.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class PerformanceIndexTests(DatabaseFixture fx)
{
    private static readonly string[] Expected =
    [
        "IX_LabReports_IssuedAtUtc_Id", "IX_LabReports_PatientId", "IX_LabReports_ReportNo",
        "IX_TestResults_Status_ApprovedAtUtc_Id", "IX_TestResults_Status_VerifiedAtUtc", "IX_TestResults_OrderId_Status",
        "IX_Invoices_OrderId", "IX_Invoices_InvoiceNo", "IX_Orders_OrderNo", "IX_Orders_PatientId",
        "IX_Samples_Barcode", "IX_Samples_Status_ReceivedAtUtc", "IX_Samples_PatientId",
        "IX_Payments_ReceivedAtUtc", "IX_AuditLogs_Org_EntityType_Action_AtUtc",
        "IX_Patients_PatientNo", "IX_Patients_Cnic", "IX_Patients_Mobile", "IX_Patients_CreatedAtUtc", "IX_Orders_CreatedAtUtc", "IX_Samples_ReplacesSampleId",
    ];

    [Fact]
    public async Task Load_test_indexes_exist_and_keep_their_column_order_and_direction()
    {
        await using var db = fx.System();
        var rows = await db.Database.SqlQueryRaw<IndexRow>("""
            SELECT i.name AS Name,
                   STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END END, ',')
                       WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyColumns
              FROM sys.indexes i
              JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
              JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
             WHERE i.name IN ({0}) GROUP BY i.name
            """.Replace("{0}", string.Join(",", Expected.Select(e => $"'{e}'")))).ToListAsync();

        var found = rows.ToDictionary(r => r.Name, r => r.KeyColumns);
        Assert.Equal(Expected.Length, found.Count);

        // The shapes the queries depend on: the sort direction must match the ORDER BY or SQL Server sorts instead of reading in order.
        Assert.Equal("Status,ApprovedAtUtc DESC,Id DESC", found["IX_TestResults_Status_ApprovedAtUtc_Id"]);
        Assert.Equal("IssuedAtUtc DESC,Id DESC", found["IX_LabReports_IssuedAtUtc_Id"]);
        Assert.Equal("OrganizationId,EntityType,Action,AtUtc DESC", found["IX_AuditLogs_Org_EntityType_Action_AtUtc"]);
        Assert.Equal("Barcode", found["IX_Samples_Barcode"]);
    }

    public sealed record IndexRow(string Name, string KeyColumns);
}
