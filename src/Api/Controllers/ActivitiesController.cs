using Activities.Application.UseCases.CreateActivity;
using Activities.Application.UseCases.GetActivities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Shared.Application.Dtos;
using Shared.Domain.Pagination;
using Shared.Presentation.Attributes;
using Shared.Presentation.Responses;
using Shared.Presentation.Results;

namespace Api.Controllers;

/// <summary>
/// The commercial activity log of a deal: what is planned to be done with a customer and what was
/// actually done.
/// </summary>
/// <remarks>
/// These two endpoints are the first front of the strangler over the monolith's
/// <c>api/actividades</c>. The Spanish contract stays in the monolith, which adapts and delegates
/// here per institution (DEC-10).
/// <para>
/// <b>Not authenticated yet.</b> DEC-9 requires every endpoint of this context to demand identity,
/// but the service has no authentication scheme configured at all, and how the monolith adapter
/// authenticates against it is still GAP-P10. Both must be resolved before any traffic is cut over
/// — the deployment is what keeps these endpoints private in the meantime.
/// </para>
/// </remarks>
[ApiController]
[Route("[controller]")]
[Tags("activities")]
public sealed class ActivitiesController(
    IGetActivitiesUseCase getActivitiesUseCase,
    ICreateActivityUseCase createActivityUseCase) : ControllerBase
{
    /// <summary>
    /// Lists activities.
    /// </summary>
    /// <remarks>
    /// Explicitly not cached. The service's base output-cache policy varies the key by tenant and
    /// headers, never by the query string, so with caching on this endpoint would serve one
    /// filter's page for another's — and would even cache a 200 where a filterless request must
    /// answer 400.
    /// </remarks>
    [HttpGet]
    [OutputCache(NoStore = true)]
    [ValidateRequest]
    [EndpointSummary("Get activities")]
    [EndpointDescription(
        "Returns a paginated list of activities filtered by deal, opportunity or deal state. "
        + "At least one of the three filters is required. Rows whose deal or opportunity no longer "
        + "exists are not returned, matching the legacy stored procedure.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<PagedPayload<GetActivitiesOutputDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<HttpOkPagedResult<GetActivitiesOutputDto>> GetActivities(
        [FromQuery] GetActivitiesInputDto filter,
        [FromQuery] PageQueryInputDto pagination,
        CancellationToken cancellationToken = default)
    {
        return await getActivitiesUseCase.ExecuteAsync(
            filter,
            new PageQuery(pagination.PageIndex, pagination.PageSize),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records an activity, planned or already completed. Answers only the generated consecutive,
    /// like the legacy endpoint did.
    /// </summary>
    [HttpPost]
    [ValidateRequest]
    [EndpointSummary("Create activity")]
    [EndpointDescription(
        "Records an activity against a deal. A scheduled activity carries a description and a due "
        + "date; a completed one carries an outcome, and an outcome type when it is a call or a "
        + "meeting. An unknown deal or advisor answers 404; an archived opportunity, 400.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<CreateActivityOutputDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<HttpCreatedResult<CreateActivityOutputDto>> CreateActivity(
        [FromBody] CreateActivityInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await createActivityUseCase.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
    }
}
