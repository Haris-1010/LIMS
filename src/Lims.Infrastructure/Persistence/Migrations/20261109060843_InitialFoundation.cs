using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.EnsureSchema(
                name: "security");

            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginHistory",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UsernameAttempted = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AddressEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AddressUr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DescriptionUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SampleTypes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Container = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CapColour = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CS_AS"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AddressEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AddressUr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsMain = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.UniqueConstraint("AK_Branches_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Branches_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "core",
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
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.UniqueConstraint("AK_Departments_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Departments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.UniqueConstraint("AK_Roles_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Roles_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessSettings",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    Key = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessSettings_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessSettings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NumberSequences",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    SequenceType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Prefix = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Padding = table.Column<int>(type: "int", nullable: false),
                    ResetPolicy = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CurrentPeriod = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSequences", x => x.Id);
                    table.CheckConstraint("CK_NumberSequences_NextValue", "[NextValue] >= 1");
                    table.CheckConstraint("CK_NumberSequences_Padding", "[Padding] BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_NumberSequences_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NumberSequences_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayNameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayNameUr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Designation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PasswordHash = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: false),
                    SecurityStamp = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    PreferredLanguage = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    DefaultBranchId = table.Column<int>(type: "int", nullable: true),
                    AllBranches = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.UniqueConstraint("AK_Users_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Users_PreferredLanguage", "[PreferredLanguage] IN ('en','ur')");
                    table.ForeignKey(
                        name: "FK_Users_Branches_OrganizationId_DefaultBranchId",
                        columns: x => new { x.OrganizationId, x.DefaultBranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "security",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "security",
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_OrganizationId_RoleId",
                        columns: x => new { x.OrganizationId, x.RoleId },
                        principalSchema: "security",
                        principalTable: "Roles",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserBranches",
                schema: "security",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBranches", x => new { x.UserId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_UserBranches_Branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalSchema: "core",
                        principalTable: "Branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBranches_Users_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "security",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_OrganizationId_RoleId",
                        columns: x => new { x.OrganizationId, x.RoleId },
                        principalSchema: "security",
                        principalTable: "Roles",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "security",
                table: "Permissions",
                columns: new[] { "Id", "DescriptionEn", "DescriptionUr", "Key", "Module" },
                values: new object[,]
                {
                    { 1, "View dashboard and statistics", "ڈیش بورڈ اور اعداد و شمار دیکھیں", "dashboard.view", "Dashboard" },
                    { 2, "Search and view patients", "مریض تلاش کریں اور دیکھیں", "patients.view", "Patients" },
                    { 3, "Register patients", "مریضوں کا اندراج", "patients.create", "Patients" },
                    { 4, "Edit patient details", "مریض کی تفصیلات میں ترمیم", "patients.edit", "Patients" },
                    { 5, "View national ID and full contact details", "شناختی کارڈ اور مکمل رابطہ تفصیلات دیکھیں", "patients.viewSensitive", "Patients" },
                    { 6, "Merge duplicate patient records", "دہرے ریکارڈ یکجا کریں", "patients.merge", "Patients" },
                    { 7, "View doctors", "ڈاکٹرز دیکھیں", "doctors.view", "Patients" },
                    { 8, "Manage doctors and referrers", "ڈاکٹرز کا انتظام", "doctors.manage", "Patients" },
                    { 9, "View lab orders", "لیب آرڈرز دیکھیں", "orders.view", "Orders" },
                    { 10, "Create lab orders", "لیب آرڈر بنائیں", "orders.create", "Orders" },
                    { 11, "Cancel orders or tests", "آرڈر یا ٹیسٹ منسوخ کریں", "orders.cancel", "Orders" },
                    { 12, "View invoices", "انوائسز دیکھیں", "billing.view", "Billing" },
                    { 13, "Apply discounts", "رعایت دیں", "billing.discount", "Billing" },
                    { 14, "Issue refunds and reversals", "رقم کی واپسی اور منسوخی", "billing.refund", "Billing" },
                    { 15, "Collect payments and print receipts", "ادائیگی وصول کریں اور رسید پرنٹ کریں", "payments.collect", "Billing" },
                    { 16, "Close cash drawer and day", "کیش دراز اور دن بند کریں", "cash.close", "Billing" },
                    { 17, "View corporate clients and balances", "کارپوریٹ کلائنٹس اور بیلنس دیکھیں", "corporate.view", "Corporate" },
                    { 18, "Manage clients, contracts and price lists", "کلائنٹس، معاہدوں اور قیمتوں کا انتظام", "corporate.manage", "Corporate" },
                    { 19, "Generate corporate credit invoices", "کارپوریٹ ادھار انوائس بنائیں", "corporate.invoice", "Corporate" },
                    { 20, "Record corporate payments and allocations", "کارپوریٹ ادائیگیاں درج کریں", "corporate.receive", "Corporate" },
                    { 21, "Authorise credit adjustments", "ادھار میں ترمیم کی منظوری", "corporate.adjust", "Corporate" },
                    { 22, "View and track samples", "نمونے دیکھیں اور ٹریک کریں", "samples.view", "Samples" },
                    { 23, "Collect samples and print barcodes", "نمونے لیں اور بارکوڈ پرنٹ کریں", "samples.collect", "Samples" },
                    { 24, "Receive samples in the laboratory", "لیبارٹری میں نمونے وصول کریں", "samples.receive", "Samples" },
                    { 25, "Reject samples and request recollection", "نمونہ مسترد کریں اور دوبارہ طلب کریں", "samples.reject", "Samples" },
                    { 26, "Transfer samples between branches", "برانچز کے درمیان نمونہ منتقل کریں", "samples.transfer", "Samples" },
                    { 27, "View results", "نتائج دیکھیں", "results.view", "Results" },
                    { 28, "Enter and save results", "نتائج درج کریں", "results.enter", "Results" },
                    { 29, "Technically verify results", "نتائج کی تکنیکی تصدیق", "results.verify", "Results" },
                    { 30, "Approve and release results", "نتائج کی منظوری اور اجرا", "results.approve", "Results" },
                    { 31, "Amend approved results (with reason)", "منظور شدہ نتائج میں ترمیم (وجہ کے ساتھ)", "results.amend", "Results" },
                    { 32, "Handle critical value alerts", "تشویشناک نتائج کے الرٹس نمٹائیں", "results.critical", "Results" },
                    { 33, "View laboratory reports", "لیبارٹری رپورٹس دیکھیں", "reports.view", "Reports" },
                    { 34, "Print and export reports", "رپورٹس پرنٹ اور ایکسپورٹ کریں", "reports.print", "Reports" },
                    { 35, "View test catalog", "ٹیسٹ کیٹلاگ دیکھیں", "catalog.view", "Catalog" },
                    { 36, "Manage tests, parameters and packages", "ٹیسٹ، پیرامیٹرز اور پیکجز کا انتظام", "catalog.manage", "Catalog" },
                    { 37, "Approve reference ranges and critical limits", "ریفرنس رینجز اور تشویشناک حدود کی منظوری", "catalog.clinical", "Catalog" },
                    { 38, "Manage test prices", "ٹیسٹ کی قیمتوں کا انتظام", "catalog.pricing", "Catalog" },
                    { 39, "View stock and ledger", "اسٹاک اور لیجر دیکھیں", "inventory.view", "Inventory" },
                    { 40, "Manage items and suppliers", "اشیاء اور سپلائرز کا انتظام", "inventory.manage", "Inventory" },
                    { 41, "Record purchases and stock receipts", "خریداری اور اسٹاک کی وصولی درج کریں", "inventory.purchase", "Inventory" },
                    { 42, "Adjust and transfer stock", "اسٹاک میں ترمیم اور منتقلی", "inventory.adjust", "Inventory" },
                    { 43, "Manage organisation, branches and departments", "ادارہ، برانچز اور شعبہ جات کا انتظام", "org.manage", "Administration" },
                    { 44, "Manage users", "صارفین کا انتظام", "users.manage", "Administration" },
                    { 45, "Manage roles and permissions", "کردار اور اجازتوں کا انتظام", "roles.manage", "Administration" },
                    { 46, "View audit logs", "آڈٹ لاگز دیکھیں", "audit.view", "Administration" },
                    { 47, "Change system settings", "سسٹم سیٹنگز تبدیل کریں", "settings.manage", "Administration" },
                    { 48, "Run and restore backups", "بیک اپ اور بحالی", "backup.manage", "Administration" }
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "SampleTypes",
                columns: new[] { "Id", "CapColour", "Code", "Container", "IsActive", "NameEn", "NameUr" },
                values: new object[,]
                {
                    { 1, "Lavender", "WB-EDTA", "EDTA tube", true, "Whole blood (EDTA)", "خون (EDTA)" },
                    { 2, "Red / Yellow", "SERUM", "Clot activator / gel tube", true, "Serum", "سیرم" },
                    { 3, "Light blue", "PL-CIT", "Sodium citrate tube", true, "Plasma (sodium citrate)", "پلازما (سوڈیم سائٹریٹ)" },
                    { 4, "Grey", "PL-FLU", "Fluoride oxalate tube", true, "Plasma (sodium fluoride)", "پلازما (سوڈیم فلورائیڈ)" },
                    { 5, "Green", "PL-HEP", "Lithium heparin tube", true, "Plasma (heparin)", "پلازما (ہیپرین)" },
                    { 6, "Green", "WB-HEP", "Heparin tube / syringe", true, "Whole blood (heparin)", "خون (ہیپرین)" },
                    { 7, null, "URINE", "Sterile urine container", true, "Urine (random)", "پیشاب" },
                    { 8, null, "URINE-24H", "24-hour urine container", true, "Urine (24-hour)", "پیشاب (24 گھنٹے)" },
                    { 9, null, "STOOL", "Stool container", true, "Stool", "پاخانہ" },
                    { 10, null, "CSF", "Sterile tube", true, "Cerebrospinal fluid", "دماغی رطوبت (CSF)" },
                    { 11, null, "SPUTUM", "Sterile container", true, "Sputum", "بلغم" },
                    { 12, null, "SWAB", "Swab with transport medium", true, "Swab", "سواب" },
                    { 13, null, "SEMEN", "Sterile container", true, "Semen", "منی" },
                    { 14, null, "FLUID", "Sterile container", true, "Body fluid", "جسمانی رطوبت" },
                    { 15, null, "TISSUE", "Formalin container", true, "Tissue (biopsy)", "ٹشو (بائیوپسی)" },
                    { 16, null, "BLOOD-CULT", "Blood culture bottle", true, "Blood (culture)", "خون (کلچر)" }
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "Units",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "g/dL", true, "grams per decilitre" },
                    { 2, "g/L", true, "grams per litre" },
                    { 3, "mg/dL", true, "milligrams per decilitre" },
                    { 4, "mg/L", true, "milligrams per litre" },
                    { 5, "µg/dL", true, "micrograms per decilitre" },
                    { 6, "µg/L", true, "micrograms per litre" },
                    { 7, "ng/mL", true, "nanograms per millilitre" },
                    { 8, "ng/dL", true, "nanograms per decilitre" },
                    { 9, "pg/mL", true, "picograms per millilitre" },
                    { 10, "mmol/L", true, "millimoles per litre" },
                    { 11, "µmol/L", true, "micromoles per litre" },
                    { 12, "nmol/L", true, "nanomoles per litre" },
                    { 13, "pmol/L", true, "picomoles per litre" },
                    { 14, "mEq/L", true, "milliequivalents per litre" },
                    { 15, "U/L", true, "units per litre" },
                    { 16, "IU/L", true, "international units per litre" },
                    { 17, "mIU/mL", true, "milli-international units per millilitre" },
                    { 18, "µIU/mL", true, "micro-international units per millilitre" },
                    { 19, "IU/mL", true, "international units per millilitre" },
                    { 20, "%", true, "percent" },
                    { 21, "fL", true, "femtolitres" },
                    { 22, "pg", true, "picograms" },
                    { 23, "×10³/µL", true, "thousand per microlitre" },
                    { 24, "×10⁶/µL", true, "million per microlitre" },
                    { 25, "×10⁹/L", true, "10⁹ per litre" },
                    { 26, "×10¹²/L", true, "10¹² per litre" },
                    { 27, "cells/µL", true, "cells per microlitre" },
                    { 28, "/HPF", true, "per high-power field" },
                    { 29, "/LPF", true, "per low-power field" },
                    { 30, "mm/hr", true, "millimetres per hour" },
                    { 31, "sec", true, "seconds" },
                    { 32, "min", true, "minutes" },
                    { 33, "ratio", true, "ratio (dimensionless)" },
                    { 34, "mL/min/1.73m²", true, "millilitres per minute per 1.73 m²" },
                    { 35, "mOsm/kg", true, "milliosmoles per kilogram" },
                    { 36, "copies/mL", true, "copies per millilitre" },
                    { 37, "titre", true, "titre" },
                    { 38, "mL", true, "millilitres" },
                    { 39, "g/24h", true, "grams per 24 hours" },
                    { 40, "mg/24h", true, "milligrams per 24 hours" },
                    { 41, "S/CO", true, "signal to cut-off ratio" },
                    { 42, "COI", true, "cut-off index" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationId_AtUtc",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "OrganizationId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_OrganizationId_Code",
                schema: "core",
                table: "Branches",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Branches_OneMainPerOrganization",
                schema: "core",
                table: "Branches",
                column: "OrganizationId",
                unique: true,
                filter: "[IsMain] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessSettings_OrganizationId_BranchId_Key",
                schema: "core",
                table: "BusinessSettings",
                columns: new[] { "OrganizationId", "BranchId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_OrganizationId_Code",
                schema: "core",
                table: "Departments",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistory_AtUtc",
                schema: "security",
                table: "LoginHistory",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistory_UserId_AtUtc",
                schema: "security",
                table: "LoginHistory",
                columns: new[] { "UserId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_OrganizationId_BranchId_SequenceType",
                schema: "core",
                table: "NumberSequences",
                columns: new[] { "OrganizationId", "BranchId", "SequenceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Code",
                schema: "core",
                table: "Organizations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Key",
                schema: "security",
                table: "Permissions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_OrganizationId_RoleId",
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "OrganizationId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                schema: "security",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_OrganizationId_Code",
                schema: "security",
                table: "Roles",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SampleTypes_Code",
                schema: "catalog",
                table: "SampleTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_Code",
                schema: "catalog",
                table: "Units",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_BranchId",
                schema: "security",
                table: "UserBranches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_OrganizationId_BranchId",
                schema: "security",
                table: "UserBranches",
                columns: new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_OrganizationId_UserId",
                schema: "security",
                table: "UserBranches",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_OrganizationId_RoleId",
                schema: "security",
                table: "UserRoles",
                columns: new[] { "OrganizationId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_OrganizationId_UserId",
                schema: "security",
                table: "UserRoles",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "security",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId_DefaultBranchId",
                schema: "security",
                table: "Users",
                columns: new[] { "OrganizationId", "DefaultBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                schema: "security",
                table: "Users",
                column: "Username",
                unique: true);

            // Ids 1–999 are reserved for reference rows seeded by migrations; rows added by
            // administrators start at 1000 so a future seed can never collide with them.
            migrationBuilder.Sql("DBCC CHECKIDENT ('[catalog].[Units]', RESEED, 999);");
            migrationBuilder.Sql("DBCC CHECKIDENT ('[catalog].[SampleTypes]', RESEED, 999);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "BusinessSettings",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "core");

            migrationBuilder.DropTable(
                name: "LoginHistory",
                schema: "security");

            migrationBuilder.DropTable(
                name: "NumberSequences",
                schema: "core");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "security");

            migrationBuilder.DropTable(
                name: "SampleTypes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Units",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "UserBranches",
                schema: "security");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "security");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "security");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "security");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "security");

            migrationBuilder.DropTable(
                name: "Branches",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Organizations",
                schema: "core");
        }
    }
}
