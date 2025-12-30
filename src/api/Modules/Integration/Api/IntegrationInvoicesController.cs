using Loyalty.Api.Modules.Integration.Application;
using Loyalty.Api.Modules.Integration.Application.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Api.Modules.Integration.Api;

/// <summary>
/// Receives invoices from MuleSoft/ERP, applies points using backend rules, and posts ledger entries idempotently.
/// </summary>
[ApiController]
[Route("api/v1/integration/invoices")]
public class IntegrationInvoicesController : ControllerBase
{
    private readonly PointsPostingService _service;
    private readonly IHostEnvironment _env;

    /// <summary>Create the invoices controller.</summary>
    public IntegrationInvoicesController(PointsPostingService service, IHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    /// <summary>
    /// Applies an invoice (idempotent per tenant+invoiceId): calculates points, posts ledger, returns new balance.
    /// </summary>
    [HttpPost("apply")]
    [ProducesResponseType(typeof(InvoiceUpsertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Apply([FromBody] InvoiceUpsertRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.ApplyInvoiceAsync(request, ct);
            return Ok(result);
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
}
