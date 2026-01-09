using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.RewardCatalog.Migrations
{
    /// <inheritdoc />
    public partial class RewardCatalogInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RewardInventories",
                columns: table => new
                {
                    RewardProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableQuantity = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardInventories", x => x.RewardProductId);
                });

            migrationBuilder.CreateTable(
                name: "RewardProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardVendor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sku = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Gtin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    PointsCost = table.Column<int>(type: "integer", nullable: false),
                    Attributes = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardProducts", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RewardInventories");

            migrationBuilder.DropTable(
                name: "RewardProducts");
        }
    }
}
