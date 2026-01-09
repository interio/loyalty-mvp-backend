using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.RewardCatalog.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToRewardCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RewardProducts_RewardVendor_Sku",
                table: "RewardProducts");

            migrationBuilder.DropIndex(
                name: "IX_RewardProducts_RewardVendor_Sku_Gtin",
                table: "RewardProducts");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RewardProducts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_RewardProducts_TenantId_RewardVendor_Sku",
                table: "RewardProducts",
                columns: new[] { "TenantId", "RewardVendor", "Sku" },
                unique: true,
                filter: "\"Gtin\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RewardProducts_TenantId_RewardVendor_Sku_Gtin",
                table: "RewardProducts",
                columns: new[] { "TenantId", "RewardVendor", "Sku", "Gtin" },
                unique: true,
                filter: "\"Gtin\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RewardProducts_TenantId_RewardVendor_Sku",
                table: "RewardProducts");

            migrationBuilder.DropIndex(
                name: "IX_RewardProducts_TenantId_RewardVendor_Sku_Gtin",
                table: "RewardProducts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RewardProducts");

            migrationBuilder.CreateIndex(
                name: "IX_RewardProducts_RewardVendor_Sku",
                table: "RewardProducts",
                columns: new[] { "RewardVendor", "Sku" },
                unique: true,
                filter: "\"Gtin\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RewardProducts_RewardVendor_Sku_Gtin",
                table: "RewardProducts",
                columns: new[] { "RewardVendor", "Sku", "Gtin" },
                unique: true,
                filter: "\"Gtin\" IS NOT NULL");
        }
    }
}
