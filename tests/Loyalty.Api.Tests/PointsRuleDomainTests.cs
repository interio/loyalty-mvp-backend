using System.Text.Json;
using System.Linq;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Xunit;

namespace Loyalty.Api.Tests;

public class PointsRuleDomainTests
{
    [Fact]
    public void ConditionEntries_MapsJsonValues()
    {
        var root = new RuleConditionGroup { Logic = "AND" };
        root.Conditions.Add(new RuleCondition
        {
            AttributeCode = "text",
            ValueJson = JsonDocument.Parse("\"a\""),
            SortOrder = 0
        });
        root.Conditions.Add(new RuleCondition
        {
            AttributeCode = "num",
            ValueJson = JsonDocument.Parse("2"),
            SortOrder = 1
        });
        root.Conditions.Add(new RuleCondition
        {
            AttributeCode = "bool",
            ValueJson = JsonDocument.Parse("true"),
            SortOrder = 2
        });
        root.Conditions.Add(new RuleCondition
        {
            AttributeCode = "null",
            ValueJson = JsonDocument.Parse("null"),
            SortOrder = 3
        });

        var rule = new PointsRule
        {
            RootGroup = root
        };

        var entries = rule.ConditionEntries;
        Assert.Equal("a", entries.Single(e => e.Key == "text").Value);
        Assert.Equal("2", entries.Single(e => e.Key == "num").Value);
        Assert.Equal("True", entries.Single(e => e.Key == "bool").Value);
        Assert.Null(entries.Single(e => e.Key == "null").Value);
    }

    [Fact]
    public void ConditionEntries_EmptyWhenNoRootGroup()
    {
        var rule = new PointsRule();
        Assert.Empty(rule.ConditionEntries);
    }
}
