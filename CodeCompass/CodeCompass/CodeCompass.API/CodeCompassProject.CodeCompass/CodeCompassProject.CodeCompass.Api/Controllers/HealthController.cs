using CodeCompassProject.CodeCompass.Application.CQRS;
using CodeCompassProject.CodeCompass.Application.DTOs;
using CodeCompassProject.CodeCompass.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CodeCompassProject.CodeCompass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IQueryHandler<GetHealthQuery, HealthResponse> _handler;

    public HealthController(IQueryHandler<GetHealthQuery, HealthResponse> handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Check the health of all CodeCompass services.
    /// </summary>
    /// <returns>Health status of each service.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> GetHealth(CancellationToken cancellationToken)
    {
        var result = await _handler.HandleAsync(new GetHealthQuery(), cancellationToken);

        return result.Status == "Unhealthy"
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, result)
            : Ok(result);
    }
}
