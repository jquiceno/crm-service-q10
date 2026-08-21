using Shared.Results;

namespace AdsChannel.Application.UseCases.CreateAdsChannel;

public interface ICreateAdsChannelUseCase
{
    Task<Result<CreateAdsChannelOutputDto>> ExecuteAsync(
        CreateAdsChannelInputDto input, CancellationToken cancellationToken = default);
}
