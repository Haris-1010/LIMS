using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "Suppliers",
                schema: "inventory",
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
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.UniqueConstraint("AK_Suppliers_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReceipts",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ReceiptNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    SupplierInvoiceNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceipts", x => x.Id);
                    table.UniqueConstraint("AK_PurchaseReceipts_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Suppliers_OrganizationId_SupplierId",
                        columns: x => new { x.OrganizationId, x.SupplierId },
                        principalSchema: "inventory",
                        principalTable: "Suppliers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockItems",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    UnitName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TracksExpiry = table.Column<bool>(type: "bit", nullable: false),
                    DefaultSupplierId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockItems", x => x.Id);
                    table.UniqueConstraint("AK_StockItems_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_StockItems_Kind", "[Kind] IN (1,2)");
                    table.CheckConstraint("CK_StockItems_Reorder", "[ReorderLevel] >= 0");
                    table.ForeignKey(
                        name: "FK_StockItems_Suppliers_OrganizationId_DefaultSupplierId",
                        columns: x => new { x.OrganizationId, x.DefaultSupplierId },
                        principalSchema: "inventory",
                        principalTable: "Suppliers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsumptionProfileLines",
                schema: "inventory",
                columns: table => new
                {
                    TestId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    QuantityPerTest = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumptionProfileLines", x => new { x.TestId, x.ItemId });
                    table.CheckConstraint("CK_ConsumptionProfileLines_Quantity", "[QuantityPerTest] > 0");
                    table.ForeignKey(
                        name: "FK_ConsumptionProfileLines_StockItems_OrganizationId_ItemId",
                        columns: x => new { x.OrganizationId, x.ItemId },
                        principalSchema: "inventory",
                        principalTable: "StockItems",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumptionProfileLines_Tests_OrganizationId_TestId",
                        columns: x => new { x.OrganizationId, x.TestId },
                        principalSchema: "catalog",
                        principalTable: "Tests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockBatches",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    QuantityOnHand = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBatches", x => x.Id);
                    table.UniqueConstraint("AK_StockBatches_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_StockBatches_NotNegative", "[QuantityOnHand] >= 0 OR [BatchNo] = 'UNBATCHED'");
                    table.ForeignKey(
                        name: "FK_StockBatches_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockBatches_StockItems_OrganizationId_ItemId",
                        columns: x => new { x.OrganizationId, x.ItemId },
                        principalSchema: "inventory",
                        principalTable: "StockItems",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockBatches_Suppliers_OrganizationId_SupplierId",
                        columns: x => new { x.OrganizationId, x.SupplierId },
                        principalSchema: "inventory",
                        principalTable: "Suppliers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockShortfalls",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "int", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockShortfalls", x => x.Id);
                    table.CheckConstraint("CK_StockShortfalls_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_StockShortfalls_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockShortfalls_StockItems_OrganizationId_ItemId",
                        columns: x => new { x.OrganizationId, x.ItemId },
                        principalSchema: "inventory",
                        principalTable: "StockItems",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseLines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BatchId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseLines", x => x.Id);
                    table.CheckConstraint("CK_PurchaseLines_Quantity", "[Quantity] > 0 AND ([UnitCost] IS NULL OR [UnitCost] >= 0)");
                    table.ForeignKey(
                        name: "FK_PurchaseLines_PurchaseReceipts_OrganizationId_ReceiptId",
                        columns: x => new { x.OrganizationId, x.ReceiptId },
                        principalSchema: "inventory",
                        principalTable: "PurchaseReceipts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseLines_StockBatches_OrganizationId_BatchId",
                        columns: x => new { x.OrganizationId, x.BatchId },
                        principalSchema: "inventory",
                        principalTable: "StockBatches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseLines_StockItems_OrganizationId_ItemId",
                        columns: x => new { x.OrganizationId, x.ItemId },
                        principalSchema: "inventory",
                        principalTable: "StockItems",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ConsumptionKey = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    PairedMovementId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_StockMovements_Direction", "([Type] IN (1,4,8) AND [Quantity] > 0) OR ([Type] IN (2,3,5,6,7) AND [Quantity] < 0)");
                    table.CheckConstraint("CK_StockMovements_Quantity", "[Quantity] <> 0");
                    table.CheckConstraint("CK_StockMovements_Type", "[Type] BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "FK_StockMovements_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockBatches_OrganizationId_BatchId",
                        columns: x => new { x.OrganizationId, x.BatchId },
                        principalSchema: "inventory",
                        principalTable: "StockBatches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockItems_OrganizationId_ItemId",
                        columns: x => new { x.OrganizationId, x.ItemId },
                        principalSchema: "inventory",
                        principalTable: "StockItems",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionProfileLines_OrganizationId_ItemId",
                schema: "inventory",
                table: "ConsumptionProfileLines",
                columns: new[] { "OrganizationId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionProfileLines_OrganizationId_TestId",
                schema: "inventory",
                table: "ConsumptionProfileLines",
                columns: new[] { "OrganizationId", "TestId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseLines_OrganizationId_BatchId",
                schema: "inventory",
                table: "PurchaseLines",
                columns: new[] { "OrganizationId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseLines_OrganizationId_ItemId",
                schema: "inventory",
                table: "PurchaseLines",
                columns: new[] { "OrganizationId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseLines_OrganizationId_ReceiptId",
                schema: "inventory",
                table: "PurchaseLines",
                columns: new[] { "OrganizationId", "ReceiptId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_OrganizationId_BranchId",
                schema: "inventory",
                table: "PurchaseReceipts",
                columns: new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_OrganizationId_IdempotencyKey",
                schema: "inventory",
                table: "PurchaseReceipts",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_OrganizationId_ReceiptNo",
                schema: "inventory",
                table: "PurchaseReceipts",
                columns: new[] { "OrganizationId", "ReceiptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_OrganizationId_SupplierId",
                schema: "inventory",
                table: "PurchaseReceipts",
                columns: new[] { "OrganizationId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_ItemId_BranchId_BatchNo",
                schema: "inventory",
                table: "StockBatches",
                columns: new[] { "ItemId", "BranchId", "BatchNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_OrganizationId_BranchId_ExpiryDate",
                schema: "inventory",
                table: "StockBatches",
                columns: new[] { "OrganizationId", "BranchId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_OrganizationId_ItemId",
                schema: "inventory",
                table: "StockBatches",
                columns: new[] { "OrganizationId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_OrganizationId_SupplierId",
                schema: "inventory",
                table: "StockBatches",
                columns: new[] { "OrganizationId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_OrganizationId_Code",
                schema: "inventory",
                table: "StockItems",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_OrganizationId_DefaultSupplierId",
                schema: "inventory",
                table: "StockItems",
                columns: new[] { "OrganizationId", "DefaultSupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId_AtUtc",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "ItemId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrganizationId_BatchId",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "OrganizationId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrganizationId_BranchId_AtUtc",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "OrganizationId", "BranchId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrganizationId_ConsumptionKey",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "OrganizationId", "ConsumptionKey" },
                unique: true,
                filter: "[ConsumptionKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrganizationId_ItemId",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "OrganizationId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockShortfalls_OrganizationId_BranchId_ResolvedAtUtc",
                schema: "inventory",
                table: "StockShortfalls",
                columns: new[] { "OrganizationId", "BranchId", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StockShortfalls_OrganizationId_ItemId",
                schema: "inventory",
                table: "StockShortfalls",
                columns: new[] { "OrganizationId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_OrganizationId_Code",
                schema: "inventory",
                table: "Suppliers",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumptionProfileLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "PurchaseLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockMovements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockShortfalls",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "PurchaseReceipts",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockBatches",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockItems",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "inventory");
        }
    }
}
