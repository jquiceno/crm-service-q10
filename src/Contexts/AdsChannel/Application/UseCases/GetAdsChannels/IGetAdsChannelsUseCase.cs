using Shared.Domain.Pagination;
using Shared.Results;

namespace AdsChannel.Application.UseCases.GetAdsChannels;

public interface IGetAdsChannelsUseCase
{
    Task<PagedResult<GetAdsChannelsOutputDto>> ExecuteAsync(
        GetAdsChannelsInputDto input, PageQuery page, CancellationToken cancellationToken = default);
}
