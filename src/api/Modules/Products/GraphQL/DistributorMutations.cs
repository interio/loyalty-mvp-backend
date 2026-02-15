using System.Security.Claims;
using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Domain;

namespace Loyalty.Api.Modules.Products.GraphQL;

/// <summary>Distributor write operations.</summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class DistributorMutations
{
    public Task<Distributor> CreateDistributor(
        CreateDistributorInput input,
        ClaimsPrincipal user,
        [Service] DistributorService distributors) =>
        SafeExecute(() =>
        {
            var scopedTenantId = ProductTenantScopeResolver.Resolve(input.TenantId, user);
            return distributors.CreateAsync(new CreateDistributorCommand(
                scopedTenantId,
                input.Name,
                input.DisplayName));
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

public record CreateDistributorInput(Guid TenantId, string Name, string DisplayName);
