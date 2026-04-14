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
        SafeExecute(() => tenants.CreateAsync(
            input.Name,
            input.Email,
            input.Phone,
            input.Address,
            input.Config is null
                ? null
                : new TenantConfigCommand(input.Config.Currency)));

    /// <summary>Updates tenant configuration section (currency and future settings).</summary>
    public Task<Tenant> UpdateTenantConfig(UpdateTenantConfigInput input, [Service] ITenantService tenants) =>
        SafeExecute(() => tenants.UpdateConfigAsync(
            input.TenantId,
            new TenantConfigCommand(input.Config?.Currency)));

    /// <summary>
    /// Sets a generic tenant config key/value.
    /// Use null <c>tenantId</c> to store shared default values.
    /// </summary>
    public Task<bool> SetTenantConfigValue(SetTenantConfigValueInput input, [Service] ITenantService tenants) =>
        SafeExecute(async () =>
        {
            await tenants.SetConfigValueAsync(input.TenantId, input.ConfigName, input.ConfigValue);
            return true;
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

/// <summary>Input for creating a tenant (client/organization).</summary>
public record CreateTenantInput(string Name, string? Email, string? Phone, string? Address, TenantConfigInput? Config = null);

/// <summary>Input for tenant configuration section.</summary>
public record TenantConfigInput(string? Currency);

/// <summary>Input for updating tenant configuration.</summary>
public record UpdateTenantConfigInput(Guid TenantId, TenantConfigInput? Config);

/// <summary>Input for setting a generic tenant/default config key-value.</summary>
public record SetTenantConfigValueInput(Guid? TenantId, string ConfigName, string ConfigValue);
