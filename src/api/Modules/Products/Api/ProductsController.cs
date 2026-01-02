using Loyalty.Api.Modules.Products.Application;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Api.Modules.Products.Api;

/// <summary>
/// Receives products from ERP/ETL, stores them locally for loyalty rules. Future: also consume from queues.
/// </summary>
[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _service;
    private readonly IHostEnvironment _env;

    public ProductsController(ProductService service, IHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    /// <summary>Upserts a batch of products for a distributor.</summary>
    [HttpPost("upsert")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upsert([FromBody] ProductUpsertBatchRequest request, CancellationToken ct)
    {
        try
        {
            await _service.UpsertAsync(request.Products, ct);
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            if (_env.IsDevelopment())
                return StatusCode(StatusCodes.Status500InternalServerError, ex.ToString());

            return StatusCode(StatusCodes.Status500InternalServerError, "Unexpected error.");
        }
    }
}
