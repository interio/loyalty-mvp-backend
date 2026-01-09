using System.Text;

namespace Loyalty.Api.Modules.RewardCatalog.Application;

public static class RewardCatalogCsvParser
{
    public static async Task<List<RewardProductUpsertRequest>> ParseAsync(Stream stream, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);

        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null)
            return new List<RewardProductUpsertRequest>();

        var headers = SplitCsvLine(headerLine)
            .Select(h => h.Trim().ToLowerInvariant())
            .ToList();

        var results = new List<RewardProductUpsertRequest>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null || string.IsNullOrWhiteSpace(line))
                continue;

            var columns = SplitCsvLine(line);
            if (columns.Count == 0) continue;

            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count && i < columns.Count; i++)
                row[headers[i]] = string.IsNullOrWhiteSpace(columns[i]) ? null : columns[i].Trim();

            results.Add(new RewardProductUpsertRequest
            {
                TenantId = GetRequiredGuid(row, "tenantid", "tenant_id"),
                RewardVendor = GetRequired(row, "rewardvendor", "vendor"),
                Sku = GetRequired(row, "sku"),
                Name = GetRequired(row, "name"),
                Gtin = GetOptional(row, "gtin"),
                PointsCost = GetInt(row, "pointscost", "points_cost"),
                InventoryQuantity = GetOptionalInt(row, "inventoryquantity", "inventory", "quantity")
            });
        }

        return results;
    }

    private static string GetRequired(Dictionary<string, string?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }
        throw new ArgumentException($"Missing required column: {string.Join("/", keys)}");
    }

    private static string? GetOptional(Dictionary<string, string?> row, params string[] keys)
    {
        foreach (var key in keys)
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        return null;
    }

    private static int GetInt(Dictionary<string, string?> row, params string[] keys)
    {
        var value = GetRequired(row, keys);
        if (!int.TryParse(value, out var parsed))
            throw new ArgumentException($"Invalid integer for {string.Join("/", keys)}: {value}");
        return parsed;
    }

    private static Guid GetRequiredGuid(Dictionary<string, string?> row, params string[] keys)
    {
        var value = GetRequired(row, keys);
        if (!Guid.TryParse(value, out var parsed))
            throw new ArgumentException($"Invalid guid for {string.Join("/", keys)}: {value}");
        return parsed;
    }

    private static int? GetOptionalInt(Dictionary<string, string?> row, params string[] keys)
    {
        var value = GetOptional(row, keys);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!int.TryParse(value, out var parsed))
            throw new ArgumentException($"Invalid integer for {string.Join("/", keys)}: {value}");
        return parsed;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        result.Add(sb.ToString());
        return result;
    }
}
