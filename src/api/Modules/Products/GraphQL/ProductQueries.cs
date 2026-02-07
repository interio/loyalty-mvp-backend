using System.Security.Claims;
using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Domain;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.Products.GraphQL;

/// <summary>Product catalog read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class ProductQueries
{
    /// <summary>Lists products.</summary>
    public Task<List<Product>> Products(Guid tenantId, ClaimsPrincipal user, [Service] ProductService products) =>
        SafeExecute(() =>
        {
            var scopedTenantId = ProductTenantScopeResolver.Resolve(tenantId, user);
            return products.ListAsync(scopedTenantId);
        });

    /// <summary>Pages products.</summary>
    public Task<ProductConnection> ProductsPage(Guid tenantId, int page, int pageSize, string? search, ClaimsPrincipal user, [Service] ProductService products) =>
        SafeExecute(async () =>
        {
            var scopedTenantId = ProductTenantScopeResolver.Resolve(tenantId, user);
            var result = await products.ListPageAsync(scopedTenantId, page, pageSize, search);
            return new ProductConnection(
                result.Items,
                new PageInfo(result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        });

    /// <summary>Searches products.</summary>
    public Task<List<Product>> ProductsSearch(Guid tenantId, string search, ClaimsPrincipal user, [Service] ProductService products) =>
        SafeExecute(() =>
        {
            var scopedTenantId = ProductTenantScopeResolver.Resolve(tenantId, user);
            return products.SearchAsync(scopedTenantId, search);
        });

    private static async Task<T> SafeExecute<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }
}

public record ProductConnection(IReadOnlyList<Product> Nodes, PageInfo PageInfo);
