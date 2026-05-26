using Api.Results;
using Health.Application.Ports;
using Health.Application.UseCases.GetHealthInfo;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Health check endpoints without dependency on the database or other external services, providing basic liveness information about the application.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public sealed class HealthController() : ControllerBase
{
    [HttpGet("info")]
    [Tags("health")]
    [ProducesResponseType(typeof(HttpOkResult<GetHealthInfoOutputDto>), StatusCodes.Status200OK)]
    [EndpointSummary("Get service health info")]
    [EndpointDescription("Returns basic liveness information: status, service name, and version.")]
    public async Task<HttpOkResult<GetHealthInfoOutputDto>> GetInfo(
        IGetHealthInfoPort getHealthInfoPort,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(getHealthInfoPort);
        return await getHealthInfoPort.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
}
