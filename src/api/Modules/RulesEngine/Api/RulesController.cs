using Loyalty.Api.Modules.RulesEngine.Application;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Api.Modules.RulesEngine.Api;

/// <summary>Manages points rules via HTTP.</summary>
[ApiController]
[Route("api/v1/rules/points")]
public class RulesController : ControllerBase
{
    private readonly PointsRuleService _service;
    private readonly IHostEnvironment _env;

    public RulesController(PointsRuleService service, IHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    [HttpPost("upsert")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upsert([FromBody] PointsRuleUpsertBatchRequest request, CancellationToken ct)
    {
        try
        {
            await _service.UpsertAsync(request.Rules, ct);
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
