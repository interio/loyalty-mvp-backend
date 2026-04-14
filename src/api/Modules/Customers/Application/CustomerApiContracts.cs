using System.ComponentModel.DataAnnotations;

namespace Loyalty.Api.Modules.Customers.Application;

/// <summary>Customer create payload for ERP/admin integrations.</summary>
public class CustomerCreateRequest
{
    [Required] public Guid TenantId { get; set; }
    [Required] public string Name { get; set; } = default!;
    public string? ContactEmail { get; set; }
    public string? ExternalId { get; set; }
    public string? Tier { get; set; }
    public CustomerAddressRequest? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Type { get; set; }
    public string? BusinessSegment { get; set; }
    public DateTimeOffset? OnboardDate { get; set; }
    [Range(0, 2)] public int? Status { get; set; }
}

/// <summary>Customer update payload for ERP/admin integrations.</summary>
public class CustomerUpdateRequest
{
    [Required] public Guid TenantId { get; set; }
    [Required] public string Name { get; set; } = default!;
    public string? ContactEmail { get; set; }
    public string? ExternalId { get; set; }
    public string? Tier { get; set; }
    public CustomerAddressRequest? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Type { get; set; }
    public string? BusinessSegment { get; set; }
    public DateTimeOffset? OnboardDate { get; set; }
    [Range(0, 2)] public int? Status { get; set; }
}

/// <summary>Address payload used by customer REST create/update APIs.</summary>
public class CustomerAddressRequest
{
    public string? Address { get; set; }
    public string? CountryCode { get; set; }
    public string? PostalCode { get; set; }
    public string? Region { get; set; }
}

/// <summary>Customer response payload for REST create/update APIs.</summary>
public class CustomerResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? ContactEmail { get; set; }
    public string? ExternalId { get; set; }
    public string Tier { get; set; } = default!;
    public CustomerAddressResponse? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Type { get; set; }
    public string? BusinessSegment { get; set; }
    public DateTimeOffset OnboardDate { get; set; }
    public int Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Address response payload for customer REST APIs.</summary>
public class CustomerAddressResponse
{
    public string? Address { get; set; }
    public string? CountryCode { get; set; }
    public string? PostalCode { get; set; }
    public string? Region { get; set; }
}
