using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Loyalty.Api.Modules.RewardCatalog.Application;
using Loyalty.Api.Modules.RewardCatalog.Infrastructure.Persistence;
using Loyalty.Api.Modules.RewardOrders.Application;
using Loyalty.Api.Modules.RewardOrders.Infrastructure.Persistence;
using Loyalty.Api.Modules.RulesEngine.Application;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Loyalty.Api.Modules.Tenants.Application;
using Loyalty.Api.Modules.Tenants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

const string ClientCors = "ClientCors";

// TODO: Add authentication/authorization to protect REST and GraphQL endpoints.
builder.Services
  .AddGraphQLServer()
  .ModifyRequestOptions(o => o.IncludeExceptionDetails = builder.Environment.IsDevelopment())
  .AddQueryType(d => d.Name("Query"))
  .AddMutationType(d => d.Name("Mutation"))
  .AddTypeExtension<Loyalty.Api.Modules.Customers.GraphQL.CustomerQueries>()
  .AddTypeExtension<Loyalty.Api.Modules.Customers.GraphQL.CustomerMutations>()
  .AddTypeExtension<Loyalty.Api.Modules.Tenants.GraphQL.TenantQueries>()
  .AddTypeExtension<Loyalty.Api.Modules.Tenants.GraphQL.TenantMutations>()
  .AddTypeExtension<Loyalty.Api.Modules.LoyaltyLedger.GraphQL.LedgerQueries>()
  .AddTypeExtension<Loyalty.Api.Modules.LoyaltyLedger.GraphQL.LedgerMutations>()
  .AddTypeExtension<Loyalty.Api.Modules.Products.GraphQL.ProductQueries>()
  .AddTypeExtension<Loyalty.Api.Modules.Products.GraphQL.DistributorQueries>()
  .AddTypeExtension<Loyalty.Api.Modules.Products.GraphQL.DistributorMutations>()
  .AddTypeExtension<Loyalty.Api.Modules.RewardCatalog.GraphQL.RewardCatalogQueries>()
  .AddTypeExtension<Loyalty.Api.Modules.RewardCatalog.GraphQL.RewardCatalogMutations>()
  .AddTypeExtension<Loyalty.Api.Modules.RewardCatalog.GraphQL.RewardProductExtensions>()
  .AddTypeExtension<Loyalty.Api.Modules.RewardOrders.GraphQL.RewardOrderQueries>()
  .AddTypeExtension<Loyalty.Api.Modules.RewardOrders.GraphQL.RewardOrderMutations>()
  .AddTypeExtension<Loyalty.Api.Modules.RulesEngine.GraphQL.PointsRuleQueries>()
  .AddTypeExtension<Loyalty.Api.Modules.RulesEngine.GraphQL.InvoiceQueries>()
  .AddTypeExtension<Loyalty.Api.Modules.RulesEngine.GraphQL.RuleCatalogQueries>()
  .AddTypeExtension<Loyalty.Api.Modules.RulesEngine.GraphQL.RuleEntityMutations>()
  .AddTypeExtension<Loyalty.Api.Modules.RulesEngine.GraphQL.RuleAttributeMutations>()
  .AddTypeExtension<Loyalty.Api.Modules.RulesEngine.GraphQL.RuleAttributeOptionMutations>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Loyalty MVP REST API",
        Version = "v1",
        Description = "REST endpoints for integrations and batch operations."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

// Basic CORS for local admin UI (override with ALLOWED_ORIGINS env/comma list if needed).
var allowedOrigins = (builder.Configuration["ALLOWED_ORIGINS"] ?? "http://localhost:3000,http://127.0.0.1:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientCors, policy =>
    {
        if (allowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var cs = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(cs))
{
    throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Default'. " +
        "Set ConnectionStrings__Default env var (Dev Container) or appsettings.Development.json.");
}

builder.Services.AddDbContext<TenantsDbContext>(opt => opt.UseNpgsql(cs));
builder.Services.AddDbContext<CustomersDbContext>(opt => opt.UseNpgsql(cs));
builder.Services.AddDbContext<LedgerDbContext>(opt => opt.UseNpgsql(cs));
builder.Services.AddDbContext<IntegrationDbContext>(opt => opt.UseNpgsql(cs));
builder.Services.AddDbContext<ProductsDbContext>(opt => opt.UseNpgsql(cs));
builder.Services.AddDbContext<RewardCatalogDbContext>(opt => opt.UseNpgsql(cs));
builder.Services.AddDbContext<RewardOrdersDbContext>(opt => opt.UseNpgsql(cs));
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICustomerLookup>(sp => (CustomerService)sp.GetRequiredService<ICustomerService>());
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserLookup>(sp => (UserService)sp.GetRequiredService<IUserService>());
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IComplexRuleEntityEvaluator, ComplexRuleInvoiceEntityEvaluator>();
builder.Services.AddScoped<IComplexRuleEntityEvaluator, ComplexRuleProductEntityEvaluator>();
builder.Services.AddScoped<IInvoicePointsRuleProvider, DatabaseInvoicePointsRuleProvider>();
builder.Services.AddScoped<PointsPostingService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<DistributorService>();
builder.Services.AddScoped<RewardCatalogService>();
builder.Services.AddScoped<IRewardCatalogLookup>(sp => sp.GetRequiredService<RewardCatalogService>());
builder.Services.AddScoped<IRewardInventoryService>(sp => sp.GetRequiredService<RewardCatalogService>());
builder.Services.AddScoped<IRewardOrderDispatcher, StubRewardOrderDispatcher>();
builder.Services.AddScoped<RewardOrderService>();
builder.Services.AddScoped<PointsRuleService>();
builder.Services.Configure<Loyalty.Api.Modules.RulesEngine.Application.InvoiceProcessorOptions>(
    builder.Configuration.GetSection("InvoiceProcessor"));
builder.Services.AddHostedService<Loyalty.Api.Modules.RulesEngine.Application.InvoiceProcessingWorker>();

var app = builder.Build();

app.UseCors(ClientCors);
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Loyalty MVP REST API v1");
        options.RoutePrefix = "swagger";
    });
}
app.MapControllers();
app.MapGraphQL("/graphql");
app.Run();
