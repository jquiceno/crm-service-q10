using AdsChannel.Domain.Repositories;
using Shared.Results;

namespace AdsChannel.Application.UseCases.GetAdsChannelById;

public sealed class GetAdsChannelByIdUseCase(IAdsChannelRepository repository) : IGetAdsChannelByIdUseCase
{
    public async Task<Result<GetAdsChannelByIdOutputDto>> ExecuteAsync(
        int id, CancellationToken cancellationToken = default)
    {
        var result = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return result.Error;

        return result.Value.ToOutputDto();
    }
}
