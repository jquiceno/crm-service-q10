using AdsChannel.Application.UseCases.CreateAdsChannel;
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
    ICreateAdsChannelUseCase createAdsChannelUseCase) : ControllerBase
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
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<HttpCreatedResult<CreateAdsChannelOutputDto>> CreateAdsChannel(
        [FromBody] CreateAdsChannelInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await createAdsChannelUseCase.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
    }
}
