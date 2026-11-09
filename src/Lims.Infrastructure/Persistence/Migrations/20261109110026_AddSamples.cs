using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_OrderItems_OrganizationId_Id",
                schema: "lab",
                table: "OrderItems",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateTable(
                name: "SampleRejectionReasons",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequiresNote = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleRejectionReasons", x => x.Id);
                    table.UniqueConstraint("AK_SampleRejectionReasons_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_SampleRejectionReasons_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SampleTransfers",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    TransferNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    FromBranchId = table.Column<int>(type: "int", nullable: false),
                    ToBranchId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    DispatchedByUserId = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CompletedByUserId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleTransfers", x => x.Id);
                    table.UniqueConstraint("AK_SampleTransfers_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_SampleTransfers_Branches", "[FromBranchId] <> [ToBranchId]");
                    table.CheckConstraint("CK_SampleTransfers_Status", "[Status] IN (0,1,2)");
                    table.ForeignKey(
                        name: "FK_SampleTransfers_Branches_OrganizationId_FromBranchId",
                        columns: x => new { x.OrganizationId, x.FromBranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SampleTransfers_Branches_OrganizationId_ToBranchId",
                        columns: x => new { x.OrganizationId, x.ToBranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Samples",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Barcode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    SampleTypeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    OriginBranchId = table.Column<int>(type: "int", nullable: false),
                    CurrentBranchId = table.Column<int>(type: "int", nullable: false),
                    DestinationBranchId = table.Column<int>(type: "int", nullable: true),
                    CollectedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CollectedByUserId = table.Column<int>(type: "int", nullable: true),
                    IdentityConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    CollectionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ReceivedByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RejectedByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectionReasonId = table.Column<int>(type: "int", nullable: true),
                    RejectionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReplacesSampleId = table.Column<long>(type: "bigint", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    LabelPrintCount = table.Column<int>(type: "int", nullable: false),
                    LastLabelPrintedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samples", x => x.Id);
                    table.UniqueConstraint("AK_Samples_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Samples_Collected", "[Status] IN (0,90) OR ([CollectedAtUtc] IS NOT NULL AND [CollectedByUserId] IS NOT NULL)");
                    table.CheckConstraint("CK_Samples_InTransit", "[Status] <> 15 OR [DestinationBranchId] IS NOT NULL");
                    table.CheckConstraint("CK_Samples_Priority", "[Priority] IN (0,1)");
                    table.CheckConstraint("CK_Samples_Received", "[Status] <> 20 OR ([ReceivedAtUtc] IS NOT NULL AND [ReceivedByUserId] IS NOT NULL)");
                    table.CheckConstraint("CK_Samples_Rejected", "[Status] <> 80 OR ([RejectedAtUtc] IS NOT NULL AND [RejectedByUserId] IS NOT NULL AND [RejectionReasonId] IS NOT NULL)");
                    table.CheckConstraint("CK_Samples_Status", "[Status] IN (0,10,15,20,80,90)");
                    table.ForeignKey(
                        name: "FK_Samples_Branches_OrganizationId_CurrentBranchId",
                        columns: x => new { x.OrganizationId, x.CurrentBranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_Branches_OrganizationId_DestinationBranchId",
                        columns: x => new { x.OrganizationId, x.DestinationBranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_Branches_OrganizationId_OriginBranchId",
                        columns: x => new { x.OrganizationId, x.OriginBranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_Orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "lab",
                        principalTable: "Orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_Patients_OrganizationId_PatientId",
                        columns: x => new { x.OrganizationId, x.PatientId },
                        principalSchema: "patient",
                        principalTable: "Patients",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_SampleRejectionReasons_OrganizationId_RejectionReasonId",
                        columns: x => new { x.OrganizationId, x.RejectionReasonId },
                        principalSchema: "lab",
                        principalTable: "SampleRejectionReasons",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_SampleTypes_SampleTypeId",
                        column: x => x.SampleTypeId,
                        principalSchema: "catalog",
                        principalTable: "SampleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_Samples_OrganizationId_ReplacesSampleId",
                        columns: x => new { x.OrganizationId, x.ReplacesSampleId },
                        principalSchema: "lab",
                        principalTable: "Samples",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SampleEvents",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    SampleId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SampleEvents_Samples_OrganizationId_SampleId",
                        columns: x => new { x.OrganizationId, x.SampleId },
                        principalSchema: "lab",
                        principalTable: "Samples",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SampleTests",
                schema: "lab",
                columns: table => new
                {
                    SampleId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleTests", x => new { x.SampleId, x.OrderItemId });
                    table.ForeignKey(
                        name: "FK_SampleTests_OrderItems_OrganizationId_OrderItemId",
                        columns: x => new { x.OrganizationId, x.OrderItemId },
                        principalSchema: "lab",
                        principalTable: "OrderItems",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SampleTests_Samples_OrganizationId_SampleId",
                        columns: x => new { x.OrganizationId, x.SampleId },
                        principalSchema: "lab",
                        principalTable: "Samples",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SampleTransferItems",
                schema: "lab",
                columns: table => new
                {
                    TransferId = table.Column<long>(type: "bigint", nullable: false),
                    SampleId = table.Column<long>(type: "bigint", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ReceivedByUserId = table.Column<int>(type: "int", nullable: true),
                    MarkedMissing = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleTransferItems", x => new { x.TransferId, x.SampleId });
                    table.ForeignKey(
                        name: "FK_SampleTransferItems_SampleTransfers_OrganizationId_TransferId",
                        columns: x => new { x.OrganizationId, x.TransferId },
                        principalSchema: "lab",
                        principalTable: "SampleTransfers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SampleTransferItems_Samples_OrganizationId_SampleId",
                        columns: x => new { x.OrganizationId, x.SampleId },
                        principalSchema: "lab",
                        principalTable: "Samples",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SampleEvents_OrganizationId_SampleId",
                schema: "lab",
                table: "SampleEvents",
                columns: new[] { "OrganizationId", "SampleId" });

            migrationBuilder.CreateIndex(
                name: "IX_SampleEvents_SampleId_Id",
                schema: "lab",
                table: "SampleEvents",
                columns: new[] { "SampleId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SampleRejectionReasons_OrganizationId_Code",
                schema: "lab",
                table: "SampleRejectionReasons",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrderId",
                schema: "lab",
                table: "Samples",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrganizationId_Barcode",
                schema: "lab",
                table: "Samples",
                columns: new[] { "OrganizationId", "Barcode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrganizationId_CurrentBranchId",
                schema: "lab",
                table: "Samples",
                columns: new[] { "OrganizationId", "CurrentBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrganizationId_DestinationBranchId",
                schema: "lab",
                table: "Samples",
                columns: new[] { "OrganizationId", "DestinationBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrganizationId_OrderId",
                schema: "lab",
                table: "Samples",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrganizationId_OriginBranchId",
                schema: "lab",
                table: "Samples",
                columns: new[] { "OrganizationId", "OriginBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrganizationId_PatientId",
                schema: "lab",
                table: "Samples",
                columns: new[] { "OrganizationId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrganizationId_RejectionReasonId",
                schema: "lab",
                table: "Samples",
                columns: new[] { "OrganizationId", "RejectionReasonId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrganizationId_ReplacesSampleId",
                schema: "lab",
                table: "Samples",
                columns: new[] { "OrganizationId", "ReplacesSampleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrganizationId_Status_CurrentBranchId",
                schema: "lab",
                table: "Samples",
                columns: new[] { "OrganizationId", "Status", "CurrentBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_SampleTypeId",
                schema: "lab",
                table: "Samples",
                column: "SampleTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_Samples_OneLivePerOrderType",
                schema: "lab",
                table: "Samples",
                columns: new[] { "OrderId", "SampleTypeId" },
                unique: true,
                filter: "[Status] <> 80 AND [Status] <> 90");

            migrationBuilder.CreateIndex(
                name: "IX_SampleTests_OrderItemId",
                schema: "lab",
                table: "SampleTests",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleTests_OrganizationId_OrderItemId",
                schema: "lab",
                table: "SampleTests",
                columns: new[] { "OrganizationId", "OrderItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_SampleTests_OrganizationId_SampleId",
                schema: "lab",
                table: "SampleTests",
                columns: new[] { "OrganizationId", "SampleId" });

            migrationBuilder.CreateIndex(
                name: "IX_SampleTransferItems_OrganizationId_SampleId",
                schema: "lab",
                table: "SampleTransferItems",
                columns: new[] { "OrganizationId", "SampleId" });

            migrationBuilder.CreateIndex(
                name: "IX_SampleTransferItems_OrganizationId_TransferId",
                schema: "lab",
                table: "SampleTransferItems",
                columns: new[] { "OrganizationId", "TransferId" });

            migrationBuilder.CreateIndex(
                name: "IX_SampleTransferItems_SampleId",
                schema: "lab",
                table: "SampleTransferItems",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleTransfers_OrganizationId_FromBranchId",
                schema: "lab",
                table: "SampleTransfers",
                columns: new[] { "OrganizationId", "FromBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_SampleTransfers_OrganizationId_Status_ToBranchId",
                schema: "lab",
                table: "SampleTransfers",
                columns: new[] { "OrganizationId", "Status", "ToBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_SampleTransfers_OrganizationId_ToBranchId",
                schema: "lab",
                table: "SampleTransfers",
                columns: new[] { "OrganizationId", "ToBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_SampleTransfers_OrganizationId_TransferNo",
                schema: "lab",
                table: "SampleTransfers",
                columns: new[] { "OrganizationId", "TransferNo" },
                unique: true);
            // Default operational rejection reasons for organisations that already exist (new ones get them at provisioning).
            // Editable by the laboratory afterwards; these are not clinical rules.
            var order = 0;
            foreach (var r in Lims.Domain.Samples.SampleRejectionReasonDefaults.All)
            {
                order += 10;
                migrationBuilder.Sql($@"
INSERT INTO lab.SampleRejectionReasons (OrganizationId, Code, NameEn, NameUr, RequiresNote, IsActive, SortOrder, CreatedAtUtc)
SELECT o.Id, '{r.Code}', N'{r.NameEn.Replace("'", "''")}', N'{r.NameUr.Replace("'", "''")}', {(r.RequiresNote ? 1 : 0)}, 1, {order}, SYSUTCDATETIME()
  FROM core.Organizations o
 WHERE NOT EXISTS (SELECT 1 FROM lab.SampleRejectionReasons x WHERE x.OrganizationId = o.Id AND x.Code = '{r.Code}');");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SampleEvents",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "SampleTests",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "SampleTransferItems",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "SampleTransfers",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "Samples",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "SampleRejectionReasons",
                schema: "lab");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_OrderItems_OrganizationId_Id",
                schema: "lab",
                table: "OrderItems");
        }
    }
}
