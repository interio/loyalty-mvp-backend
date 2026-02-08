using System;
using System.Text.Json;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Xunit;

namespace Loyalty.Api.Tests;

public class ComplexRuleComparisonEngineTests
{
    [Fact]
    public void Compare_HandlesNumericAndBooleanEquality()
    {
        var rightNum = JsonDocument.Parse("10").RootElement;
        var rightBool = JsonDocument.Parse("true").RootElement;

        Assert.True(ComplexRuleComparisonEngine.Compare(10m, "eq", rightNum));
        Assert.True(ComplexRuleComparisonEngine.Compare("10", "eq", rightNum));
        Assert.True(ComplexRuleComparisonEngine.Compare(true, "eq", rightBool));
        Assert.False(ComplexRuleComparisonEngine.Compare(false, "eq", rightBool));
    }

    [Fact]
    public void Compare_HandlesContainsAndSetOperators()
    {
        var contains = JsonDocument.Parse("\"abc\"").RootElement;
        var inSet = JsonDocument.Parse("[\"EUR\",\"USD\"]").RootElement;

        Assert.True(ComplexRuleComparisonEngine.Compare("xyzAbc123", "contains", contains));
        Assert.True(ComplexRuleComparisonEngine.Compare("eur", "in", inSet));
        Assert.True(ComplexRuleComparisonEngine.Compare("gbp", "nin", inSet));
    }

    [Fact]
    public void Compare_HandlesDateComparisons()
    {
        var laterDate = JsonDocument.Parse("\"2024-01-01T00:00:00Z\"").RootElement;
        var earlierDate = JsonDocument.Parse("\"2022-01-01T00:00:00Z\"").RootElement;
        var left = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.False(ComplexRuleComparisonEngine.Compare(left, "gt", laterDate));
        Assert.True(ComplexRuleComparisonEngine.Compare(left, "gt", earlierDate));
        Assert.True(ComplexRuleComparisonEngine.Compare(left, "lte", laterDate));
    }
}
