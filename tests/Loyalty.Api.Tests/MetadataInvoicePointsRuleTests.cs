using System;
using System.Text.Json;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Xunit;

namespace Loyalty.Api.Tests;

public class MetadataInvoicePointsRuleTests
{
    [Fact]
    public void Wrapper_DelegatesToInner()
    {
        var inner = new SpendRule(100m, 10);
        var metadata = new InvoiceRuleMetadata(
            Guid.NewGuid(),
            "spend",
            0,
            true,
            DateTimeOffset.UtcNow,
            null,
            JsonDocument.Parse("{}"));

        var wrapper = new MetadataInvoicePointsRule(inner, metadata);

        var points = wrapper.CalculatePoints(new InvoiceUpsertRequest
        {
            Lines = new() { new InvoiceLineRequest { Sku = "A", Quantity = 1, NetAmount = 200m } }
        });

        Assert.Equal(inner.Name, wrapper.Name);
        Assert.Equal(20, points);
        Assert.Equal(metadata, wrapper.Metadata);
    }
}
