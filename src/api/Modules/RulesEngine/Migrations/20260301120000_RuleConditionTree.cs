using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Api.Modules.RulesEngine.Migrations
{
    /// <inheritdoc />
    public partial class RuleConditionTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RootGroupId",
                table: "PointsRules",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RuleConditionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Logic = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleConditionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleConditionGroups_PointsRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "PointsRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RuleConditionGroups_RuleConditionGroups_ParentGroupId",
                        column: x => x.ParentGroupId,
                        principalTable: "RuleConditionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuleConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AttributeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Operator = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleConditions_RuleConditionGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "RuleConditionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuleConditionGroups_ParentGroupId",
                table: "RuleConditionGroups",
                column: "ParentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleConditionGroups_RuleId",
                table: "RuleConditionGroups",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleConditions_EntityCode_AttributeCode",
                table: "RuleConditions",
                columns: new[] { "EntityCode", "AttributeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_RuleConditions_GroupId",
                table: "RuleConditions",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_PointsRules_RuleConditionGroups_RootGroupId",
                table: "PointsRules",
                column: "RootGroupId",
                principalTable: "RuleConditionGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(
                """
                CREATE EXTENSION IF NOT EXISTS "pgcrypto";

                CREATE TEMP TABLE rule_condition_group_map(rule_id uuid, group_id uuid) ON COMMIT DROP;

                INSERT INTO rule_condition_group_map(rule_id, group_id)
                SELECT "Id", gen_random_uuid()
                FROM "PointsRules";

                INSERT INTO "RuleConditionGroups" ("Id", "RuleId", "ParentGroupId", "Logic", "SortOrder", "CreatedAt")
                SELECT map.group_id, map.rule_id, NULL, 'AND', 0, COALESCE(pr."CreatedAt", CURRENT_TIMESTAMP)
                FROM rule_condition_group_map map
                JOIN "PointsRules" pr ON pr."Id" = map.rule_id;

                UPDATE "PointsRules" pr
                SET "RootGroupId" = map.group_id
                FROM rule_condition_group_map map
                WHERE pr."Id" = map.rule_id;

                INSERT INTO "RuleConditions" ("Id", "GroupId", "EntityCode", "AttributeCode", "Operator", "ValueJson", "SortOrder", "CreatedAt")
                SELECT
                    gen_random_uuid(),
                    map.group_id,
                    'rule',
                    kv.key,
                    'eq',
                    kv.value,
                    ROW_NUMBER() OVER (PARTITION BY pr."Id" ORDER BY kv.key) - 1,
                    COALESCE(pr."CreatedAt", CURRENT_TIMESTAMP)
                FROM "PointsRules" pr
                JOIN rule_condition_group_map map ON map.rule_id = pr."Id"
                LEFT JOIN LATERAL jsonb_each(pr."Conditions") kv ON true
                WHERE kv.key IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "Conditions",
                table: "PointsRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Conditions",
                table: "PointsRules",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.DropForeignKey(
                name: "FK_PointsRules_RuleConditionGroups_RootGroupId",
                table: "PointsRules");

            migrationBuilder.DropTable(
                name: "RuleConditions");

            migrationBuilder.DropTable(
                name: "RuleConditionGroups");

            migrationBuilder.DropColumn(
                name: "RootGroupId",
                table: "PointsRules");
        }
    }
}
