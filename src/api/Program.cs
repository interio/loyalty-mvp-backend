using Loyalty.Api.Infrastructure.Persistence;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.RulesEngine.Application;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.Tenants.Application;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
  .AddTypeExtension<Loyalty.Api.Modules.LoyaltyLedger.GraphQL.LedgerMutations>();
builder.Services.AddControllers();

var cs = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(cs))
{
    throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Default'. " +
        "Set ConnectionStrings__Default env var (Dev Container) or appsettings.Development.json.");
}

builder.Services.AddDbContext<LoyaltyDbContext>(opt => opt.UseNpgsql(cs));
builder.Services.AddDbContext<IntegrationDbContext>(opt => opt.UseNpgsql(cs));
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICustomerLookup>(sp => (CustomerService)sp.GetRequiredService<ICustomerService>());
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserLookup>(sp => (UserService)sp.GetRequiredService<IUserService>());
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IInvoicePointsRuleProvider, HardcodedRulesProvider>();
builder.Services.AddScoped<PointsPostingService>();

var app = builder.Build();

app.MapControllers();
app.MapGraphQL("/graphql");
app.Run();
