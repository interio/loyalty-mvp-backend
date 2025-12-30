using System;
using System.Collections.Generic;
using System.Linq;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Xunit;

namespace Loyalty.Api.Tests;

public class RulesTests
{
    [Fact]
    public void SpendRule_ComputesSteps()
    {
        var rule = new SpendRule(100m, 10);
        var request = new InvoiceUpsertRequest
        {
            Lines = new List<InvoiceLineRequest>
            {
                new() { Sku = "A", Quantity = 1, NetAmount = 250m }
            }
        };

        var points = rule.CalculatePoints(request);
        Assert.Equal(20, points);
    }

    [Fact]
    public void SkuQuantityRule_ComputesQuantitySteps()
    {
        var rule = new SkuQuantityRule("SKU1", 4m, 25);
        var request = new InvoiceUpsertRequest
        {
            Lines = new List<InvoiceLineRequest>
            {
                new() { Sku = "SKU1", Quantity = 3m, NetAmount = 0 },
                new() { Sku = "SKU1", Quantity = 1m, NetAmount = 0 },
                new() { Sku = "OTHER", Quantity = 10m, NetAmount = 0 }
            }
        };

        var points = rule.CalculatePoints(request);
        Assert.Equal(25, points);
    }

    [Fact]
    public void HardcodedRulesProvider_AggregatesPoints()
    {
        var provider = new HardcodedRulesProvider();
        var request = new InvoiceUpsertRequest
        {
            Lines = new List<InvoiceLineRequest>
            {
                new() { Sku = "BEER-HEINEKEN-BTL-24PK", Quantity = 4m, NetAmount = 360m },
                new() { Sku = "OTHER", Quantity = 1m, NetAmount = 640m }
            }
        };

        var total = provider.GetRules().Sum(r => r.CalculatePoints(request));
        // Spend rule: floor((360+640)/100)*10 = 100 points. SKU rule: 25 points.
        Assert.Equal(125, total);
    }
}
