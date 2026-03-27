using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Loyalty.Api.Modules.Products.Migrations;

[DbContext(typeof(ProductsDbContext))]
[Migration("20260327162000_MakeProductCostOptional")]
public class MakeProductCostOptional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "Cost",
            table: "Products",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: true,
            oldClrType: typeof(decimal),
            oldType: "numeric(18,2)",
            oldPrecision: 18,
            oldScale: 2);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Products"
            SET "Cost" = 0
            WHERE "Cost" IS NULL;
            """);

        migrationBuilder.AlterColumn<decimal>(
            name: "Cost",
            table: "Products",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(18,2)",
            oldPrecision: 18,
            oldScale: 2,
            oldNullable: true);
    }
}
