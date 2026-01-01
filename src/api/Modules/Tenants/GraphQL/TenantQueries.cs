using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.Tenants.Application;
using Loyalty.Api.Modules.Tenants.Domain;

namespace Loyalty.Api.Modules.Tenants.GraphQL;

/// <summary>Tenant read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class TenantQueries
{
    /// <summary>Lists tenants (admin convenience).</summary>
    public Task<List<Tenant>> Tenants([Service] ITenantService tenants) =>
        SafeExecute(() => tenants.ListAsync());

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
