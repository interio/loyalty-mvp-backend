using Loyalty.Api.Modules.RulesEngine.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;

/// <summary>Integration context for inbound documents.</summary>
public class IntegrationDbContext : DbContext
{
    public IntegrationDbContext(DbContextOptions<IntegrationDbContext> options) : base(options) { }

    public DbSet<InboundDocument> InboundDocuments => Set<InboundDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboundDocument>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ExternalId).IsRequired().HasMaxLength(200);
            e.Property(x => x.DocumentType).IsRequired().HasMaxLength(100);
            e.Property(x => x.Status).IsRequired().HasMaxLength(50);
            e.Property(x => x.PayloadHash).HasMaxLength(200);

            e.Property(x => x.Payload)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<JsonObject>(v, new JsonSerializerOptions())!)
                .HasColumnType("jsonb");

            e.HasIndex(x => new { x.TenantId, x.DocumentType, x.ExternalId }).IsUnique();
            e.HasIndex(x => x.PayloadHash);
        });
    }
}
