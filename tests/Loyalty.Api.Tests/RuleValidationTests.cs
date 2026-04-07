using System;
using System.Collections.Generic;
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
        Assert.Throws<ArgumentNullException>(() => new SkuQuantityRule((string)null!, 1, 10));
        Assert.Throws<ArgumentNullException>(() => new SkuQuantityRule((IEnumerable<string>)null!, 1, 10));
        Assert.Throws<ArgumentException>(() => new SkuQuantityRule(Array.Empty<string>(), 1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkuQuantityRule("SKU", 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkuQuantityRule("SKU", 1, 0));
    }
}
