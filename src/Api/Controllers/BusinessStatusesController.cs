using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using BusinessStatus.Application.UseCases.DeleteBusinessStatus;
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
    ICreateBusinessStatusUseCase createBusinessStatusUseCase,
    IDeleteBusinessStatusUseCase deleteBusinessStatusUseCase) : ControllerBase
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

    [HttpDelete("{id}")]
    [EndpointSummary("Delete business status")]
    [EndpointDescription(
        "Deletes the business status with the given id. A terminal status — the one at 0 % or at 100 % — "
        + "answers 409, and so does a status still referenced by a business.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCacheInvalidate(CacheTag)]
    public async Task<HttpNoContentResult> DeleteBusinessStatus(
        [FromRoute] int id,
        CancellationToken cancellationToken = default)
    {
        return await deleteBusinessStatusUseCase.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
    }
}
