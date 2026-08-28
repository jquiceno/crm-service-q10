using ContactChannel.Application.UseCases.CreateContactChannel;
using ContactChannel.Application.UseCases.GetContactChannelById;
using ContactChannel.Application.UseCases.GetContactChannels;
using ContactChannel.Application.UseCases.UpdateContactChannel;
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
/// Endpoints for the contact channel catalog: the channel through which a prospect reached the institution.
/// </summary>
[ApiController]
[Route("[controller]")]
[Tags("ContactChannels")]
public sealed class ContactChannelsController(
    IGetContactChannelsUseCase getContactChannelsUseCase,
    IGetContactChannelByIdUseCase getContactChannelByIdUseCase,
    ICreateContactChannelUseCase createContactChannelUseCase,
    IUpdateContactChannelUseCase updateContactChannelUseCase) : ControllerBase
{
    private const string CacheTag = "contact-channels";

    [HttpGet]
    [ValidateRequest]
    [EndpointSummary("Get contact channels")]
    [EndpointDescription("Returns a paginated and filtered list of contact channels. Omitting the state returns active and inactive channels alike.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<PagedPayload<GetContactChannelsOutputDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCache(Tags = [CacheTag], Duration = 259200)]
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

    [HttpGet("{id:int}")]
    [EndpointSummary("Get contact channel by id")]
    [EndpointDescription("Returns the contact channel with the given identifier.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<GetContactChannelByIdOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCache(Tags = [CacheTag], Duration = 259200)]
    public async Task<HttpOkResult<GetContactChannelByIdOutputDto>> GetContactChannelById(
        [FromRoute] int id,
        CancellationToken cancellationToken = default)
    {
        return await getContactChannelByIdUseCase.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    [HttpPost]
    [ValidateRequest]
    [EndpointSummary("Create contact channel")]
    [EndpointDescription("Creates a contact channel and returns it with the identifier the database generated. A name that already exists is accepted: the catalog does not require unique names.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<CreateContactChannelOutputDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCacheInvalidate(CacheTag)]
    public async Task<HttpCreatedResult<CreateContactChannelOutputDto>> CreateContactChannel(
        [FromBody] CreateContactChannelInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await createContactChannelUseCase
            .ExecuteAsync(input, cancellationToken)
            .ConfigureAwait(false);
    }

    [HttpPut("{id:int}")]
    [ValidateRequest]
    [EndpointSummary("Update contact channel")]
    [EndpointDescription("Updates the name and the state of the contact channel with the given identifier. An unknown identifier answers 404.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<UpdateContactChannelOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [OutputCacheInvalidate(CacheTag)]
    public async Task<HttpOkResult<UpdateContactChannelOutputDto>> UpdateContactChannel(
        [FromRoute] int id,
        [FromBody] UpdateContactChannelInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await updateContactChannelUseCase
            .ExecuteAsync(id, input, cancellationToken)
            .ConfigureAwait(false);
    }
}
