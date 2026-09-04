using LossReason.Application.UseCases.CreateLossReason;
using LossReason.Application.UseCases.DeleteLossReason;
using LossReason.Application.UseCases.GetLossReasonById;
using LossReason.Application.UseCases.GetLossReasons;
using LossReason.Application.UseCases.UpdateLossReason;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Shared.Application.Dtos;
using Shared.Domain.Pagination;
using Shared.Presentation.Attributes;
using Shared.Presentation.Filters;
using Shared.Presentation.Responses;
using Shared.Presentation.Results;

namespace Api.Controllers;

/// <summary>
/// Catalog of loss reasons: the reasons a deal can be marked as lost with.
/// </summary>
[ApiController]
[Route("[controller]")]
[Tags("LossReasons")]
public sealed class LossReasonsController(
    IGetLossReasonsUseCase getLossReasonsUseCase,
    IGetLossReasonByIdUseCase getLossReasonByIdUseCase,
    ICreateLossReasonUseCase createLossReasonUseCase,
    IUpdateLossReasonUseCase updateLossReasonUseCase,
    IDeleteLossReasonUseCase deleteLossReasonUseCase) : ControllerBase
{
    private const string CacheTag = "loss-reasons";

    private const int CacheDurationSeconds = 3 * 24 * 60 * 60;

    // The route id travels as SequenceIdInputDto, not as a bare int: ValidateRequestFilter skips
    // simple types, so a validator over an int would never run. Wrapped, SequenceIdInputValidator
    // applies and the three actions that take an id carry [ValidateRequest]. A route constraint would
    // answer 404 instead, hiding a malformed id as a missing resource.

    [HttpGet]
    [ValidateRequest]
    [EndpointSummary("Get loss reasons")]
    [EndpointDescription("Returns a paginated list of loss reasons, optionally filtered by name and state. An empty catalog answers 200 with an empty item list.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<PagedPayload<GetLossReasonsOutputDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCache(Duration = CacheDurationSeconds, Tags = [CacheTag])]
    public async Task<HttpOkPagedResult<GetLossReasonsOutputDto>> GetLossReasons(
        [FromQuery] GetLossReasonsInputDto filter,
        [FromQuery] PageQueryInputDto pagination,
        CancellationToken cancellationToken = default)
    {
        return await getLossReasonsUseCase.ExecuteAsync(
            filter,
            new PageQuery(pagination.PageIndex, pagination.PageSize),
            cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("{id}")]
    [ValidateRequest]
    [EndpointSummary("Get loss reason by id")]
    [EndpointDescription("Returns the loss reason with the given id.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<GetLossReasonByIdOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCache(Duration = CacheDurationSeconds, Tags = [CacheTag])]
    public async Task<HttpOkResult<GetLossReasonByIdOutputDto>> GetLossReasonById(
        [FromRoute] SequenceIdInputDto route,
        CancellationToken cancellationToken = default)
    {
        return await getLossReasonByIdUseCase.ExecuteAsync(route.Id, cancellationToken).ConfigureAwait(false);
    }

    [HttpPost]
    [ValidateRequest]
    [EndpointSummary("Create loss reason")]
    [EndpointDescription("Creates a loss reason and returns it with the identifier the database assigned.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<CreateLossReasonOutputDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCacheInvalidate(CacheTag)]
    public async Task<HttpCreatedResult<CreateLossReasonOutputDto>> CreateLossReason(
        [FromBody] CreateLossReasonInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await createLossReasonUseCase.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
    }

    [HttpPut("{id}")]
    [ValidateRequest]
    [EndpointSummary("Update loss reason")]
    [EndpointDescription("Updates the name and state of the loss reason with the given id. An unknown id answers 404.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<UpdateLossReasonOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCacheInvalidate(CacheTag)]
    public async Task<HttpOkResult<UpdateLossReasonOutputDto>> UpdateLossReason(
        [FromRoute] SequenceIdInputDto route,
        [FromBody] UpdateLossReasonInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await updateLossReasonUseCase.ExecuteAsync(route.Id, input, cancellationToken).ConfigureAwait(false);
    }

    [HttpDelete("{id}")]
    [ValidateRequest]
    [EndpointSummary("Delete loss reason")]
    [EndpointDescription("Deletes the loss reason with the given id. A reason already assigned to a deal answers 409 and is not deleted.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCacheInvalidate(CacheTag)]
    public async Task<HttpNoContentResult> DeleteLossReason(
        [FromRoute] SequenceIdInputDto route,
        CancellationToken cancellationToken = default)
    {
        return await deleteLossReasonUseCase.ExecuteAsync(route.Id, cancellationToken).ConfigureAwait(false);
    }
}
