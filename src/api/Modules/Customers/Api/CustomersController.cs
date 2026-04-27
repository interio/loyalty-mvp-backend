using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Api.Modules.Customers.Api;

/// <summary>REST endpoints for ERP/customer master create and update flows.</summary>
[ApiController]
[Route("api/v1/customers")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;
    private readonly IHostEnvironment _env;

    public CustomersController(ICustomerService service, IHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    /// <summary>Creates a customer/outlet and seeds its points account.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromBody] CustomerCreateRequest request, CancellationToken ct)
    {
        try
        {
            var customer = await _service.CreateAsync(new CreateCustomerCommand(
                request.TenantId,
                request.Name,
                request.ContactEmail,
                request.ExternalId,
                request.Tier,
                request.Address is null
                    ? null
                    : new CustomerAddress
                    {
                        Address = request.Address.Address,
                        CountryCode = request.Address.CountryCode,
                        PostalCode = request.Address.PostalCode,
                        Region = request.Address.Region
                    },
                request.PhoneNumber,
                request.Type,
                request.BusinessSegment,
                request.OnboardDate,
                request.Status), ct);

            return Created($"/api/v1/customers/{customer.Id}", ToResponse(customer));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (System.Collections.Generic.KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            if (_env.IsDevelopment())
                return StatusCode(StatusCodes.Status500InternalServerError, ex.ToString());

            return StatusCode(StatusCodes.Status500InternalServerError, "Unexpected error.");
        }
    }

    /// <summary>Updates an existing customer profile (tenant-scoped).</summary>
    [HttpPut("{customerId:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(Guid customerId, [FromBody] CustomerUpdateRequest request, CancellationToken ct)
    {
        try
        {
            var customer = await _service.UpdateAsync(new UpdateCustomerCommand(
                customerId,
                request.TenantId,
                request.Name,
                request.ContactEmail,
                request.ExternalId,
                request.Tier,
                request.Address is null
                    ? null
                    : new CustomerAddress
                    {
                        Address = request.Address.Address,
                        CountryCode = request.Address.CountryCode,
                        PostalCode = request.Address.PostalCode,
                        Region = request.Address.Region
                    },
                request.PhoneNumber,
                request.Type,
                request.BusinessSegment,
                request.OnboardDate,
                request.Status), ct);

            return Ok(ToResponse(customer));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (System.Collections.Generic.KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            if (_env.IsDevelopment())
                return StatusCode(StatusCodes.Status500InternalServerError, ex.ToString());

            return StatusCode(StatusCodes.Status500InternalServerError, "Unexpected error.");
        }
    }

    private static CustomerResponse ToResponse(Customer customer) =>
        new()
        {
            Id = customer.Id,
            TenantId = customer.TenantId,
            Name = customer.Name,
            ContactEmail = customer.ContactEmail,
            ExternalId = customer.ExternalId,
            Tier = customer.Tier,
            Address = customer.Address is null
                ? null
                : new CustomerAddressResponse
                {
                    Address = customer.Address.Address,
                    CountryCode = customer.Address.CountryCode,
                    PostalCode = customer.Address.PostalCode,
                    Region = customer.Address.Region
                },
            PhoneNumber = customer.PhoneNumber,
            Type = customer.Type,
            BusinessSegment = customer.BusinessSegment,
            OnboardDate = customer.OnboardDate,
            Status = customer.Status,
            WelcomeBonusAwarded = customer.WelcomeBonusAwarded,
            WelcomeBonusAwardedAt = customer.WelcomeBonusAwardedAt,
            CreatedAt = customer.CreatedAt
        };
}
