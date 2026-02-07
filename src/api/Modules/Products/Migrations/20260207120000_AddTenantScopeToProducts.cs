using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.Products.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantScopeToProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_DistributorId_Sku",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_DistributorId_Sku_Gtin",
                table: "Products");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Products",
                type: "uuid",
                nullable: true);

            // Legacy products had no tenant column.
            // Backfill from the first tenant row available in Tenants.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE first_tenant_id uuid;
                BEGIN
                    SELECT "Id"
                    INTO first_tenant_id
                    FROM "Tenants"
                    ORDER BY "Id"
                    LIMIT 1;

                    IF first_tenant_id IS NULL THEN
                        RAISE EXCEPTION 'Cannot backfill Products.TenantId: no rows found in Tenants.';
                    END IF;

                    UPDATE "Products"
                    SET "TenantId" = first_tenant_id
                    WHERE "TenantId" IS NULL;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Products",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_DistributorId_Sku",
                table: "Products",
                columns: new[] { "TenantId", "DistributorId", "Sku" },
                unique: true,
                filter: "\"Gtin\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_DistributorId_Sku_Gtin",
                table: "Products",
                columns: new[] { "TenantId", "DistributorId", "Sku", "Gtin" },
                unique: true,
                filter: "\"Gtin\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_DistributorId_Sku",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_DistributorId_Sku_Gtin",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_DistributorId_Sku",
                table: "Products",
                columns: new[] { "DistributorId", "Sku" },
                unique: true,
                filter: "\"Gtin\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_DistributorId_Sku_Gtin",
                table: "Products",
                columns: new[] { "DistributorId", "Sku", "Gtin" },
                unique: true,
                filter: "\"Gtin\" IS NOT NULL");
        }
    }
}
