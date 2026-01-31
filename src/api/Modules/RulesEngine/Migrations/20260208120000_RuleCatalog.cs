using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.RulesEngine.Migrations
{
    /// <inheritdoc />
    public partial class RuleCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuleEntities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValueType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsMultiValue = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsQueryable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UiControl = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleAttributes_RuleEntities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "RuleEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuleAttributeOperators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operator = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleAttributeOperators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleAttributeOperators_RuleAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "RuleAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuleAttributeOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleAttributeOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleAttributeOptions_RuleAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "RuleAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuleAttributeOperators_AttributeId_Operator",
                table: "RuleAttributeOperators",
                columns: new[] { "AttributeId", "Operator" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleAttributeOptions_AttributeId_Value",
                table: "RuleAttributeOptions",
                columns: new[] { "AttributeId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleAttributes_EntityId_Code",
                table: "RuleAttributes",
                columns: new[] { "EntityId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleAttributes_EntityId",
                table: "RuleAttributes",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleAttributeOperators_AttributeId",
                table: "RuleAttributeOperators",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleAttributeOptions_AttributeId",
                table: "RuleAttributeOptions",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleEntities_TenantId_Code",
                table: "RuleEntities",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleAttributeOptions");

            migrationBuilder.DropTable(
                name: "RuleAttributeOperators");

            migrationBuilder.DropTable(
                name: "RuleAttributes");

            migrationBuilder.DropTable(
                name: "RuleEntities");
        }
    }
}
