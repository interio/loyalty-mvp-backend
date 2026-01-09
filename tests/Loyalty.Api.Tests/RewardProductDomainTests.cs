using System.Linq;
using System.Text.Json.Nodes;
using Loyalty.Api.Modules.RewardCatalog.Domain;
using Xunit;

namespace Loyalty.Api.Tests;

public class RewardProductDomainTests
{
    [Fact]
    public void AttributeEntries_FormatsScalarValues()
    {
        var product = new RewardProduct
        {
            Attributes = new JsonObject
            {
                ["string"] = "value",
                ["int"] = 10,
                ["long"] = 12L,
                ["decimal"] = 1.5m,
                ["double"] = 2.5d,
                ["bool"] = true
            }
        };

        var entries = product.AttributeEntries;
        Assert.Equal("value", entries.Single(e => e.Key == "string").Value);
        Assert.Equal("10", entries.Single(e => e.Key == "int").Value);
        Assert.Equal("12", entries.Single(e => e.Key == "long").Value);
        Assert.Equal("1.5", entries.Single(e => e.Key == "decimal").Value);
        Assert.Equal("2.5", entries.Single(e => e.Key == "double").Value);
        Assert.Equal("True", entries.Single(e => e.Key == "bool").Value);
    }
}
