using Shared.Results;

namespace AdsChannel.Application.UseCases.GetAdsChannelById;

public interface IGetAdsChannelByIdUseCase
{
    Task<Result<GetAdsChannelByIdOutputDto>> ExecuteAsync(
        int id, CancellationToken cancellationToken = default);
}
