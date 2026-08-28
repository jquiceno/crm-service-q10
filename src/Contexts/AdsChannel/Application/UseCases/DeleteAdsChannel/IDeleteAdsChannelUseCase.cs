using Shared.Results;

namespace AdsChannel.Application.UseCases.DeleteAdsChannel;

public interface IDeleteAdsChannelUseCase
{
    Task<Result> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}
