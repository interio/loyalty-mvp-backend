using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.Products.Migrations
{
    /// <inheritdoc />
    public partial class AddDistributors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Distributors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Distributors", x => x.Id);
                    table.UniqueConstraint("AK_Distributors_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Distributors_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Distributors_TenantId_Name",
                table: "Distributors",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            // Backfill distributors for existing products before adding FK.
            migrationBuilder.Sql(
                """
                INSERT INTO "Distributors" ("Id", "TenantId", "Name", "DisplayName", "CreatedAt")
                SELECT p."DistributorId",
                       p."TenantId",
                       'dist-' || REPLACE(LEFT(p."DistributorId"::text, 12), '-', ''),
                       p."DistributorId"::text,
                       CURRENT_TIMESTAMP
                FROM (
                    SELECT DISTINCT "DistributorId", "TenantId"
                    FROM "Products"
                ) p
                LEFT JOIN "Distributors" d
                    ON d."Id" = p."DistributorId"
                   AND d."TenantId" = p."TenantId"
                WHERE d."Id" IS NULL;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Distributors_TenantId_DistributorId",
                table: "Products",
                columns: new[] { "TenantId", "DistributorId" },
                principalTable: "Distributors",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Distributors_TenantId_DistributorId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "Distributors");
        }
    }
}
