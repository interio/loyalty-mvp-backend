using System.Security.Claims;

namespace Loyalty.Api.Modules.Products.Application;

/// <summary>
/// Resolves tenant scope for product operations and enforces consistency with authenticated tenant claims.
/// </summary>
public static class ProductTenantScopeResolver
{
    private static readonly string[] TenantClaimTypes = ["tenant_id", "tenantId", "tid"];

    public static Guid Resolve(Guid requestedTenantId, ClaimsPrincipal? user)
    {
        var hasTenantClaim = TryGetTenantClaim(user, out var claimTenantId, out var claimError);
        if (!string.IsNullOrWhiteSpace(claimError))
            throw new ArgumentException(claimError);

        var isAuthenticated = user?.Identity?.IsAuthenticated == true;
        if (isAuthenticated && !hasTenantClaim)
            throw new UnauthorizedAccessException("Authenticated requests must include a tenant claim.");

        if (hasTenantClaim)
        {
            if (requestedTenantId == Guid.Empty)
                return claimTenantId;

            if (requestedTenantId != claimTenantId)
                throw new UnauthorizedAccessException("Requested tenant does not match authenticated tenant.");

            return requestedTenantId;
        }

        if (requestedTenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.");

        return requestedTenantId;
    }

    public static void EnsureBatchMatchesTenant(IEnumerable<ProductUpsertRequest> requests, ClaimsPrincipal? user)
    {
        var hasTenantClaim = TryGetTenantClaim(user, out var claimTenantId, out var claimError);
        if (!string.IsNullOrWhiteSpace(claimError))
            throw new ArgumentException(claimError);

        var isAuthenticated = user?.Identity?.IsAuthenticated == true;
        if (isAuthenticated && !hasTenantClaim)
            throw new UnauthorizedAccessException("Authenticated requests must include a tenant claim.");

        if (!hasTenantClaim)
            return;

        foreach (var request in requests)
        {
            if (request.TenantId != claimTenantId)
                throw new UnauthorizedAccessException("TenantId in payload must match authenticated tenant.");
        }
    }

    private static bool TryGetTenantClaim(ClaimsPrincipal? user, out Guid tenantId, out string? error)
    {
        tenantId = Guid.Empty;
        error = null;

        if (user is null)
            return false;

        foreach (var claimType in TenantClaimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (!Guid.TryParse(value, out tenantId))
            {
                error = $"Tenant claim '{claimType}' must be a valid GUID.";
                return false;
            }

            return true;
        }

        return false;
    }
}
