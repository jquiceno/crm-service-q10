using AdsChannel.Domain.Queries;
using AdsChannel.Domain.Repositories;
using Shared.Domain.Pagination;
using Shared.Results;

namespace AdsChannel.Application.UseCases.GetAdsChannels;

public sealed class GetAdsChannelsUseCase(IAdsChannelRepository repository) : IGetAdsChannelsUseCase
{
    public async Task<PagedResult<AdsChannelOutputDto>> ExecuteAsync(
        GetAdsChannelsInputDto input, PageQuery page, CancellationToken cancellationToken = default)
    {
        var filter = new AdsChannelFilter(input.NameContains, input.IsActive);

        var result = await repository.GetAsync(filter, page, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return PagedResult<AdsChannelOutputDto>.Failure(result.Error);

        IReadOnlyList<AdsChannelOutputDto> items = [.. result.Items.Select(x => x.ToOutputDto())];

        return PagedResult<AdsChannelOutputDto>.Success(items, result.TotalCount);
    }
}
