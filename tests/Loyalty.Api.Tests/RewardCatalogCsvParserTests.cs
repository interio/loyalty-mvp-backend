using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Loyalty.Api.Modules.RewardCatalog.Application;
using Xunit;

namespace Loyalty.Api.Tests;

public class RewardCatalogCsvParserTests
{
    [Fact]
    public async Task ParseAsync_MapsColumnsAndHandlesQuotes()
    {
        var csv = "rewardVendor,sku,name,pointsCost,inventoryQuantity\n" +
                  "\"Vendor A\",\"SKU-1\",\"Gift \"\"Card\"\"\",100,5\n";

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await RewardCatalogCsvParser.ParseAsync(stream);

        Assert.Single(result);
        Assert.Equal("Vendor A", result[0].RewardVendor);
        Assert.Equal("SKU-1", result[0].Sku);
        Assert.Equal("Gift \"Card\"", result[0].Name);
        Assert.Equal(100, result[0].PointsCost);
        Assert.Equal(5, result[0].InventoryQuantity);
    }

    [Fact]
    public async Task ParseAsync_AllowsOptionalColumns()
    {
        var csv = "vendor,sku,name,points_cost\nVendor A,SKU-2,Item,10\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await RewardCatalogCsvParser.ParseAsync(stream);

        Assert.Single(result);
        Assert.Null(result[0].InventoryQuantity);
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnInvalidValues()
    {
        var csv = "vendor,sku,name,points_cost\nVendor A,SKU-3,Item,abc\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => RewardCatalogCsvParser.ParseAsync(stream));
        Assert.Contains("Invalid integer", ex.Message);
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnMissingRequiredColumns()
    {
        var csv = "vendor,sku,points_cost\nVendor A,SKU-3,10\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => RewardCatalogCsvParser.ParseAsync(stream));
        Assert.Contains("Missing required column", ex.Message);
    }

    [Fact]
    public async Task ParseAsync_EmptyStreamReturnsEmptyList()
    {
        await using var stream = new MemoryStream();
        var result = await RewardCatalogCsvParser.ParseAsync(stream);
        Assert.Empty(result);
    }
}
