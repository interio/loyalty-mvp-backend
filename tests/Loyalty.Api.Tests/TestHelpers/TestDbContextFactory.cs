using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Loyalty.Api.Modules.RewardCatalog.Infrastructure.Persistence;
using Loyalty.Api.Modules.RewardOrders.Infrastructure.Persistence;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Loyalty.Api.Modules.Tenants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Tests.TestHelpers;

public static class TestDbContextFactory
{
    public static TenantsDbContext CreateTenants(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TenantsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new TenantsDbContext(options);
    }

    public static CustomersDbContext CreateCustomers(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new CustomersDbContext(options);
    }

    public static async Task EnsureCustomersSchemaAsync(CustomersDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"Tenants\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"Name\" character varying(200) NOT NULL, " +
            "\"CreatedAt\" timestamp with time zone NOT NULL)");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"Customers\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"TenantId\" uuid NOT NULL, " +
            "\"Name\" character varying(300) NOT NULL, " +
            "\"ContactEmail\" character varying(320), " +
            "\"ExternalId\" character varying(200), " +
            "\"CreatedAt\" timestamp with time zone NOT NULL)");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"Users\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"TenantId\" uuid NOT NULL, " +
            "\"CustomerId\" uuid NOT NULL, " +
            "\"Email\" character varying(320) NOT NULL, " +
            "\"ExternalId\" character varying(200), " +
            "\"Role\" character varying(100), " +
            "\"CreatedAt\" timestamp with time zone NOT NULL)");
    }

    public static LedgerDbContext CreateLedger(string connectionString)
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new LedgerDbContext(options);
    }

    public static async Task EnsureLedgerSchemaAsync(LedgerDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"PointsAccounts\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"CustomerId\" uuid NOT NULL, " +
            "\"Balance\" bigint NOT NULL, " +
            "\"UpdatedAt\" timestamp with time zone NOT NULL)");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"PointsTransactions\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"CustomerId\" uuid NOT NULL, " +
            "\"ActorUserId\" uuid NULL, " +
            "\"ActorEmail\" character varying(320) NULL, " +
            "\"Comment\" character varying(1000) NULL, " +
            "\"Amount\" integer NOT NULL, " +
            "\"Reason\" character varying(200) NOT NULL, " +
            "\"CorrelationId\" character varying(200), " +
            "\"CreatedAt\" timestamp with time zone NOT NULL, " +
            "\"AppliedRules\" jsonb NULL)");
    }

    public static ProductsDbContext CreateProducts(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ProductsDbContext(options);
    }

    public static async Task EnsureProductsSchemaAsync(ProductsDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"Tenants\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"Name\" character varying(200) NOT NULL, " +
            "\"CreatedAt\" timestamp with time zone NOT NULL)");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"Distributors\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"TenantId\" uuid NOT NULL, " +
            "\"Name\" character varying(200) NOT NULL, " +
            "\"DisplayName\" character varying(300) NOT NULL, " +
            "\"CreatedAt\" timestamp with time zone NOT NULL, " +
            "CONSTRAINT \"AK_Distributors_TenantId_Id\" UNIQUE (\"TenantId\", \"Id\"))");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Distributors_TenantId_Name\" " +
            "ON \"Distributors\" (\"TenantId\", \"Name\")");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"Products\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"TenantId\" uuid NOT NULL, " +
            "\"DistributorId\" uuid NOT NULL, " +
            "\"Sku\" character varying(200) NOT NULL, " +
            "\"Gtin\" character varying(50) NULL, " +
            "\"Name\" character varying(400) NOT NULL, " +
            "\"Cost\" numeric(18,2) NOT NULL, " +
            "\"Attributes\" text NOT NULL, " +
            "\"CreatedAt\" timestamp with time zone NOT NULL, " +
            "\"UpdatedAt\" timestamp with time zone NOT NULL, " +
            "CONSTRAINT \"FK_Products_Distributors_TenantId_DistributorId\" " +
            "FOREIGN KEY (\"TenantId\", \"DistributorId\") REFERENCES \"Distributors\" (\"TenantId\", \"Id\"))");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Products_TenantId_DistributorId_Sku\" " +
            "ON \"Products\" (\"TenantId\", \"DistributorId\", \"Sku\") WHERE \"Gtin\" IS NULL");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Products_TenantId_DistributorId_Sku_Gtin\" " +
            "ON \"Products\" (\"TenantId\", \"DistributorId\", \"Sku\", \"Gtin\") WHERE \"Gtin\" IS NOT NULL");
    }

    public static IntegrationDbContext CreateIntegration(string connectionString)
    {
        var options = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new IntegrationDbContext(options);
    }

    public static async Task EnsureIntegrationSchemaAsync(IntegrationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"InboundDocuments\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"TenantId\" uuid NOT NULL, " +
            "\"ExternalId\" text NOT NULL, " +
            "\"CustomerExternalId\" character varying(200) NULL, " +
            "\"DocumentType\" text NOT NULL, " +
            "\"Payload\" jsonb NOT NULL, " +
            "\"PayloadHash\" text NULL, " +
            "\"Status\" text NOT NULL DEFAULT 'pending_points', " +
            "\"Error\" text NULL, " +
            "\"AttemptCount\" integer NOT NULL DEFAULT 0, " +
            "\"LastAttemptAt\" timestamp with time zone NULL, " +
            "\"ReceivedAt\" timestamp with time zone NOT NULL DEFAULT now(), " +
            "\"ProcessedAt\" timestamp with time zone NULL)");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_InboundDocuments_TenantId_CustomerExternalId\" " +
            "ON \"InboundDocuments\" (\"TenantId\", \"CustomerExternalId\")");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_InboundDocuments_TenantId_ReceivedAt\" " +
            "ON \"InboundDocuments\" (\"TenantId\", \"ReceivedAt\")");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"PointsRules\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"TenantId\" uuid NOT NULL, " +
            "\"RuleType\" text NOT NULL, " +
            "\"Conditions\" jsonb NOT NULL, " +
            "\"Active\" boolean NOT NULL, " +
            "\"Priority\" integer NOT NULL, " +
            "\"RuleVersion\" integer NOT NULL, " +
            "\"EffectiveFrom\" timestamp with time zone NOT NULL, " +
            "\"EffectiveTo\" timestamp with time zone NULL, " +
            "\"CreatedAt\" timestamp with time zone NOT NULL, " +
            "\"UpdatedAt\" timestamp with time zone NULL)");
    }

    public static RewardCatalogDbContext CreateRewardCatalog(string connectionString)
    {
        var options = new DbContextOptionsBuilder<RewardCatalogDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new RewardCatalogDbContext(options);
    }

    public static async Task EnsureRewardCatalogSchemaAsync(RewardCatalogDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"RewardProducts\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"TenantId\" uuid NOT NULL, " +
            "\"RewardVendor\" character varying(200) NOT NULL, " +
            "\"Sku\" character varying(200) NOT NULL, " +
            "\"Gtin\" character varying(50) NULL, " +
            "\"Name\" character varying(400) NOT NULL, " +
            "\"PointsCost\" integer NOT NULL, " +
            "\"Attributes\" text NOT NULL, " +
            "\"CreatedAt\" timestamp with time zone NOT NULL, " +
            "\"UpdatedAt\" timestamp with time zone NOT NULL)");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"RewardInventories\" (" +
            "\"RewardProductId\" uuid PRIMARY KEY, " +
            "\"AvailableQuantity\" integer NOT NULL, " +
            "\"UpdatedAt\" timestamp with time zone NOT NULL, " +
            "\"LastSyncedAt\" timestamp with time zone NULL)");
    }

    public static RewardOrdersDbContext CreateRewardOrders(string connectionString)
    {
        var options = new DbContextOptionsBuilder<RewardOrdersDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new RewardOrdersDbContext(options);
    }

    public static async Task EnsureRewardOrdersSchemaAsync(RewardOrdersDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"RewardOrders\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"TenantId\" uuid NOT NULL, " +
            "\"CustomerId\" uuid NOT NULL, " +
            "\"ActorUserId\" uuid NOT NULL, " +
            "\"Status\" integer NOT NULL, " +
            "\"TotalPoints\" integer NOT NULL, " +
            "\"PlacedOnBehalf\" boolean NOT NULL DEFAULT false, " +
            "\"ProviderReference\" character varying(200) NULL, " +
            "\"CreatedAt\" timestamp with time zone NOT NULL, " +
            "\"UpdatedAt\" timestamp with time zone NOT NULL)");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"RewardOrderItems\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"RewardOrderId\" uuid NOT NULL, " +
            "\"RewardProductId\" uuid NOT NULL, " +
            "\"RewardVendor\" character varying(200) NOT NULL, " +
            "\"Sku\" character varying(200) NOT NULL, " +
            "\"Name\" character varying(400) NOT NULL, " +
            "\"Quantity\" integer NOT NULL, " +
            "\"PointsCost\" integer NOT NULL, " +
            "\"TotalPoints\" integer NOT NULL)");
    }
}
