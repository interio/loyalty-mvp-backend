using System.Text.Json;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Microsoft.EntityFrameworkCore;
using Loyalty.Api.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;

/// <summary>DbContext for ledger (accounts and transactions).</summary>
public class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }

    public DbSet<PointsAccount> PointsAccounts => Set<PointsAccount>();
    public DbSet<PointsTransaction> PointsTransactions => Set<PointsTransaction>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ignore tenant entity to avoid accidental table creation in this context.
        modelBuilder.Ignore<Tenant>();

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.Address);
            e.Property(x => x.Name).IsRequired().HasMaxLength(300);
            e.ToTable("Customers", b => b.ExcludeFromMigrations());
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).IsRequired().HasMaxLength(320);
            e.ToTable("Users", b => b.ExcludeFromMigrations());
        });

        modelBuilder.Entity<PointsAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CustomerId).IsUnique();
            e.ToTable("PointsAccounts");
        });

        modelBuilder.Entity<PointsTransaction>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Reason).IsRequired().HasMaxLength(200);
            e.Property(x => x.ActorEmail).HasMaxLength(320);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.CorrelationId).HasMaxLength(200);
            e.Property(x => x.AppliedRules)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => v == null ? null : JsonDocument.Parse(v, new JsonDocumentOptions()))
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(JsonDocumentComparer);

            e.HasOne(x => x.Customer)
                .WithMany(c => c.Transactions)
                .HasForeignKey(x => x.CustomerId);

            e.HasOne(x => x.ActorUser)
                .WithMany(u => u.InitiatedTransactions)
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.CorrelationId);
            e.HasIndex(x => new { x.CustomerId, x.CorrelationId }).IsUnique();
            e.ToTable("PointsTransactions");
        });
    }

    private static readonly ValueComparer<JsonDocument?> JsonDocumentComparer = new(
        (l, r) => JsonDocumentEquals(l, r),
        v => JsonDocumentHash(v),
        v => JsonDocumentSnapshot(v));

    private static bool JsonDocumentEquals(JsonDocument? left, JsonDocument? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return JsonDocumentDeepEquals(left.RootElement, right.RootElement);
    }

    private static int JsonDocumentHash(JsonDocument? value) =>
        value == null ? 0 : JsonSerializer.Serialize(value, new JsonSerializerOptions()).GetHashCode();

    private static JsonDocument? JsonDocumentSnapshot(JsonDocument? value) =>
        value == null ? null : JsonDocument.Parse(JsonSerializer.Serialize(value, new JsonSerializerOptions()));

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
