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

    [HttpPost("complex")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateComplexRule([FromBody] ComplexRuleCreateRequest request, CancellationToken ct)
    {
        try
        {
            var id = await _service.CreateComplexRuleAsync(request, ct);
            return Ok(new { id });
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

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PointsRuleStatusUpdateRequest request, CancellationToken ct)
    {
        try
        {
            if (request.TenantId == Guid.Empty)
                return BadRequest("tenantId is required.");

            await _service.SetActiveAsync(id, request.TenantId, request.Active, ct);
            return Ok();
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

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid tenantId, CancellationToken ct)
    {
        try
        {
            if (tenantId == Guid.Empty)
                return BadRequest("tenantId is required.");

            await _service.DeleteAsync(id, tenantId, ct);
            return Ok();
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

public class PointsRuleStatusUpdateRequest
{
    public Guid TenantId { get; set; }
    public bool Active { get; set; }
}
