using Loyalty.Api.Modules.Integration.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Loyalty.Api.Infrastructure.Persistence;

/// <summary>Integration context for inbound documents.</summary>
public class IntegrationDbContext : DbContext
{
    /// <summary>Constructs the integration context.</summary>
    public IntegrationDbContext(DbContextOptions<IntegrationDbContext> options) : base(options) { }

    /// <summary>Inbound documents from ERP/MuleSoft.</summary>
    public DbSet<InboundDocument> InboundDocuments => Set<InboundDocument>();

    /// <summary>Configure entities and indexes.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboundDocument>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ExternalId).IsRequired().HasMaxLength(200);
            e.Property(x => x.DocumentType).IsRequired().HasMaxLength(100);
            e.Property(x => x.Status).IsRequired().HasMaxLength(50);
            e.Property(x => x.PayloadHash).HasMaxLength(200);

            // JSONB payload stored as text; use converter for providers like InMemory.
            e.Property(x => x.Payload)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<JsonObject>(v, new JsonSerializerOptions())!)
                .HasColumnType("jsonb");

            // Idempotency: external id + document type within a tenant.
            e.HasIndex(x => new { x.TenantId, x.DocumentType, x.ExternalId }).IsUnique();
            e.HasIndex(x => x.PayloadHash);
        });
    }
}
