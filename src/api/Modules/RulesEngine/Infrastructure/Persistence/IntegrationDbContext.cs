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
    public DbSet<RuleConditionGroup> RuleConditionGroups => Set<RuleConditionGroup>();
    public DbSet<RuleCondition> RuleConditions => Set<RuleCondition>();
    public DbSet<RuleEntity> RuleEntities => Set<RuleEntity>();
    public DbSet<RuleAttribute> RuleAttributes => Set<RuleAttribute>();
    public DbSet<RuleAttributeOperator> RuleAttributeOperators => Set<RuleAttributeOperator>();
    public DbSet<RuleAttributeOption> RuleAttributeOptions => Set<RuleAttributeOption>();

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
            e.Property(x => x.RewardPoints).HasDefaultValue(0);
            e.Property(x => x.Priority).HasDefaultValue(0);
            e.Property(x => x.Active).HasDefaultValue(true);
            e.Property(x => x.EffectiveFrom).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.RootGroupId);
            e.HasOne(x => x.RootGroup)
                .WithMany()
                .HasForeignKey(x => x.RootGroupId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => new { x.TenantId, x.Active, x.Priority, x.EffectiveFrom });
            e.ToTable("PointsRules");
        });

        modelBuilder.Entity<RuleConditionGroup>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Logic).IsRequired().HasMaxLength(3);
            e.Property(x => x.SortOrder).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Rule)
                .WithMany()
                .HasForeignKey(x => x.RuleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ParentGroup)
                .WithMany(x => x.ChildGroups)
                .HasForeignKey(x => x.ParentGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.RuleId);
            e.HasIndex(x => x.ParentGroupId);
            e.ToTable("RuleConditionGroups");
        });

        modelBuilder.Entity<RuleCondition>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityCode).IsRequired().HasMaxLength(100);
            e.Property(x => x.AttributeCode).IsRequired().HasMaxLength(100);
            e.Property(x => x.Operator).IsRequired().HasMaxLength(20);
            e.Property(x => x.SortOrder).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.ValueJson)
                .HasConversion(
                    v => v == null ? "null" : JsonSerializer.Serialize(v, JsonComparerOptions),
                    v => JsonDocument.Parse(v ?? "null", default))
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(docComparer);
            e.HasOne(x => x.Group)
                .WithMany(x => x.Conditions)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.GroupId);
            e.HasIndex(x => new { x.EntityCode, x.AttributeCode });
            e.ToTable("RuleConditions");
        });

        modelBuilder.Entity<RuleEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).IsRequired().HasMaxLength(100);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            e.ToTable("RuleEntities");
        });

        modelBuilder.Entity<RuleAttribute>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).IsRequired().HasMaxLength(100);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
            e.Property(x => x.ValueType).IsRequired().HasMaxLength(50);
            e.Property(x => x.UiControl).IsRequired().HasMaxLength(50);
            e.Property(x => x.IsMultiValue).HasDefaultValue(false);
            e.Property(x => x.IsQueryable).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Entity)
                .WithMany()
                .HasForeignKey(x => x.EntityId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.EntityId, x.Code }).IsUnique();
            e.ToTable("RuleAttributes");
        });

        modelBuilder.Entity<RuleAttributeOperator>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Operator).IsRequired().HasMaxLength(50);
            e.HasOne(x => x.Attribute)
                .WithMany()
                .HasForeignKey(x => x.AttributeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AttributeId, x.Operator }).IsUnique();
            e.ToTable("RuleAttributeOperators");
        });

        modelBuilder.Entity<RuleAttributeOption>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Value).IsRequired().HasMaxLength(100);
            e.Property(x => x.Label).IsRequired().HasMaxLength(200);
            e.HasOne(x => x.Attribute)
                .WithMany()
                .HasForeignKey(x => x.AttributeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AttributeId, x.Value }).IsUnique();
            e.ToTable("RuleAttributeOptions");
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
