using Loyalty.Api.Modules.RewardCatalog.Application;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Api.Modules.RewardCatalog.Api;

/// <summary>Receives reward catalog products via HTTP and CSV upload.</summary>
[ApiController]
[Route("api/v1/rewards/catalog")]
public class RewardCatalogController : ControllerBase
{
    private readonly RewardCatalogService _service;
    private readonly IHostEnvironment _env;

    public RewardCatalogController(RewardCatalogService service, IHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    [HttpPost("upsert")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upsert([FromBody] RewardProductUpsertBatchRequest request, CancellationToken ct)
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

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest("CSV file is required.");

        try
        {
            await using var stream = file.OpenReadStream();
            var products = await RewardCatalogCsvParser.ParseAsync(stream, ct);
            await _service.UpsertAsync(products, ct);
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
