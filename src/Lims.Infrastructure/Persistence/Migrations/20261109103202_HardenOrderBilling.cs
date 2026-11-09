using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenOrderBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Refunds_Method",
                schema: "billing",
                table: "Refunds",
                sql: "[Method] BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Priority",
                schema: "lab",
                table: "Orders",
                sql: "[Priority] IN (0,1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status",
                schema: "lab",
                table: "Orders",
                sql: "[Status] IN (0,1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderItems_Status",
                schema: "lab",
                table: "OrderItems",
                sql: "[Status] IN (0,10,20,30,40,90)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Refunds_Method",
                schema: "billing",
                table: "Refunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Priority",
                schema: "lab",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status",
                schema: "lab",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderItems_Status",
                schema: "lab",
                table: "OrderItems");
        }
    }
}
