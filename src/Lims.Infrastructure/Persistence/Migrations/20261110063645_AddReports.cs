using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegistrationNo",
                schema: "security",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureContentType",
                schema: "security",
                table: "Users",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SignatureImage",
                schema: "security",
                table: "Users",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LabReports",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    OrderBranchId = table.Column<int>(type: "int", nullable: false),
                    LabBranchId = table.Column<int>(type: "int", nullable: false),
                    ReportNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false),
                    IssuedByUserId = table.Column<int>(type: "int", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    AmendmentReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReplacesReportId = table.Column<long>(type: "bigint", nullable: true),
                    ReleasedCount = table.Column<int>(type: "int", nullable: false),
                    ActiveTestCount = table.Column<int>(type: "int", nullable: false),
                    LatestApprovedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabReports", x => x.Id);
                    table.UniqueConstraint("AK_LabReports_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_LabReports_Counts", "[ReleasedCount] >= 1 AND [ActiveTestCount] >= [ReleasedCount] AND [PrintCount] >= 0");
                    table.CheckConstraint("CK_LabReports_Final", "[Status] <> 1 OR [ReleasedCount] = [ActiveTestCount]");
                    table.CheckConstraint("CK_LabReports_Hash", "LEN([SnapshotHash]) = 64 AND LEN([SnapshotJson]) > 2");
                    table.CheckConstraint("CK_LabReports_Language", "[Language] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_LabReports_Status", "[Status] IN (0,1,90)");
                    table.CheckConstraint("CK_LabReports_Version", "[Version] >= 1 AND ([Version] = 1 OR [AmendmentReason] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LabReports_Branches_OrganizationId_LabBranchId",
                        columns: x => new { x.OrganizationId, x.LabBranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabReports_Branches_OrganizationId_OrderBranchId",
                        columns: x => new { x.OrganizationId, x.OrderBranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabReports_LabReports_OrganizationId_ReplacesReportId",
                        columns: x => new { x.OrganizationId, x.ReplacesReportId },
                        principalSchema: "lab",
                        principalTable: "LabReports",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabReports_Orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "lab",
                        principalTable: "Orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabReports_Patients_OrganizationId_PatientId",
                        columns: x => new { x.OrganizationId, x.PatientId },
                        principalSchema: "patient",
                        principalTable: "Patients",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportTemplates",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    LogoImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    LogoContentType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportTemplates", x => x.Id);
                    table.UniqueConstraint("AK_ReportTemplates_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_ReportTemplates_Logo", "[LogoImage] IS NULL OR (DATALENGTH([LogoImage]) <= 307200 AND [LogoContentType] IN ('image/png','image/jpeg'))");
                    table.ForeignKey(
                        name: "FK_ReportTemplates_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportTemplateTranslations",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    HeaderNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FooterNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Disclaimer = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportTemplateTranslations", x => x.Id);
                    table.CheckConstraint("CK_ReportTemplateTranslations_Language", "[Language] IN ('en','ur')");
                    table.ForeignKey(
                        name: "FK_ReportTemplateTranslations_ReportTemplates_OrganizationId_TemplateId",
                        columns: x => new { x.OrganizationId, x.TemplateId },
                        principalSchema: "lab",
                        principalTable: "ReportTemplates",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "security",
                table: "Permissions",
                columns: new[] { "Id", "DescriptionEn", "DescriptionUr", "Key", "Module" },
                values: new object[,]
                {
                    { 49, "Issue (finalise) laboratory reports", "لیبارٹری رپورٹس جاری کریں", "reports.issue", "Reports" },
                    { 50, "Edit the report template and logo", "رپورٹ ٹیمپلیٹ اور لوگو میں ترمیم", "reports.templates", "Reports" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Signature",
                schema: "security",
                table: "Users",
                sql: "[SignatureImage] IS NULL OR (DATALENGTH([SignatureImage]) <= 204800 AND [SignatureContentType] IN ('image/png','image/jpeg'))");

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_OrderId_Version",
                schema: "lab",
                table: "LabReports",
                columns: new[] { "OrderId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_OrganizationId_LabBranchId",
                schema: "lab",
                table: "LabReports",
                columns: new[] { "OrganizationId", "LabBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_OrganizationId_OrderBranchId",
                schema: "lab",
                table: "LabReports",
                columns: new[] { "OrganizationId", "OrderBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_OrganizationId_OrderId",
                schema: "lab",
                table: "LabReports",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_OrganizationId_PatientId",
                schema: "lab",
                table: "LabReports",
                columns: new[] { "OrganizationId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_OrganizationId_ReplacesReportId",
                schema: "lab",
                table: "LabReports",
                columns: new[] { "OrganizationId", "ReplacesReportId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_OrganizationId_ReportNo",
                schema: "lab",
                table: "LabReports",
                columns: new[] { "OrganizationId", "ReportNo" });

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_OrganizationId_Status_LabBranchId",
                schema: "lab",
                table: "LabReports",
                columns: new[] { "OrganizationId", "Status", "LabBranchId" });

            migrationBuilder.CreateIndex(
                name: "UX_LabReports_OneLivePerOrder",
                schema: "lab",
                table: "LabReports",
                column: "OrderId",
                unique: true,
                filter: "[Status] <> 90");

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_OrganizationId",
                schema: "lab",
                table: "ReportTemplates",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplateTranslations_OrganizationId_TemplateId",
                schema: "lab",
                table: "ReportTemplateTranslations",
                columns: new[] { "OrganizationId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplateTranslations_TemplateId_Language",
                schema: "lab",
                table: "ReportTemplateTranslations",
                columns: new[] { "TemplateId", "Language" },
                unique: true);

            // Existing organisations: grant the two new permissions to the default roles (new organisations get them at provisioning).
            // Roles stay editable; this only gives the standard roles the same starting access as new organisations.
            migrationBuilder.Sql(@"
INSERT INTO security.RolePermissions (OrganizationId, RoleId, PermissionId)
SELECT r.OrganizationId, r.Id, p.Id
  FROM security.Roles r
  JOIN security.Permissions p ON (p.[Key] = 'reports.issue' AND r.Code IN ('SYSADMIN','LABMGR','PATHOLOGIST'))
                              OR (p.[Key] = 'reports.templates' AND r.Code IN ('SYSADMIN','LABMGR'))
 WHERE r.IsSystem = 1
   AND NOT EXISTS (SELECT 1 FROM security.RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabReports",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "ReportTemplateTranslations",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "ReportTemplates",
                schema: "lab");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Signature",
                schema: "security",
                table: "Users");

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                schema: "security",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DropColumn(
                name: "RegistrationNo",
                schema: "security",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SignatureContentType",
                schema: "security",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SignatureImage",
                schema: "security",
                table: "Users");
        }
    }
}
