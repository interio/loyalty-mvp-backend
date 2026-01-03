using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.RulesEngine.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceProcessingQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "InboundDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAt",
                table: "InboundDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundDocuments_Status_DocumentType",
                table: "InboundDocuments",
                columns: new[] { "Status", "DocumentType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InboundDocuments_Status_DocumentType",
                table: "InboundDocuments");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "InboundDocuments");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "InboundDocuments");
        }
    }
}
