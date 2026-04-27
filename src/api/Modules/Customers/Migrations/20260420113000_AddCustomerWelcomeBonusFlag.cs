using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.Customers.Migrations
{
    public partial class AddCustomerWelcomeBonusFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WelcomeBonusAwardedAt",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WelcomeBonusAwarded",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WelcomeBonusAwardedAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "WelcomeBonusAwarded",
                table: "Customers");
        }
    }
}
