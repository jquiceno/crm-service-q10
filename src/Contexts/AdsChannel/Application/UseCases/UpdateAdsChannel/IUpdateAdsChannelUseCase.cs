using Shared.Results;

namespace AdsChannel.Application.UseCases.UpdateAdsChannel;

public interface IUpdateAdsChannelUseCase
{
    Task<Result<UpdateAdsChannelOutputDto>> ExecuteAsync(
        int id, UpdateAdsChannelInputDto input, CancellationToken cancellationToken = default);
}
