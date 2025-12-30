using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowUserExternalIdPerCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_ExternalId",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_CustomerId_ExternalId",
                table: "Users",
                columns: new[] { "TenantId", "CustomerId", "ExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_CustomerId_ExternalId",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_ExternalId",
                table: "Users",
                columns: new[] { "TenantId", "ExternalId" },
                unique: true);
        }
    }
}
