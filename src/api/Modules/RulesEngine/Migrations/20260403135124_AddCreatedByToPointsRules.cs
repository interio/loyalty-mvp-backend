using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.RulesEngine.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByToPointsRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "PointsRules",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PointsRules",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PointsRules");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "PointsRules",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
