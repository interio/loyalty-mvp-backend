using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;

#nullable disable

namespace Loyalty.Api.Modules.RulesEngine.Migrations
{
    [DbContext(typeof(IntegrationDbContext))]
    [Migration("20260310120000_EnsureRuleConditionCascadeDelete")]
    public partial class EnsureRuleConditionCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RuleConditions_RuleConditionGroups_GroupId",
                table: "RuleConditions");

            migrationBuilder.DropForeignKey(
                name: "FK_RuleConditionGroups_RuleConditionGroups_ParentGroupId",
                table: "RuleConditionGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_RuleConditionGroups_PointsRules_RuleId",
                table: "RuleConditionGroups");

            migrationBuilder.AddForeignKey(
                name: "FK_RuleConditionGroups_PointsRules_RuleId",
                table: "RuleConditionGroups",
                column: "RuleId",
                principalTable: "PointsRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RuleConditionGroups_RuleConditionGroups_ParentGroupId",
                table: "RuleConditionGroups",
                column: "ParentGroupId",
                principalTable: "RuleConditionGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RuleConditions_RuleConditionGroups_GroupId",
                table: "RuleConditions",
                column: "GroupId",
                principalTable: "RuleConditionGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: this migration enforces existing cascade semantics.
        }
    }
}
