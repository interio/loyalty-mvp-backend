# Loyalty B2B Headless Backend

This project is a headless modular backend service designed to be split into microservices later. Modules are isolated by contexts (Tenants, Customers/Users, Ledger, RulesEngine/Integration, Products, GraphQL).

## Requirements
- .NET 8 SDK
- PostgreSQL (set `ConnectionStrings__Default`; Docker uses `Host=postgres;Port=5432;Database=loyalty;Username=loyalty;Password=loyalty`)
- Optional: set `ALLOWED_ORIGINS` (comma-separated, or `*`) to control API CORS policy.
- Node/PNPM not required (GraphQL served by Hot Chocolate in ASP.NET Core)

## Module structure
- `src/api/Modules/Tenants` — tenant domain, GraphQL resolvers, TenantsDbContext.
- `src/api/Modules/Customers` — customers/users domain, GraphQL resolvers, CustomersDbContext.
- `src/api/Modules/LoyaltyLedger` — points accounts and transactions, GraphQL resolvers, LedgerDbContext.
- `src/api/Modules/RulesEngine` — inbound invoices + rules; REST controller; IntegrationDbContext.
- `src/api/Modules/Products` — product catalog for loyalty rules; REST + GraphQL; ProductsDbContext.
- `src/api/Modules/RewardCatalog` — reward products + inventory snapshots; REST + GraphQL; RewardCatalogDbContext.
- `src/api/Modules/RewardOrders` — reward redemption orders; GraphQL; RewardOrdersDbContext.
- `src/api/Modules/Shared` — shared paging helpers and common types used across modules.
- GraphQL composition lives in `Program.cs` using module extensions.

## High-level architecture
- **Tenants**: owns tenant lifecycle.
- **Customers/Users**: customers/outlets and their users.
- **LoyaltyLedger**: immutable points transactions + cached balance.
- **RulesEngine**: ingests invoices, applies DB-backed rules, posts ledger entries idempotently (async worker).
- **Products**: stores product master data for rules evaluation.
- **RewardCatalog**: stores reward vendor products and inventory snapshots.
- **RewardOrders**: stores customer redemption orders and triggers ledger redemptions.
- **GraphQL**: transport layer, delegates to module services.
- DbContexts per module; migrations per context under each module folder.
- Idempotency: ledger correlation IDs; RulesEngine invoice upsert keyed by tenantId+invoiceId.

## Configure connection string
Set `ConnectionStrings__Default` env var (required; `appsettings*.json` do not include it by default). Example:
```
export ConnectionStrings__Default="Host=postgres;Port=5432;Database=loyalty;Username=loyalty;Password=loyalty"
```
Optional CORS override:
```
export ALLOWED_ORIGINS="http://localhost:3000,http://127.0.0.1:3000"
```

## Database setup (per-context migrations)
Run from repo root:
```
cd src/api
dotnet ef database update --context TenantsDbContext
dotnet ef database update --context CustomersDbContext
dotnet ef database update --context LedgerDbContext
dotnet ef database update --context IntegrationDbContext
dotnet ef database update --context ProductsDbContext
dotnet ef database update --context RewardCatalogDbContext
dotnet ef database update --context RewardOrdersDbContext
```

## Run the API
```
dotnet run --project src/api/Loyalty.Api.csproj
```
Local `dotnet run` / `dotnet watch` uses the launch profile from `src/api/Properties/launchSettings.json` (`http://localhost:5137`).
Docker/Compose sets `ASPNETCORE_URLS=http://0.0.0.0:8080`.
To force port 8080 locally:
```
ASPNETCORE_URLS=http://localhost:8080 dotnet run --project src/api/Loyalty.Api.csproj
```

## Endpoints
- GraphQL: `POST /graphql`
  - Resolvers are registered per module; use module inputs:
    - Tenants: `createTenant`, `tenants`, `tenantsPage`
    - Customers/Users: `createCustomer`, `createUser`, `customer`, `customersByTenant`, `customersByTenantPage` (optional `search`), `customersByTenantSearch`, `usersByCustomer`, `usersByTenant`, `usersByTenantPage` (optional `search`), `usersByTenantSearch`
    - Ledger: `redeemPoints`, `manualAdjustPoints`, `customerTransactions`
    - Products: `products`, `productsSearch`, `productsPage` (optional `search`)
    - RewardCatalog: `rewardProducts`, `rewardProductsSearch`, `rewardProductsPage` (optional `search`), `rewardProduct`, `upsertRewardProduct`, `deleteRewardProduct`
    - RewardOrders: `rewardOrdersByTenant`, `rewardOrdersByTenantPage`, `rewardOrdersByTenantCursor`, `rewardOrdersByCustomer`, `rewardOrder`, `updateRewardOrderStatus`, `placeRewardOrder`, `placeRewardOrderOnBehalf`
    - RulesEngine: `pointsRulesByTenant`, `pointsRulesByTenantPage`, `ruleConditionTree`, `ruleConditionTreeFlat`, `invoicesByTenant` (with `take`), `invoicesByTenantPage` (optional `search`), `invoiceById`, `invoicesByTenantCursor` (optional `search`)
    - Rules catalog metadata (RulesEngine): `ruleEntities`, `ruleAttributes`, `ruleAttributeOperators`, `ruleAttributeOptions`, `ruleOperatorCatalog`, `createRuleEntity`, `updateRuleEntity`, `deleteRuleEntity`, `createRuleAttribute`, `updateRuleAttribute`, `deleteRuleAttribute`, `setRuleAttributeOperators`, `createRuleAttributeOption`, `updateRuleAttributeOption`, `deleteRuleAttributeOption`
  - `*Cursor` queries use keyset pagination with `after` + `take` and return `pageInfo { endCursor, hasNextPage }`.
- REST (RulesEngine): `POST /api/v1/integration/invoices/apply`
  - Body: see `InvoiceUpsertRequest` in `src/api/Modules/RulesEngine/Application/Invoices/InvoiceUpsertRequest.cs`
  - Returns `202 Accepted` with a `correlationId` (processing is async via background worker).
- REST (RulesEngine rules): `POST /api/v1/rules/points/upsert` (batch insert of versioned rules), `POST /api/v1/rules/points/complex`, `PUT /api/v1/rules/points/{id}` (tenantId + active only), `DELETE /api/v1/rules/points/{id}?tenantId=...`
  - Existing rules are immutable; create a new rule version instead of editing an existing one.
- REST (Products): `POST /api/v1/products/upsert`
- REST (RewardCatalog): `POST /api/v1/rewards/catalog/upsert`, `POST /api/v1/rewards/catalog/upload` (CSV)
- RewardOrders are available via GraphQL mutations/queries (see `RewardOrderMutations` and `RewardOrderQueries`).

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
dotnet ef migrations add <Name> --context ProductsDbContext --output-dir Modules/Products/Migrations
dotnet ef migrations add <Name> --context RewardCatalogDbContext --output-dir Modules/RewardCatalog/Migrations
dotnet ef migrations add <Name> --context RewardOrdersDbContext --output-dir Modules/RewardOrders/Migrations
```

## Notes / TODOs
- List queries support page-based pagination via `page` + `pageSize` in GraphQL (admin UI consumes these).
- Admin UI auth is a placeholder; secure GraphQL/REST with authentication/authorization.
- Cross-context lookups use IDs; navigations across DbContexts are avoided to ease future extraction.
- Ensure correlation IDs are provided for idempotent ledger operations (GraphQL and REST).
