# Loyalty B2B Modular Monolith (MVP)

This project is a modular monolith designed to be split into microservices later. Modules are isolated by contexts (Tenants, Customers/Users, Ledger, RulesEngine/Integration, Experience/GraphQL).

## Requirements
- .NET 8 SDK
- PostgreSQL (default connection: `Host=postgres;Port=5432;Database=loyalty;Username=loyalty;Password=loyalty`)
- Node/PNPM not required (GraphQL served by Hot Chocolate in ASP.NET Core)

## Module structure
- `src/api/Modules/Tenants` — tenant domain, GraphQL resolvers, TenantsDbContext.
- `src/api/Modules/Customers` — customers/users domain, GraphQL resolvers, CustomersDbContext.
- `src/api/Modules/LoyaltyLedger` — points accounts and transactions, GraphQL resolvers, LedgerDbContext.
- `src/api/Modules/RulesEngine` — inbound invoices + rules; REST controller; IntegrationDbContext.
- `src/api/Modules/Experience` — GraphQL composition lives in `Program.cs` using module extensions.

## High-level architecture
- **Tenants**: owns tenant lifecycle.
- **Customers/Users**: customers/outlets and their users.
- **LoyaltyLedger**: immutable points transactions + cached balance.
- **RulesEngine**: ingests invoices, applies hardcoded rules, posts ledger entries idempotently.
- **Experience**: GraphQL transport, delegates to module services.
- DbContexts per module; migrations per context under each module folder.
- Idempotency: ledger correlation IDs; RulesEngine invoice upsert keyed by tenantId+invoiceId.

## Configure connection string
Set `ConnectionStrings__Default` env var or edit `src/api/appsettings.Development.json`. Example:
```
export ConnectionStrings__Default="Host=postgres;Port=5432;Database=loyalty;Username=loyalty;Password=loyalty"
```

## Database setup (per-context migrations)
Run from repo root:
```
cd src/api
dotnet ef database update --context TenantsDbContext
dotnet ef database update --context CustomersDbContext
dotnet ef database update --context LedgerDbContext
dotnet ef database update --context IntegrationDbContext
```

## Run the API
```
dotnet run --project src/api/Loyalty.Api.csproj
```
By default listens on `http://localhost:8080` (or URLS override). In logs, look for the bound URL.

## Endpoints
- GraphQL: `POST /graphql`
  - Resolvers are registered per module; use module inputs:
    - Tenants: `createTenant`, `tenants`
    - Customers/Users: `createCustomer`, `createUser`, `customer`, `customersByTenant`, `usersByCustomer`
    - Ledger: `redeemPoints`, `manualAdjustPoints`, `customerTransactions`
- REST (RulesEngine): `POST /api/v1/integration/invoices/apply`
  - Body: see `InvoiceUpsertRequest` in `src/api/Modules/RulesEngine/Application/Invoices/InvoiceUpsertRequest.cs`

## Tests
```
dotnet test LoyaltyMvp.sln
```
Includes unit tests for rules, ledger behaviors, invoice idempotency (`tests/Loyalty.Api.Tests`).

## Adding migrations (per context)
Examples:
```
cd src/api
dotnet ef migrations add <Name> --context TenantsDbContext --output-dir Modules/Tenants/Migrations
dotnet ef migrations add <Name> --context CustomersDbContext --output-dir Modules/Customers/Migrations
dotnet ef migrations add <Name> --context LedgerDbContext --output-dir Modules/LoyaltyLedger/Migrations
dotnet ef migrations add <Name> --context IntegrationDbContext --output-dir Modules/RulesEngine/Migrations
```

## Notes / TODOs
- RulesEngine rules are hardcoded (`HardcodedRulesProvider`); replace with DB-backed rules and admin UI later.
- Cross-context lookups use IDs; navigations across DbContexts are avoided to ease future extraction.
- Ensure correlation IDs are provided for idempotent ledger operations (GraphQL and REST).
