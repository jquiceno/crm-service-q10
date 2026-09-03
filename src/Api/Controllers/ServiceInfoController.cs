using Shared.Presentation.Responses;
using Shared.Presentation.Results;
using ServiceInfo.Application.UseCases.GetServiceInfo;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Endpoints that expose basic information about this service (name, version, status) without depending on the database or other external services.
/// </summary>
[ApiController]
[Route("info")]
[Tags("serviceinfo")]
public sealed class ServiceInfoController(
    IGetServiceInfoUseCase getServiceInfoUseCase) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiSuccessResponse<GetServiceInfoOutputDto>), StatusCodes.Status200OK)]
    [EndpointSummary("Get service info")]
    [EndpointDescription("Returns basic service information: status, service name, and version.")]
    public async Task<HttpOkResult<GetServiceInfoOutputDto>> GetInfo(
        CancellationToken cancellationToken = default)
    {
        return await getServiceInfoUseCase.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
}
