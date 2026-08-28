using AdsChannel.Application.UseCases.CreateAdsChannel;
using AdsChannel.Application.UseCases.DeleteAdsChannel;
using AdsChannel.Application.UseCases.GetAdsChannelById;
using AdsChannel.Application.UseCases.UpdateAdsChannel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Shared.Presentation.Attributes;
using Shared.Presentation.Filters;
using Shared.Presentation.Responses;
using Shared.Presentation.Results;

namespace Api.Controllers;

[ApiController]
[Route("[controller]")]
[Tags("AdsChannels")]
public sealed class AdsChannelsController(
    ICreateAdsChannelUseCase createAdsChannelUseCase,
    IUpdateAdsChannelUseCase updateAdsChannelUseCase,
    IDeleteAdsChannelUseCase deleteAdsChannelUseCase,
    IGetAdsChannelByIdUseCase getAdsChannelByIdUseCase) : ControllerBase
{
    private const string CacheTag = "ads-channels";

    [HttpPost]
    [ValidateRequest]
    [OutputCacheInvalidate(CacheTag)]
    [EndpointSummary("Create ads channel")]
    [EndpointDescription("Creates a new ads channel.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<CreateAdsChannelOutputDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<HttpCreatedResult<CreateAdsChannelOutputDto>> CreateAdsChannel(
        [FromBody] CreateAdsChannelInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await createAdsChannelUseCase.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
    }

    [HttpPut("{id}")]
    [ValidateRequest]
    [OutputCacheInvalidate(CacheTag)]
    [EndpointSummary("Update ads channel")]
    [EndpointDescription("Updates an existing ads channel.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<UpdateAdsChannelOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<HttpOkResult<UpdateAdsChannelOutputDto>> UpdateAdsChannel(
        [FromRoute] int id,
        [FromBody] UpdateAdsChannelInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await updateAdsChannelUseCase.ExecuteAsync(id, input, cancellationToken).ConfigureAwait(false);
    }

    [HttpDelete("{id}")]
    [OutputCacheInvalidate(CacheTag)]
    [EndpointSummary("Delete ads channel")]
    [EndpointDescription("Deletes an existing ads channel.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<HttpNoContentResult> DeleteAdsChannel(
        [FromRoute] int id,
        CancellationToken cancellationToken = default)
    {
        return await deleteAdsChannelUseCase.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("{id}")]
    [OutputCache(Duration = 120, Tags = [CacheTag], VaryByRouteValueNames = ["id"])]
    [EndpointSummary("Get ads channel by id")]
    [EndpointDescription("Returns a single ads channel by its identifier.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<GetAdsChannelByIdOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<HttpOkResult<GetAdsChannelByIdOutputDto>> GetAdsChannelById(
        [FromRoute] int id,
        CancellationToken cancellationToken = default)
    {
        return await getAdsChannelByIdUseCase.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
    }
}
