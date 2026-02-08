using System;
using System.Linq;
using System.Text.Json;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Xunit;

namespace Loyalty.Api.Tests;

public class ComplexRuleCompilerTests
{
    [Fact]
    public void Compile_BuildsOrderedNodes_FromGroupsAndConditions()
    {
        var ruleId = Guid.NewGuid();
        var root = CreateGroup(ruleId, null, "AND", 0);
        var child = CreateGroup(ruleId, root.Id, "AND", 2);

        var compiled = ComplexRuleCompiler.Compile(
            root.Id,
            new[] { root, child },
            new[]
            {
                CreateCondition(root.Id, "invoice", "currency", "eq", "\"EUR\"", 1)
            });

        var rootNodes = compiled.NodesByGroup[root.Id];
        Assert.Equal(2, rootNodes.Count);
        Assert.False(rootNodes[0].IsGroup);
        Assert.Equal("currency", rootNodes[0].Condition?.AttributeCode);
        Assert.True(rootNodes[1].IsGroup);
        Assert.Equal(child.Id, rootNodes[1].GroupId);
    }

    [Fact]
    public void Compile_ExcludesRuleMetadataConditions()
    {
        var ruleId = Guid.NewGuid();
        var root = CreateGroup(ruleId, null, "AND", 0);

        var compiled = ComplexRuleCompiler.Compile(
            root.Id,
            new[] { root },
            new[]
            {
                CreateCondition(root.Id, "rule", "rewardPoints", "eq", "25", 0),
                CreateCondition(root.Id, "invoice", "currency", "eq", "\"EUR\"", 1)
            });

        var rootNodes = compiled.NodesByGroup[root.Id];
        Assert.Single(rootNodes);
        Assert.Equal("currency", rootNodes.Single().Condition?.AttributeCode);
    }

    [Fact]
    public void Compile_PropagatesLineScopedConditionFlags_ToAncestorGroups()
    {
        var ruleId = Guid.NewGuid();
        var root = CreateGroup(ruleId, null, "AND", 0);
        var child = CreateGroup(ruleId, root.Id, "AND", 0);

        var compiled = ComplexRuleCompiler.Compile(
            root.Id,
            new[] { root, child },
            new[]
            {
                CreateCondition(child.Id, "product", "sku", "eq", "\"SKU-1\"", 0)
            });

        Assert.Contains(child.Id, compiled.GroupsWithLineScopedConditions);
        Assert.Contains(root.Id, compiled.GroupsWithLineScopedConditions);
    }

    [Fact]
    public void Compile_UsesEvaluatorScope_ForCustomEntities()
    {
        var ruleId = Guid.NewGuid();
        var root = CreateGroup(ruleId, null, "AND", 0);
        var child = CreateGroup(ruleId, root.Id, "AND", 0);
        var evaluators = new ComplexRuleEntityEvaluatorRegistry(new[] { new DistributorEvaluator() });

        var compiled = ComplexRuleCompiler.Compile(
            root.Id,
            new[] { root, child },
            new[]
            {
                CreateCondition(child.Id, "distributor", "code", "eq", "\"D-1\"", 0)
            },
            evaluators);

        Assert.Contains(child.Id, compiled.GroupsWithLineScopedConditions);
        Assert.Contains(root.Id, compiled.GroupsWithLineScopedConditions);
    }

    private static RuleConditionGroup CreateGroup(Guid ruleId, Guid? parentGroupId, string logic, int sortOrder)
    {
        return new RuleConditionGroup
        {
            Id = Guid.NewGuid(),
            RuleId = ruleId,
            ParentGroupId = parentGroupId,
            Logic = logic,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static RuleCondition CreateCondition(
        Guid groupId,
        string entityCode,
        string attributeCode,
        string op,
        string jsonValue,
        int sortOrder)
    {
        return new RuleCondition
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            EntityCode = entityCode,
            AttributeCode = attributeCode,
            Operator = op,
            ValueJson = JsonDocument.Parse(jsonValue),
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class DistributorEvaluator : IComplexRuleEntityEvaluator
    {
        public string EntityCode => "distributor";

        public ComplexRuleEntityScope Scope => ComplexRuleEntityScope.InvoiceLine;

        public bool Evaluate(RuleCondition condition, ComplexRuleEvaluationContext context) => true;
    }
}
