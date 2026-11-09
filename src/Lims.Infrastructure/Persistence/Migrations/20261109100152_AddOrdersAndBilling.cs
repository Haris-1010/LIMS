using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdersAndBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.EnsureSchema(
                name: "lab");

            migrationBuilder.CreateTable(
                name: "CashDrawerSessions",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    OpeningFloat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CashIn = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CashOut = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CountedCash = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Variance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashDrawerSessions", x => x.Id);
                    table.UniqueConstraint("AK_CashDrawerSessions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_CashDrawer_Closed", "[Status] = 0 OR ([ClosedAtUtc] IS NOT NULL AND [CountedCash] IS NOT NULL AND [ExpectedCash] IS NOT NULL)");
                    table.CheckConstraint("CK_CashDrawer_Float", "[OpeningFloat] >= 0");
                    table.ForeignKey(
                        name: "FK_CashDrawerSessions_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashDrawerSessions_Users_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OrderNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    ReferringDoctorId = table.Column<int>(type: "int", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ClinicalNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CancelledByUserId = table.Column<int>(type: "int", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.UniqueConstraint("AK_Orders_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Orders_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Doctors_OrganizationId_ReferringDoctorId",
                        columns: x => new { x.OrganizationId, x.ReferringDoctorId },
                        principalSchema: "patient",
                        principalTable: "Doctors",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Patients_OrganizationId_PatientId",
                        columns: x => new { x.OrganizationId, x.PatientId },
                        principalSchema: "patient",
                        principalTable: "Patients",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    Gross = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DiscountByUserId = table.Column<int>(type: "int", nullable: true),
                    Net = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Paid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Refunded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.UniqueConstraint("AK_Invoices_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Invoices_Amounts", "[Gross] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [Gross] AND [Net] = [Gross] - [DiscountAmount] AND [Paid] >= 0 AND [Refunded] >= 0");
                    table.CheckConstraint("CK_Invoices_Balance", "[Balance] = [Net] - ([Paid] - [Refunded])");
                    table.CheckConstraint("CK_Invoices_DiscountPercent", "[DiscountPercent] IS NULL OR ([DiscountPercent] > 0 AND [DiscountPercent] <= 100)");
                    table.ForeignKey(
                        name: "FK_Invoices_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "lab",
                        principalTable: "Orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Patients_OrganizationId_PatientId",
                        columns: x => new { x.OrganizationId, x.PatientId },
                        principalSchema: "patient",
                        principalTable: "Patients",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    TestId = table.Column<int>(type: "int", nullable: true),
                    PackageId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CancelledByUserId = table.Column<int>(type: "int", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.CheckConstraint("CK_InvoiceLines_Kind", "([Kind] = 1 AND [TestId] IS NOT NULL) OR ([Kind] = 2 AND [PackageId] IS NOT NULL)");
                    table.CheckConstraint("CK_InvoiceLines_Price", "[Price] >= 0");
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_OrganizationId_InvoiceId",
                        columns: x => new { x.OrganizationId, x.InvoiceId },
                        principalSchema: "billing",
                        principalTable: "Invoices",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_TestPackages_OrganizationId_PackageId",
                        columns: x => new { x.OrganizationId, x.PackageId },
                        principalSchema: "catalog",
                        principalTable: "TestPackages",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Tests_OrganizationId_TestId",
                        columns: x => new { x.OrganizationId, x.TestId },
                        principalSchema: "catalog",
                        principalTable: "Tests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ReceiptNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ReceivedByUserId = table.Column<int>(type: "int", nullable: false),
                    DrawerSessionId = table.Column<long>(type: "bigint", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Payments_CashDrawer", "[Method] <> 1 OR [DrawerSessionId] IS NOT NULL");
                    table.CheckConstraint("CK_Payments_Method", "[Method] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Payments_CashDrawerSessions_OrganizationId_DrawerSessionId",
                        columns: x => new { x.OrganizationId, x.DrawerSessionId },
                        principalSchema: "billing",
                        principalTable: "CashDrawerSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_OrganizationId_InvoiceId",
                        columns: x => new { x.OrganizationId, x.InvoiceId },
                        principalSchema: "billing",
                        principalTable: "Invoices",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    RefundNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RefundedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    RefundedByUserId = table.Column<int>(type: "int", nullable: false),
                    DrawerSessionId = table.Column<long>(type: "bigint", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.CheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Refunds_CashDrawer", "[Method] <> 1 OR [DrawerSessionId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Refunds_CashDrawerSessions_OrganizationId_DrawerSessionId",
                        columns: x => new { x.OrganizationId, x.DrawerSessionId },
                        principalSchema: "billing",
                        principalTable: "CashDrawerSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Invoices_OrganizationId_InvoiceId",
                        columns: x => new { x.OrganizationId, x.InvoiceId },
                        principalSchema: "billing",
                        principalTable: "Invoices",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                schema: "lab",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceLineId = table.Column<long>(type: "bigint", nullable: false),
                    TestId = table.Column<int>(type: "int", nullable: false),
                    TestCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TestNameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TestNameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CancelledByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_InvoiceLines_InvoiceLineId",
                        column: x => x.InvoiceLineId,
                        principalSchema: "billing",
                        principalTable: "InvoiceLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "lab",
                        principalTable: "Orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Tests_OrganizationId_TestId",
                        columns: x => new { x.OrganizationId, x.TestId },
                        principalSchema: "catalog",
                        principalTable: "Tests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerSessions_OrganizationId_UserId",
                schema: "billing",
                table: "CashDrawerSessions",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "UX_CashDrawer_OneOpenPerUser",
                schema: "billing",
                table: "CashDrawerSessions",
                columns: new[] { "OrganizationId", "BranchId", "UserId" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_OrganizationId_InvoiceId",
                schema: "billing",
                table: "InvoiceLines",
                columns: new[] { "OrganizationId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_OrganizationId_PackageId",
                schema: "billing",
                table: "InvoiceLines",
                columns: new[] { "OrganizationId", "PackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_OrganizationId_TestId",
                schema: "billing",
                table: "InvoiceLines",
                columns: new[] { "OrganizationId", "TestId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrganizationId_BranchId",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrganizationId_InvoiceNo",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "OrganizationId", "InvoiceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrganizationId_OrderId",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "OrganizationId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrganizationId_PatientId",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "OrganizationId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrganizationId_Status",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_InvoiceLineId",
                schema: "lab",
                table: "OrderItems",
                column: "InvoiceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId_TestId",
                schema: "lab",
                table: "OrderItems",
                columns: new[] { "OrderId", "TestId" },
                unique: true,
                filter: "[Status] <> 90");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrganizationId_OrderId",
                schema: "lab",
                table: "OrderItems",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrganizationId_Status",
                schema: "lab",
                table: "OrderItems",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrganizationId_TestId",
                schema: "lab",
                table: "OrderItems",
                columns: new[] { "OrganizationId", "TestId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId_BranchId_CreatedAtUtc",
                schema: "lab",
                table: "Orders",
                columns: new[] { "OrganizationId", "BranchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId_IdempotencyKey",
                schema: "lab",
                table: "Orders",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId_OrderNo",
                schema: "lab",
                table: "Orders",
                columns: new[] { "OrganizationId", "OrderNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId_PatientId",
                schema: "lab",
                table: "Orders",
                columns: new[] { "OrganizationId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId_ReferringDoctorId",
                schema: "lab",
                table: "Orders",
                columns: new[] { "OrganizationId", "ReferringDoctorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrganizationId_BranchId_ReceivedAtUtc",
                schema: "billing",
                table: "Payments",
                columns: new[] { "OrganizationId", "BranchId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrganizationId_DrawerSessionId",
                schema: "billing",
                table: "Payments",
                columns: new[] { "OrganizationId", "DrawerSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrganizationId_IdempotencyKey",
                schema: "billing",
                table: "Payments",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrganizationId_InvoiceId",
                schema: "billing",
                table: "Payments",
                columns: new[] { "OrganizationId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrganizationId_ReceiptNo",
                schema: "billing",
                table: "Payments",
                columns: new[] { "OrganizationId", "ReceiptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OrganizationId_DrawerSessionId",
                schema: "billing",
                table: "Refunds",
                columns: new[] { "OrganizationId", "DrawerSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OrganizationId_IdempotencyKey",
                schema: "billing",
                table: "Refunds",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OrganizationId_InvoiceId",
                schema: "billing",
                table: "Refunds",
                columns: new[] { "OrganizationId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OrganizationId_RefundNo",
                schema: "billing",
                table: "Refunds",
                columns: new[] { "OrganizationId", "RefundNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderItems",
                schema: "lab");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Refunds",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "InvoiceLines",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "CashDrawerSessions",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "lab");
        }
    }
}
