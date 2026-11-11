using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportBannerImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "FooterImage",
                schema: "lab",
                table: "ReportTemplates",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FooterImageContentType",
                schema: "lab",
                table: "ReportTemplates",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "HeaderImage",
                schema: "lab",
                table: "ReportTemplates",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeaderImageContentType",
                schema: "lab",
                table: "ReportTemplates",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReportTemplates_FooterImage",
                schema: "lab",
                table: "ReportTemplates",
                sql: "[FooterImage] IS NULL OR (DATALENGTH([FooterImage]) <= 614400 AND [FooterImageContentType] IN ('image/png','image/jpeg'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReportTemplates_HeaderImage",
                schema: "lab",
                table: "ReportTemplates",
                sql: "[HeaderImage] IS NULL OR (DATALENGTH([HeaderImage]) <= 614400 AND [HeaderImageContentType] IN ('image/png','image/jpeg'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ReportTemplates_FooterImage",
                schema: "lab",
                table: "ReportTemplates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReportTemplates_HeaderImage",
                schema: "lab",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "FooterImage",
                schema: "lab",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "FooterImageContentType",
                schema: "lab",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "HeaderImage",
                schema: "lab",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "HeaderImageContentType",
                schema: "lab",
                table: "ReportTemplates");
        }
    }
}
