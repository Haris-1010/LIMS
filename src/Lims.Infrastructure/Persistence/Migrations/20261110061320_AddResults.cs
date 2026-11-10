using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestResults",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    SampleId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    OrderBranchId = table.Column<int>(type: "int", nullable: false),
                    LabBranchId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EnteredByUserId = table.Column<int>(type: "int", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    VerifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    AmendmentReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReplacesResultId = table.Column<long>(type: "bigint", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestResults", x => x.Id);
                    table.UniqueConstraint("AK_TestResults_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_TestResults_Approved", "[Status] < 30 OR ([ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_TestResults_DraftClean", "[Status] <> 0 OR ([VerifiedByUserId] IS NULL AND [ApprovedByUserId] IS NULL)");
                    table.CheckConstraint("CK_TestResults_Status", "[Status] IN (0,10,20,30,90)");
                    table.CheckConstraint("CK_TestResults_Submitted", "[Status] = 0 OR ([EnteredByUserId] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_TestResults_Verified", "[Status] < 20 OR ([VerifiedByUserId] IS NOT NULL AND [VerifiedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_TestResults_Version", "[Version] >= 1 AND ([Version] = 1 OR [AmendmentReason] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TestResults_Branches_OrganizationId_LabBranchId",
                        columns: x => new { x.OrganizationId, x.LabBranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestResults_Branches_OrganizationId_OrderBranchId",
                        columns: x => new { x.OrganizationId, x.OrderBranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestResults_OrderItems_OrganizationId_OrderItemId",
                        columns: x => new { x.OrganizationId, x.OrderItemId },
                        principalSchema: "lab",
                        principalTable: "OrderItems",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestResults_Orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "lab",
                        principalTable: "Orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestResults_Patients_OrganizationId_PatientId",
                        columns: x => new { x.OrganizationId, x.PatientId },
                        principalSchema: "patient",
                        principalTable: "Patients",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestResults_Samples_OrganizationId_SampleId",
                        columns: x => new { x.OrganizationId, x.SampleId },
                        principalSchema: "lab",
                        principalTable: "Samples",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestResults_TestResults_OrganizationId_ReplacesResultId",
                        columns: x => new { x.OrganizationId, x.ReplacesResultId },
                        principalSchema: "lab",
                        principalTable: "TestResults",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Worklists",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    WorklistNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Worklists", x => x.Id);
                    table.UniqueConstraint("AK_Worklists_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Worklists_Closed", "[Status] = 0 OR [ClosedAtUtc] IS NOT NULL");
                    table.CheckConstraint("CK_Worklists_Status", "[Status] IN (0,1)");
                    table.ForeignKey(
                        name: "FK_Worklists_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Worklists_Departments_OrganizationId_DepartmentId",
                        columns: x => new { x.OrganizationId, x.DepartmentId },
                        principalSchema: "core",
                        principalTable: "Departments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CriticalValueAlerts",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    ResultId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    LabBranchId = table.Column<int>(type: "int", nullable: false),
                    ParameterId = table.Column<int>(type: "int", nullable: false),
                    ValueText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Limit = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    IsHigh = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    NotifiedTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Method = table.Column<int>(type: "int", nullable: true),
                    ReadBackConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    NotifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    NotifiedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriticalValueAlerts", x => x.Id);
                    table.CheckConstraint("CK_CriticalValueAlerts_Method", "[Method] IS NULL OR [Method] IN (1,2,3)");
                    table.CheckConstraint("CK_CriticalValueAlerts_Notified", "[Status] = 0 OR ([NotifiedTo] IS NOT NULL AND [Method] IS NOT NULL AND [NotifiedByUserId] IS NOT NULL AND [NotifiedAtUtc] IS NOT NULL AND [ReadBackConfirmed] = 1)");
                    table.CheckConstraint("CK_CriticalValueAlerts_Status", "[Status] IN (0,1)");
                    table.ForeignKey(
                        name: "FK_CriticalValueAlerts_Branches_OrganizationId_LabBranchId",
                        columns: x => new { x.OrganizationId, x.LabBranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriticalValueAlerts_Patients_OrganizationId_PatientId",
                        columns: x => new { x.OrganizationId, x.PatientId },
                        principalSchema: "patient",
                        principalTable: "Patients",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriticalValueAlerts_TestResults_OrganizationId_ResultId",
                        columns: x => new { x.OrganizationId, x.ResultId },
                        principalSchema: "lab",
                        principalTable: "TestResults",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultApprovals",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    ResultId = table.Column<long>(type: "bigint", nullable: false),
                    Step = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultApprovals_TestResults_OrganizationId_ResultId",
                        columns: x => new { x.OrganizationId, x.ResultId },
                        principalSchema: "lab",
                        principalTable: "TestResults",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestResultValues",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    ResultId = table.Column<long>(type: "bigint", nullable: false),
                    ParameterId = table.Column<int>(type: "int", nullable: false),
                    ValueNumeric = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    OptionId = table.Column<int>(type: "int", nullable: true),
                    ValueText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ValueTextUr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UnitText = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    Flag = table.Column<int>(type: "int", nullable: false),
                    RangeId = table.Column<int>(type: "int", nullable: true),
                    RangeLow = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    RangeHigh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    RangeCriticalLow = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    RangeCriticalHigh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    RangeText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestResultValues", x => x.Id);
                    table.CheckConstraint("CK_TestResultValues_Decimals", "[DecimalPlaces] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_TestResultValues_Flag", "[Flag] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_TestResultValues_OneKind", "([ValueNumeric] IS NOT NULL AND [OptionId] IS NULL AND [ValueText] IS NULL) OR ([OptionId] IS NOT NULL AND [ValueNumeric] IS NULL) OR ([ValueNumeric] IS NULL AND [OptionId] IS NULL AND [ValueText] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TestResultValues_ParameterOptions_OptionId",
                        column: x => x.OptionId,
                        principalSchema: "catalog",
                        principalTable: "ParameterOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestResultValues_ReferenceRanges_RangeId",
                        column: x => x.RangeId,
                        principalSchema: "catalog",
                        principalTable: "ReferenceRanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestResultValues_TestParameters_OrganizationId_ParameterId",
                        columns: x => new { x.OrganizationId, x.ParameterId },
                        principalSchema: "catalog",
                        principalTable: "TestParameters",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestResultValues_TestResults_OrganizationId_ResultId",
                        columns: x => new { x.OrganizationId, x.ResultId },
                        principalSchema: "lab",
                        principalTable: "TestResults",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorklistItems",
                schema: "lab",
                columns: table => new
                {
                    WorklistId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorklistItems", x => new { x.WorklistId, x.OrderItemId });
                    table.ForeignKey(
                        name: "FK_WorklistItems_OrderItems_OrganizationId_OrderItemId",
                        columns: x => new { x.OrganizationId, x.OrderItemId },
                        principalSchema: "lab",
                        principalTable: "OrderItems",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorklistItems_Worklists_OrganizationId_WorklistId",
                        columns: x => new { x.OrganizationId, x.WorklistId },
                        principalSchema: "lab",
                        principalTable: "Worklists",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalValueAlerts_OrganizationId_LabBranchId",
                schema: "lab",
                table: "CriticalValueAlerts",
                columns: new[] { "OrganizationId", "LabBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalValueAlerts_OrganizationId_PatientId",
                schema: "lab",
                table: "CriticalValueAlerts",
                columns: new[] { "OrganizationId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalValueAlerts_OrganizationId_ResultId",
                schema: "lab",
                table: "CriticalValueAlerts",
                columns: new[] { "OrganizationId", "ResultId" });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalValueAlerts_OrganizationId_Status_LabBranchId",
                schema: "lab",
                table: "CriticalValueAlerts",
                columns: new[] { "OrganizationId", "Status", "LabBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalValueAlerts_ResultId_ParameterId",
                schema: "lab",
                table: "CriticalValueAlerts",
                columns: new[] { "ResultId", "ParameterId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultApprovals_OrganizationId_ResultId",
                schema: "lab",
                table: "ResultApprovals",
                columns: new[] { "OrganizationId", "ResultId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultApprovals_ResultId_Id",
                schema: "lab",
                table: "ResultApprovals",
                columns: new[] { "ResultId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_OrderItemId_Version",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "OrderItemId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_OrganizationId_LabBranchId",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "OrganizationId", "LabBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_OrganizationId_OrderBranchId",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "OrganizationId", "OrderBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_OrganizationId_OrderId",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_OrganizationId_OrderItemId",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "OrganizationId", "OrderItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_OrganizationId_PatientId",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "OrganizationId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_OrganizationId_ReplacesResultId",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "OrganizationId", "ReplacesResultId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_OrganizationId_SampleId",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "OrganizationId", "SampleId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_OrganizationId_Status_LabBranchId",
                schema: "lab",
                table: "TestResults",
                columns: new[] { "OrganizationId", "Status", "LabBranchId" });

            migrationBuilder.CreateIndex(
                name: "UX_TestResults_OneInProgress",
                schema: "lab",
                table: "TestResults",
                column: "OrderItemId",
                unique: true,
                filter: "[Status] < 30");

            migrationBuilder.CreateIndex(
                name: "UX_TestResults_OneReleased",
                schema: "lab",
                table: "TestResults",
                column: "OrderItemId",
                unique: true,
                filter: "[Status] = 30");

            migrationBuilder.CreateIndex(
                name: "IX_TestResultValues_OptionId",
                schema: "lab",
                table: "TestResultValues",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "IX_TestResultValues_OrganizationId_ParameterId",
                schema: "lab",
                table: "TestResultValues",
                columns: new[] { "OrganizationId", "ParameterId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResultValues_OrganizationId_ResultId",
                schema: "lab",
                table: "TestResultValues",
                columns: new[] { "OrganizationId", "ResultId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResultValues_RangeId",
                schema: "lab",
                table: "TestResultValues",
                column: "RangeId");

            migrationBuilder.CreateIndex(
                name: "IX_TestResultValues_ResultId_ParameterId",
                schema: "lab",
                table: "TestResultValues",
                columns: new[] { "ResultId", "ParameterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorklistItems_OrganizationId_OrderItemId",
                schema: "lab",
                table: "WorklistItems",
                columns: new[] { "OrganizationId", "OrderItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorklistItems_OrganizationId_WorklistId",
                schema: "lab",
                table: "WorklistItems",
                columns: new[] { "OrganizationId", "WorklistId" });

            migrationBuilder.CreateIndex(
                name: "UX_WorklistItems_OneOpen",
                schema: "lab",
                table: "WorklistItems",
                column: "OrderItemId",
                unique: true,
                filter: "[IsOpen] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Worklists_OrganizationId_BranchId_Status",
                schema: "lab",
                table: "Worklists",
                columns: new[] { "OrganizationId", "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Worklists_OrganizationId_DepartmentId",
                schema: "lab",
                table: "Worklists",
                columns: new[] { "OrganizationId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Worklists_OrganizationId_WorklistNo",
                schema: "lab",
                table: "Worklists",
                columns: new[] { "OrganizationId", "WorklistNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CriticalValueAlerts",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "ResultApprovals",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "TestResultValues",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "WorklistItems",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "TestResults",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "Worklists",
                schema: "lab");
        }
    }
}
