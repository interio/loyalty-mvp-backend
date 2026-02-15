using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.Tenants.Application;
using Loyalty.Api.Modules.Tenants.Domain;

namespace Loyalty.Api.Modules.Tenants.GraphQL;

/// <summary>Tenant mutations.</summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class TenantMutations
{
    /// <summary>Creates a tenant record.</summary>
    public Task<Tenant> CreateTenant(CreateTenantInput input, [Service] ITenantService tenants) =>
        SafeExecute(() => tenants.CreateAsync(input.Name, input.Email, input.Phone, input.Address));

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

/// <summary>Input for creating a tenant (client/organization).</summary>
public record CreateTenantInput(string Name, string? Email, string? Phone, string? Address);
