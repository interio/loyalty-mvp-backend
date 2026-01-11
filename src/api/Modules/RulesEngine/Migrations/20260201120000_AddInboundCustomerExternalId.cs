using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.RulesEngine.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundCustomerExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerExternalId",
                table: "InboundDocuments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundDocuments_TenantId_CustomerExternalId",
                table: "InboundDocuments",
                columns: new[] { "TenantId", "CustomerExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundDocuments_TenantId_ReceivedAt",
                table: "InboundDocuments",
                columns: new[] { "TenantId", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InboundDocuments_TenantId_CustomerExternalId",
                table: "InboundDocuments");

            migrationBuilder.DropIndex(
                name: "IX_InboundDocuments_TenantId_ReceivedAt",
                table: "InboundDocuments");

            migrationBuilder.DropColumn(
                name: "CustomerExternalId",
                table: "InboundDocuments");
        }
    }
}
