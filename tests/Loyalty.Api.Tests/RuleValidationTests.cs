using System;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Xunit;

namespace Loyalty.Api.Tests;

public class RuleValidationTests
{
    [Fact]
    public void SpendRule_ValidatesConstructor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpendRule(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpendRule(10, 0));
    }

    [Fact]
    public void SkuQuantityRule_ValidatesConstructor()
    {
        Assert.Throws<ArgumentNullException>(() => new SkuQuantityRule(null!, 1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkuQuantityRule("SKU", 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkuQuantityRule("SKU", 1, 0));
    }
}
