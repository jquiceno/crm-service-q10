using AdsChannel.Application.UseCases.CreateAdsChannel;
using AdsChannel.Application.UseCases.UpdateAdsChannel;
using Microsoft.AspNetCore.Mvc;
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
    IUpdateAdsChannelUseCase updateAdsChannelUseCase) : ControllerBase
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
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<HttpOkResult<UpdateAdsChannelOutputDto>> UpdateAdsChannel(
        [FromRoute] int id,
        [FromBody] UpdateAdsChannelInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await updateAdsChannelUseCase.ExecuteAsync(id, input, cancellationToken).ConfigureAwait(false);
    }
}
