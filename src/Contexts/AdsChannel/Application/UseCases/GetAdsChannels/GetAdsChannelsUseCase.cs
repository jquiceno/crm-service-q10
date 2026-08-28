using AdsChannel.Domain.Queries;
using AdsChannel.Domain.Repositories;
using Shared.Domain.Pagination;
using Shared.Results;

namespace AdsChannel.Application.UseCases.GetAdsChannels;

public sealed class GetAdsChannelsUseCase(IAdsChannelRepository repository) : IGetAdsChannelsUseCase
{
    public async Task<PagedResult<GetAdsChannelsOutputDto>> ExecuteAsync(
        GetAdsChannelsInputDto input, PageQuery page, CancellationToken cancellationToken = default)
    {
        var filter = new AdsChannelFilter(input.NameContains, input.IsActive);

        var result = await repository.GetAsync(filter, page, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return PagedResult<GetAdsChannelsOutputDto>.Failure(result.Error);

        IReadOnlyList<GetAdsChannelsOutputDto> items = [.. result.Items.Select(x => x.ToOutputDto())];

        return PagedResult<GetAdsChannelsOutputDto>.Success(items, result.TotalCount);
    }
}
