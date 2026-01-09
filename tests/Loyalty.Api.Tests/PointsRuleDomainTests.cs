using System.Linq;
using System.Text.Json;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Xunit;

namespace Loyalty.Api.Tests;

public class PointsRuleDomainTests
{
    [Fact]
    public void ConditionEntries_MapsJsonValues()
    {
        var rule = new PointsRule
        {
            Conditions = JsonDocument.Parse("{\"text\":\"a\",\"num\":2,\"bool\":true,\"null\":null}")
        };

        var entries = rule.ConditionEntries;
        Assert.Equal("a", entries.Single(e => e.Key == "text").Value);
        Assert.Equal("2", entries.Single(e => e.Key == "num").Value);
        Assert.Equal("True", entries.Single(e => e.Key == "bool").Value);
        Assert.Null(entries.Single(e => e.Key == "null").Value);
    }

    [Fact]
    public void ConditionEntries_EmptyWhenNotObject()
    {
        var rule = new PointsRule
        {
            Conditions = JsonDocument.Parse("[1,2,3]")
        };

        Assert.Empty(rule.ConditionEntries);
    }
}
