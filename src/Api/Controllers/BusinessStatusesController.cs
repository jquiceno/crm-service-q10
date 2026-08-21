using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using Microsoft.AspNetCore.Mvc;
using Shared.Presentation.Attributes;
using Shared.Presentation.Filters;
using Shared.Presentation.Responses;
using Shared.Presentation.Results;

namespace Api.Controllers;

/// <summary>
/// Endpoints over the business status catalogue: the ordered stages a business goes through until it
/// closes. The percentage carries the meaning — 0 is Lost and 100 is Won — so both limits are
/// reserved and this service never assigns them.
/// </summary>
[ApiController]
[Route("[controller]")]
[Tags("business-statuses")]
public sealed class BusinessStatusesController(
    ICreateBusinessStatusUseCase createBusinessStatusUseCase) : ControllerBase
{
    private const string CacheTag = "business-statuses";

    [HttpPost]
    [ValidateRequest]
    [EndpointSummary("Create business status")]
    [EndpointDescription(
        "Creates a business status and returns it with the identifier the database assigned. "
        + "A percentage of 0 or 100 is rejected: those values are reserved for the terminal statuses.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<CreateBusinessStatusOutputDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCacheInvalidate(CacheTag)]
    public async Task<HttpCreatedResult<CreateBusinessStatusOutputDto>> CreateBusinessStatus(
        [FromBody] CreateBusinessStatusInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await createBusinessStatusUseCase.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
    }
}
