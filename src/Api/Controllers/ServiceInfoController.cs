using Shared.Presentation.Results;
using ServiceInfo.Application.Ports;
using ServiceInfo.Application.UseCases.GetServiceInfo;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Endpoints that expose basic information about this service (name, version, status) without depending on the database or other external services.
/// </summary>
[ApiController]
[Route("info")]
public sealed class ServiceInfoController() : ControllerBase
{
    [HttpGet]
    [Tags("serviceinfo")]
    [ProducesResponseType(typeof(GetServiceInfoOutputDto), StatusCodes.Status200OK)]
    [EndpointSummary("Get service info")]
    [EndpointDescription("Returns basic service information: status, service name, and version.")]
    public async Task<HttpOkResult<GetServiceInfoOutputDto>> GetInfo(
        IGetServiceInfoPort getServiceInfoPort,
        CancellationToken cancellationToken = default)
    {
        return await getServiceInfoPort.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
}
