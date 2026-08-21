using ContactChannel.Application.UseCases.GetContactChannels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Shared.Application.Dtos;
using Shared.Domain.Pagination;
using Shared.Presentation.Attributes;
using Shared.Presentation.Responses;
using Shared.Presentation.Results;

namespace Api.Controllers;

/// <summary>
/// Endpoints for the contact channel catalog: the channel through which a prospect reached the institution.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Tags("ContactChannels")]
public sealed class ContactChannelsController(
    IGetContactChannelsUseCase getContactChannelsUseCase) : ControllerBase
{
    private const string CacheTag = "contact-channels";

    [HttpGet]
    [ValidateRequest]
    [EndpointSummary("Get contact channels")]
    [EndpointDescription("Returns a paginated and filtered list of contact channels. Omitting the state returns active and inactive channels alike.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<PagedPayload<GetContactChannelsOutputDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCache(Tags = [CacheTag], VaryByQueryKeys = ["*"])]
    public async Task<HttpOkPagedResult<GetContactChannelsOutputDto>> GetContactChannels(
        [FromQuery] GetContactChannelsInputDto filter,
        [FromQuery] PageQueryInputDto pagination,
        CancellationToken cancellationToken = default)
    {
        return await getContactChannelsUseCase.ExecuteAsync(
            filter,
            new PageQuery(pagination.PageIndex, pagination.PageSize),
            cancellationToken).ConfigureAwait(false);
    }
}
