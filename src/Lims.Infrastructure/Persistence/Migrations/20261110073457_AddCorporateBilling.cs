using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Method",
                schema: "billing",
                table: "Payments");

            migrationBuilder.AddColumn<int>(
                name: "ContractId",
                schema: "lab",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CorporateClientId",
                schema: "lab",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractId",
                schema: "billing",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CorporateClientId",
                schema: "billing",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                schema: "billing",
                table: "Invoices",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CorporateClients",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AddressEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AddressUr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateClients", x => x.Id);
                    table.UniqueConstraint("AK_CorporateClients_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_CorporateClients_Credit", "[CreditLimit] IS NULL OR [CreditLimit] >= 0");
                    table.CheckConstraint("CK_CorporateClients_Terms", "[PaymentTermsDays] BETWEEN 0 AND 365");
                });

            migrationBuilder.CreateTable(
                name: "CorporateContracts",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateContracts", x => x.Id);
                    table.UniqueConstraint("AK_CorporateContracts_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_CorporateContracts_Dates", "[ValidTo] IS NULL OR [ValidTo] >= [ValidFrom]");
                    table.CheckConstraint("CK_CorporateContracts_Discount", "[DiscountPercent] >= 0 AND [DiscountPercent] <= 100");
                    table.ForeignKey(
                        name: "FK_CorporateContracts_CorporateClients_OrganizationId_ClientId",
                        columns: x => new { x.OrganizationId, x.ClientId },
                        principalSchema: "billing",
                        principalTable: "CorporateClients",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorporateReceipts",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    ReceiptNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Allocated = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ReceivedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateReceipts", x => x.Id);
                    table.UniqueConstraint("AK_CorporateReceipts_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_CorporateReceipts_Amount", "[Amount] > 0 AND [Allocated] >= 0 AND [Allocated] <= [Amount]");
                    table.CheckConstraint("CK_CorporateReceipts_Kind", "([Kind] = 1 AND [Method] BETWEEN 1 AND 5) OR ([Kind] = 2 AND [Method] IS NULL AND [Reason] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CorporateReceipts_CorporateClients_OrganizationId_ClientId",
                        columns: x => new { x.OrganizationId, x.ClientId },
                        principalSchema: "billing",
                        principalTable: "CorporateClients",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorporateContractPrices",
                schema: "billing",
                columns: table => new
                {
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    TestId = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateContractPrices", x => new { x.ContractId, x.TestId });
                    table.CheckConstraint("CK_CorporateContractPrices_Price", "[Price] >= 0");
                    table.ForeignKey(
                        name: "FK_CorporateContractPrices_CorporateContracts_OrganizationId_ContractId",
                        columns: x => new { x.OrganizationId, x.ContractId },
                        principalSchema: "billing",
                        principalTable: "CorporateContracts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorporateContractPrices_Tests_OrganizationId_TestId",
                        columns: x => new { x.OrganizationId, x.TestId },
                        principalSchema: "catalog",
                        principalTable: "Tests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorporateAllocations",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    AllocatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateAllocations", x => x.Id);
                    table.CheckConstraint("CK_CorporateAllocations_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_CorporateAllocations_CorporateReceipts_OrganizationId_ReceiptId",
                        columns: x => new { x.OrganizationId, x.ReceiptId },
                        principalSchema: "billing",
                        principalTable: "CorporateReceipts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorporateAllocations_Invoices_OrganizationId_InvoiceId",
                        columns: x => new { x.OrganizationId, x.InvoiceId },
                        principalSchema: "billing",
                        principalTable: "Invoices",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorporateAllocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "billing",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Method",
                schema: "billing",
                table: "Payments",
                sql: "[Method] BETWEEN 1 AND 6");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrganizationId_CorporateClientId",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "OrganizationId", "CorporateClientId" },
                filter: "[CorporateClientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateAllocations_InvoiceId",
                schema: "billing",
                table: "CorporateAllocations",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateAllocations_OrganizationId_InvoiceId",
                schema: "billing",
                table: "CorporateAllocations",
                columns: new[] { "OrganizationId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateAllocations_OrganizationId_ReceiptId",
                schema: "billing",
                table: "CorporateAllocations",
                columns: new[] { "OrganizationId", "ReceiptId" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateAllocations_PaymentId",
                schema: "billing",
                table: "CorporateAllocations",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateAllocations_ReceiptId",
                schema: "billing",
                table: "CorporateAllocations",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateClients_OrganizationId_Code",
                schema: "billing",
                table: "CorporateClients",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateContractPrices_OrganizationId_ContractId",
                schema: "billing",
                table: "CorporateContractPrices",
                columns: new[] { "OrganizationId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateContractPrices_OrganizationId_TestId",
                schema: "billing",
                table: "CorporateContractPrices",
                columns: new[] { "OrganizationId", "TestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateContracts_ClientId_IsActive",
                schema: "billing",
                table: "CorporateContracts",
                columns: new[] { "ClientId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateContracts_OrganizationId_ClientId",
                schema: "billing",
                table: "CorporateContracts",
                columns: new[] { "OrganizationId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateReceipts_ClientId_ReceivedAtUtc",
                schema: "billing",
                table: "CorporateReceipts",
                columns: new[] { "ClientId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateReceipts_OrganizationId_ClientId",
                schema: "billing",
                table: "CorporateReceipts",
                columns: new[] { "OrganizationId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateReceipts_OrganizationId_IdempotencyKey",
                schema: "billing",
                table: "CorporateReceipts",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateReceipts_OrganizationId_ReceiptNo",
                schema: "billing",
                table: "CorporateReceipts",
                columns: new[] { "OrganizationId", "ReceiptNo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_CorporateClients_OrganizationId_CorporateClientId",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "OrganizationId", "CorporateClientId" },
                principalSchema: "billing",
                principalTable: "CorporateClients",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
            // Corporate Billing Officers raise the credit orders, so the standard role needs orders.create (new organisations get it at provisioning).
            migrationBuilder.Sql(@"
INSERT INTO security.RolePermissions (OrganizationId, RoleId, PermissionId)
SELECT r.OrganizationId, r.Id, p.Id
  FROM security.Roles r
  JOIN security.Permissions p ON p.[Key] IN ('orders.create','catalog.view')
 WHERE r.Code = 'CORPBILLING' AND r.IsSystem = 1
   AND NOT EXISTS (SELECT 1 FROM security.RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_CorporateClients_OrganizationId_CorporateClientId",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "CorporateAllocations",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "CorporateContractPrices",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "CorporateReceipts",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "CorporateContracts",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "CorporateClients",
                schema: "billing");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Method",
                schema: "billing",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_OrganizationId_CorporateClientId",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ContractId",
                schema: "lab",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CorporateClientId",
                schema: "lab",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ContractId",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CorporateClientId",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DueDate",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Method",
                schema: "billing",
                table: "Payments",
                sql: "[Method] BETWEEN 1 AND 5");
        }
    }
}
