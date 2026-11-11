using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TestResults_OrderId_Status",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "OrderId", "Status" })
                .Annotation("SqlServer:Include", new[] { "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_Status_ApprovedAtUtc_Id",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "Status", "ApprovedAtUtc", "Id" },
                descending: new[] { false, true, true })
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_Status_VerifiedAtUtc",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "Status", "VerifiedAtUtc" })
                .Annotation("SqlServer:Include", new[] { "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_Barcode",
                schema: "lab",
                table: "Samples",
                column: "Barcode")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "OriginBranchId", "CurrentBranchId", "DestinationBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_PatientId",
                schema: "lab",
                table: "Samples",
                column: "PatientId")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "OriginBranchId", "CurrentBranchId", "DestinationBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_ReplacesSampleId",
                schema: "lab",
                table: "Samples",
                column: "ReplacesSampleId",
                filter: "[ReplacesSampleId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "OriginBranchId", "CurrentBranchId", "DestinationBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_Status_ReceivedAtUtc",
                schema: "lab",
                table: "Samples",
                columns: new[] { "Status", "ReceivedAtUtc" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "OriginBranchId", "CurrentBranchId", "DestinationBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReceivedAtUtc",
                schema: "billing",
                table: "Payments",
                column: "ReceivedAtUtc",
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "BranchId", "Method", "Amount", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Cnic",
                schema: "patient",
                table: "Patients",
                column: "Cnic",
                filter: "[Cnic] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_CreatedAtUtc",
                schema: "patient",
                table: "Patients",
                column: "CreatedAtUtc",
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Mobile",
                schema: "patient",
                table: "Patients",
                column: "Mobile",
                filter: "[Mobile] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PatientNo",
                schema: "patient",
                table: "Patients",
                column: "PatientNo")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedAtUtc",
                schema: "lab",
                table: "Orders",
                column: "CreatedAtUtc")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNo",
                schema: "lab",
                table: "Orders",
                column: "OrderNo")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PatientId",
                schema: "lab",
                table: "Orders",
                column: "PatientId")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_IssuedAtUtc_Id",
                schema: "lab",
                table: "LabReports",
                columns: new[] { "IssuedAtUtc", "Id" },
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_PatientId",
                schema: "lab",
                table: "LabReports",
                column: "PatientId")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_ReportNo",
                schema: "lab",
                table: "LabReports",
                column: "ReportNo")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNo",
                schema: "billing",
                table: "Invoices",
                column: "InvoiceNo")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "BranchId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrderId",
                schema: "billing",
                table: "Invoices",
                column: "OrderId")
                .Annotation("SqlServer:Include", new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Org_EntityType_Action_AtUtc",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "OrganizationId", "EntityType", "Action", "AtUtc" },
                descending: new[] { false, false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TestResults_OrderId_Status",
                schema: "lab",
                table: "TestResults");

            migrationBuilder.DropIndex(
                name: "IX_TestResults_Status_ApprovedAtUtc_Id",
                schema: "lab",
                table: "TestResults");

            migrationBuilder.DropIndex(
                name: "IX_TestResults_Status_VerifiedAtUtc",
                schema: "lab",
                table: "TestResults");

            migrationBuilder.DropIndex(
                name: "IX_Samples_Barcode",
                schema: "lab",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_PatientId",
                schema: "lab",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_ReplacesSampleId",
                schema: "lab",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_Status_ReceivedAtUtc",
                schema: "lab",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReceivedAtUtc",
                schema: "billing",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Patients_Cnic",
                schema: "patient",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_CreatedAtUtc",
                schema: "patient",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_Mobile",
                schema: "patient",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_PatientNo",
                schema: "patient",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CreatedAtUtc",
                schema: "lab",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderNo",
                schema: "lab",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PatientId",
                schema: "lab",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_LabReports_IssuedAtUtc_Id",
                schema: "lab",
                table: "LabReports");

            migrationBuilder.DropIndex(
                name: "IX_LabReports_PatientId",
                schema: "lab",
                table: "LabReports");

            migrationBuilder.DropIndex(
                name: "IX_LabReports_ReportNo",
                schema: "lab",
                table: "LabReports");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNo",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_OrderId",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Org_EntityType_Action_AtUtc",
                schema: "audit",
                table: "AuditLogs");
        }
    }
}
