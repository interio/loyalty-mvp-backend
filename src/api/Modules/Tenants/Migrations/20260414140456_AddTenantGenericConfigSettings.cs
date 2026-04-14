using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.Tenants.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantGenericConfigSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantConfigSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfigName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ConfigValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantConfigSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantConfigSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigSettings_ConfigName",
                table: "TenantConfigSettings",
                column: "ConfigName",
                unique: true,
                filter: "\"TenantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigSettings_TenantId_ConfigName",
                table: "TenantConfigSettings",
                columns: new[] { "TenantId", "ConfigName" },
                unique: true,
                filter: "\"TenantId\" IS NOT NULL");

            migrationBuilder.Sql("""
                INSERT INTO "TenantConfigSettings" ("Id", "TenantId", "ConfigName", "ConfigValue", "CreatedAt", "UpdatedAt")
                VALUES ('00000000-0000-0000-0000-000000000001', NULL, 'currency', 'EUR', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT ("ConfigName") WHERE "TenantId" IS NULL DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantConfigSettings");
        }
    }
}
