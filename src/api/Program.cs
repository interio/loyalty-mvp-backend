using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Loyalty.Api.Modules.RulesEngine.Application;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Loyalty.Api.Modules.Tenants.Application;
using Loyalty.Api.Modules.Tenants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
  .AddTypeExtension<Loyalty.Api.Modules.RulesEngine.GraphQL.PointsRuleQueries>()
  .AddTypeExtension<Loyalty.Api.Modules.RulesEngine.GraphQL.InvoiceQueries>();
builder.Services.AddControllers();

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
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICustomerLookup>(sp => (CustomerService)sp.GetRequiredService<ICustomerService>());
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserLookup>(sp => (UserService)sp.GetRequiredService<IUserService>());
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IInvoicePointsRuleProvider, DatabaseInvoicePointsRuleProvider>();
builder.Services.AddScoped<PointsPostingService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<PointsRuleService>();
builder.Services.Configure<Loyalty.Api.Modules.RulesEngine.Application.InvoiceProcessorOptions>(
    builder.Configuration.GetSection("InvoiceProcessor"));
builder.Services.AddHostedService<Loyalty.Api.Modules.RulesEngine.Application.InvoiceProcessingWorker>();

var app = builder.Build();

app.UseCors(ClientCors);
app.MapControllers();
app.MapGraphQL("/graphql");
app.Run();
