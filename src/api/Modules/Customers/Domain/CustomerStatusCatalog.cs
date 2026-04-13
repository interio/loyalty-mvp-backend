namespace Loyalty.Api.Modules.Customers.Domain;

/// <summary>Status codes for customer lifecycle.</summary>
public static class CustomerStatusCatalog
{
    public const int Inactive = 0;
    public const int Active = 1;
    public const int Suspended = 2;

    public static bool IsSupported(int status) =>
        status is Active or Inactive or Suspended;
}
