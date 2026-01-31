using Loyalty.Api.Modules.RulesEngine.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;

/// <summary>Integration context for inbound documents.</summary>
public class IntegrationDbContext : DbContext
{
    private static readonly JsonSerializerOptions JsonComparerOptions = new();

    public IntegrationDbContext(DbContextOptions<IntegrationDbContext> options) : base(options) { }

    public DbSet<InboundDocument> InboundDocuments => Set<InboundDocument>();
    public DbSet<PointsRule> PointsRules => Set<PointsRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var comparer = new ValueComparer<JsonObject>(
            (l, r) => JsonNode.DeepEquals(l, r),
            v => JsonSerializer.Serialize(v, new JsonSerializerOptions()).GetHashCode(),
            v => JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(v, new JsonSerializerOptions()), new JsonSerializerOptions())!);

        modelBuilder.Entity<InboundDocument>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ExternalId).IsRequired().HasMaxLength(200);
            e.Property(x => x.CustomerExternalId).HasMaxLength(200);
            e.Property(x => x.DocumentType).IsRequired().HasMaxLength(100);
            e.Property(x => x.Status).IsRequired().HasMaxLength(50);
            e.Property(x => x.PayloadHash).HasMaxLength(200);
            e.Property(x => x.AttemptCount).HasDefaultValue(0);
            e.Property(x => x.LastAttemptAt);

            e.Property(x => x.Payload)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<JsonObject>(v, new JsonSerializerOptions())!)
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(comparer);

            e.HasIndex(x => new { x.TenantId, x.DocumentType, x.ExternalId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CustomerExternalId });
            e.HasIndex(x => new { x.TenantId, x.ReceivedAt });
            e.HasIndex(x => new { x.Status, x.DocumentType });
            e.HasIndex(x => x.PayloadHash);
            e.ToTable("InboundDocuments");
        });

        var docComparer = new ValueComparer<JsonDocument?>(
            (l, r) => JsonDocumentEquals(l, r),
            v => JsonDocumentHash(v),
            v => JsonDocumentSnapshot(v));

        modelBuilder.Entity<PointsRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.RuleType).IsRequired().HasMaxLength(100);
            e.Property(x => x.Priority).HasDefaultValue(0);
            e.Property(x => x.Active).HasDefaultValue(true);
            e.Property(x => x.RuleVersion).HasDefaultValue(1);
            e.Property(x => x.EffectiveFrom).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Conditions)
                .HasConversion(
                    v => v == null ? "{}" : JsonSerializer.Serialize(v, JsonComparerOptions),
                    v => JsonDocument.Parse(v ?? "{}", default))
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(docComparer);

            e.HasIndex(x => new { x.TenantId, x.Active, x.Priority, x.EffectiveFrom });
            e.ToTable("PointsRules");
        });
    }

    private static bool JsonDocumentEquals(JsonDocument? left, JsonDocument? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return JsonDocumentDeepEquals(left.RootElement, right.RootElement);
    }

    private static int JsonDocumentHash(JsonDocument? value) =>
        value == null ? 0 : JsonSerializer.Serialize(value, JsonComparerOptions).GetHashCode();

    private static JsonDocument? JsonDocumentSnapshot(JsonDocument? value) =>
        value == null ? null : JsonDocument.Parse(JsonSerializer.Serialize(value, JsonComparerOptions));

    private static bool JsonDocumentDeepEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind) return false;
        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var leftProps = left.EnumerateObject().OrderBy(p => p.Name);
                    var rightProps = right.EnumerateObject().OrderBy(p => p.Name);
                    return leftProps.SequenceEqual(rightProps, new JsonPropertyComparer());
                }
            case JsonValueKind.Array:
                {
                    var leftItems = left.EnumerateArray().ToList();
                    var rightItems = right.EnumerateArray().ToList();
                    return leftItems.Count == rightItems.Count &&
                           leftItems.Zip(rightItems).All(pair => JsonDocumentDeepEquals(pair.First, pair.Second));
                }
            default:
                return left.ToString() == right.ToString();
        }
    }

    private sealed class JsonPropertyComparer : IEqualityComparer<JsonProperty>
    {
        public bool Equals(JsonProperty x, JsonProperty y) =>
            x.Name == y.Name && JsonDocumentDeepEquals(x.Value, y.Value);

        public int GetHashCode(JsonProperty obj) => HashCode.Combine(obj.Name, obj.Value.ToString());
    }
}
