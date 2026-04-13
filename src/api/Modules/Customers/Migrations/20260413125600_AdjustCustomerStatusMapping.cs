using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.Customers.Migrations
{
    /// <inheritdoc />
    public partial class AdjustCustomerStatusMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Customers"
                SET "Status" = CASE
                    WHEN "Status" = 0 THEN 1
                    WHEN "Status" = 1 THEN 0
                    ELSE "Status"
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE "Customers"
                SET "Status" = CASE
                    WHEN "Status" = 0 THEN 1
                    WHEN "Status" = 1 THEN 0
                    ELSE "Status"
                END;
                """);
        }
    }
}
