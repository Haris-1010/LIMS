using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestCategories",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCategories", x => x.Id);
                    table.UniqueConstraint("AK_TestCategories_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_TestCategories_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestPackages",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestPackages", x => x.Id);
                    table.UniqueConstraint("AK_TestPackages_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_TestPackages_Price", "[Price] >= 0");
                    table.ForeignKey(
                        name: "FK_TestPackages_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tests",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShortName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    SampleTypeId = table.Column<int>(type: "int", nullable: false),
                    SampleVolume = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CollectionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Method = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TurnaroundHours = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReportNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    AvailableAllBranches = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tests", x => x.Id);
                    table.UniqueConstraint("AK_Tests_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Tests_Price", "[Price] >= 0");
                    table.CheckConstraint("CK_Tests_Turnaround", "[TurnaroundHours] >= 0");
                    table.ForeignKey(
                        name: "FK_Tests_Departments_OrganizationId_DepartmentId",
                        columns: x => new { x.OrganizationId, x.DepartmentId },
                        principalSchema: "core",
                        principalTable: "Departments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tests_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tests_SampleTypes_SampleTypeId",
                        column: x => x.SampleTypeId,
                        principalSchema: "catalog",
                        principalTable: "SampleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tests_TestCategories_OrganizationId_CategoryId",
                        columns: x => new { x.OrganizationId, x.CategoryId },
                        principalSchema: "catalog",
                        principalTable: "TestCategories",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageTests",
                schema: "catalog",
                columns: table => new
                {
                    PackageId = table.Column<int>(type: "int", nullable: false),
                    TestId = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageTests", x => new { x.PackageId, x.TestId });
                    table.ForeignKey(
                        name: "FK_PackageTests_TestPackages_OrganizationId_PackageId",
                        columns: x => new { x.OrganizationId, x.PackageId },
                        principalSchema: "catalog",
                        principalTable: "TestPackages",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageTests_Tests_OrganizationId_TestId",
                        columns: x => new { x.OrganizationId, x.TestId },
                        principalSchema: "catalog",
                        principalTable: "Tests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestBranches",
                schema: "catalog",
                columns: table => new
                {
                    TestId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestBranches", x => new { x.TestId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_TestBranches_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestBranches_Tests_OrganizationId_TestId",
                        columns: x => new { x.OrganizationId, x.TestId },
                        principalSchema: "catalog",
                        principalTable: "Tests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestParameters",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    TestId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResultType = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestParameters", x => x.Id);
                    table.UniqueConstraint("AK_TestParameters_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_TestParameters_Decimals", "[DecimalPlaces] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_TestParameters_ResultType", "[ResultType] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_TestParameters_Tests_OrganizationId_TestId",
                        columns: x => new { x.OrganizationId, x.TestId },
                        principalSchema: "catalog",
                        principalTable: "Tests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestParameters_Units_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "catalog",
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParameterOptions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    ParameterId = table.Column<int>(type: "int", nullable: false),
                    ValueEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValueUr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsAbnormal = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParameterOptions_TestParameters_OrganizationId_ParameterId",
                        columns: x => new { x.OrganizationId, x.ParameterId },
                        principalSchema: "catalog",
                        principalTable: "TestParameters",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceRanges",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    ParameterId = table.Column<int>(type: "int", nullable: false),
                    Sex = table.Column<int>(type: "int", nullable: false),
                    AgeFromDays = table.Column<int>(type: "int", nullable: false),
                    AgeToDays = table.Column<int>(type: "int", nullable: true),
                    Low = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    High = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    CriticalLow = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    CriticalHigh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    DisplayText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReplacesRangeId = table.Column<int>(type: "int", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RetiredByUserId = table.Column<int>(type: "int", nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceRanges", x => x.Id);
                    table.CheckConstraint("CK_ReferenceRanges_Age", "[AgeFromDays] >= 0 AND ([AgeToDays] IS NULL OR [AgeToDays] > [AgeFromDays])");
                    table.CheckConstraint("CK_ReferenceRanges_Approval", "[Status] = 0 OR ([ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_ReferenceRanges_LowHigh", "[Low] IS NULL OR [High] IS NULL OR [Low] <= [High]");
                    table.CheckConstraint("CK_ReferenceRanges_Sex", "[Sex] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_ReferenceRanges_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_ReferenceRanges_ReferenceRanges_ReplacesRangeId",
                        column: x => x.ReplacesRangeId,
                        principalSchema: "catalog",
                        principalTable: "ReferenceRanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferenceRanges_TestParameters_OrganizationId_ParameterId",
                        columns: x => new { x.OrganizationId, x.ParameterId },
                        principalSchema: "catalog",
                        principalTable: "TestParameters",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageTests_OrganizationId_PackageId",
                schema: "catalog",
                table: "PackageTests",
                columns: new[] { "OrganizationId", "PackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageTests_OrganizationId_TestId",
                schema: "catalog",
                table: "PackageTests",
                columns: new[] { "OrganizationId", "TestId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterOptions_OrganizationId_ParameterId",
                schema: "catalog",
                table: "ParameterOptions",
                columns: new[] { "OrganizationId", "ParameterId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterOptions_ParameterId_ValueEn",
                schema: "catalog",
                table: "ParameterOptions",
                columns: new[] { "ParameterId", "ValueEn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceRanges_OrganizationId_ParameterId",
                schema: "catalog",
                table: "ReferenceRanges",
                columns: new[] { "OrganizationId", "ParameterId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceRanges_OrganizationId_Status",
                schema: "catalog",
                table: "ReferenceRanges",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceRanges_ParameterId_Status",
                schema: "catalog",
                table: "ReferenceRanges",
                columns: new[] { "ParameterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceRanges_ReplacesRangeId",
                schema: "catalog",
                table: "ReferenceRanges",
                column: "ReplacesRangeId");

            migrationBuilder.CreateIndex(
                name: "IX_TestBranches_OrganizationId_BranchId",
                schema: "catalog",
                table: "TestBranches",
                columns: new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestBranches_OrganizationId_TestId",
                schema: "catalog",
                table: "TestBranches",
                columns: new[] { "OrganizationId", "TestId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestCategories_OrganizationId_Code",
                schema: "catalog",
                table: "TestCategories",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestPackages_OrganizationId_Code",
                schema: "catalog",
                table: "TestPackages",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestParameters_OrganizationId_TestId",
                schema: "catalog",
                table: "TestParameters",
                columns: new[] { "OrganizationId", "TestId" });

            migrationBuilder.CreateIndex(
                name: "IX_TestParameters_TestId_Code",
                schema: "catalog",
                table: "TestParameters",
                columns: new[] { "TestId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestParameters_UnitId",
                schema: "catalog",
                table: "TestParameters",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Tests_OrganizationId_CategoryId",
                schema: "catalog",
                table: "Tests",
                columns: new[] { "OrganizationId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tests_OrganizationId_Code",
                schema: "catalog",
                table: "Tests",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tests_OrganizationId_DepartmentId",
                schema: "catalog",
                table: "Tests",
                columns: new[] { "OrganizationId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tests_SampleTypeId",
                schema: "catalog",
                table: "Tests",
                column: "SampleTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageTests",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ParameterOptions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ReferenceRanges",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "TestBranches",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "TestPackages",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "TestParameters",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Tests",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "TestCategories",
                schema: "catalog");
        }
    }
}
