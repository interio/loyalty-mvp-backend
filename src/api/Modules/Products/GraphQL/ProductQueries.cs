using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Domain;

namespace Loyalty.Api.Modules.Products.GraphQL;

/// <summary>Product catalog read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class ProductQueries
{
    /// <summary>Lists products.</summary>
    public Task<List<Product>> Products([Service] ProductService products) =>
        SafeExecute(() => products.ListAsync());

    /// <summary>Pages products.</summary>
    public Task<ProductConnection> ProductsPage(int page, int pageSize, [Service] ProductService products) =>
        SafeExecute(async () =>
        {
            var result = await products.ListPageAsync(page, pageSize);
            return new ProductConnection(
                result.Items,
                new ProductPageInfo(result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        });

    /// <summary>Searches products.</summary>
    public Task<List<Product>> ProductsSearch(string search, [Service] ProductService products) =>
        SafeExecute(() => products.SearchAsync(search));

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

public record ProductConnection(IReadOnlyList<Product> Nodes, ProductPageInfo PageInfo);

public record ProductPageInfo(int TotalCount, int Page, int PageSize, int TotalPages);
