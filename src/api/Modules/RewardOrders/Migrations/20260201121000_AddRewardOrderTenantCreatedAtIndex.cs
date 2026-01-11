using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.RewardOrders.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardOrderTenantCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RewardOrders_TenantId_CreatedAt",
                table: "RewardOrders",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RewardOrders_TenantId_CreatedAt",
                table: "RewardOrders");
        }
    }
}
