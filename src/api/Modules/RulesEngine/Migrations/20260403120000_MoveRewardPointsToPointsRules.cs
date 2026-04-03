using Microsoft.EntityFrameworkCore.Migrations;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Loyalty.Api.Modules.RulesEngine.Migrations
{
    [DbContext(typeof(IntegrationDbContext))]
    [Migration("20260403120000_MoveRewardPointsToPointsRules")]
    public partial class MoveRewardPointsToPointsRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RewardPoints",
                table: "PointsRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "PointsRules" pr
                SET "RewardPoints" = COALESCE(
                    (
                        SELECT CASE
                            WHEN jsonb_typeof(rc."ValueJson") = 'number'
                                 AND (rc."ValueJson"::text ~ '^-?[0-9]+$')
                                THEN rc."ValueJson"::text::integer
                            WHEN jsonb_typeof(rc."ValueJson") = 'string'
                                 AND (trim(both '"' from rc."ValueJson"::text) ~ '^-?[0-9]+$')
                                THEN trim(both '"' from rc."ValueJson"::text)::integer
                            ELSE NULL
                        END
                        FROM "RuleConditionGroups" rg
                        INNER JOIN "RuleConditions" rc ON rc."GroupId" = rg."Id"
                        WHERE rg."RuleId" = pr."Id"
                          AND lower(rc."EntityCode") = 'rule'
                          AND lower(rc."AttributeCode") = 'rewardpoints'
                        ORDER BY
                            CASE WHEN rg."Id" = pr."RootGroupId" THEN 0 ELSE 1 END,
                            rg."SortOrder",
                            rc."SortOrder"
                        LIMIT 1
                    ),
                    "RewardPoints");
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "RuleConditions" rc
                USING "RuleConditionGroups" rg
                WHERE rc."GroupId" = rg."Id"
                  AND lower(rc."EntityCode") = 'rule'
                  AND lower(rc."AttributeCode") = 'rewardpoints';
                """);

            migrationBuilder.DropColumn(
                name: "RuleVersion",
                table: "PointsRules");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RuleVersion",
                table: "PointsRules",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.DropColumn(
                name: "RewardPoints",
                table: "PointsRules");
        }
    }
}
