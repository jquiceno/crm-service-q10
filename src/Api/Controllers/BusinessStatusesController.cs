using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using BusinessStatus.Application.UseCases.GetBusinessStatusById;
using BusinessStatus.Application.UseCases.UpdateBusinessStatus;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
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
    ICreateBusinessStatusUseCase createBusinessStatusUseCase,
    IGetBusinessStatusByIdUseCase getBusinessStatusByIdUseCase,
    IUpdateBusinessStatusUseCase updateBusinessStatusUseCase) : ControllerBase
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

    [HttpGet("{id}")]
    [EndpointSummary("Get business status by id")]
    [EndpointDescription("Returns the business status with the given id. An unknown id answers 404.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<GetBusinessStatusByIdOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCache(Duration = 300, Tags = [CacheTag], VaryByRouteValueNames = ["id"])]
    public async Task<HttpOkResult<GetBusinessStatusByIdOutputDto>> GetBusinessStatusById(
        [FromRoute] int id,
        CancellationToken cancellationToken = default)
    {
        return await getBusinessStatusByIdUseCase.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    [HttpPut("{id:int}")]
    [ValidateRequest]
    [EndpointSummary("Update business status")]
    [EndpointDescription(
        "Replaces every field of an existing business status and returns it as it stands afterwards. "
        + "A percentage of 0 or 100 is rejected because those values are reserved for the terminal "
        + "statuses, and the percentage of a status that already is terminal cannot be changed.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<UpdateBusinessStatusOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCacheInvalidate(CacheTag)]
    public async Task<HttpOkResult<UpdateBusinessStatusOutputDto>> UpdateBusinessStatus(
        [FromRoute] int id,
        [FromBody] UpdateBusinessStatusInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await updateBusinessStatusUseCase.ExecuteAsync(id, input, cancellationToken).ConfigureAwait(false);
    }
}
