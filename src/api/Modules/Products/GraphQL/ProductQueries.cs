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
