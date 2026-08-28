using AdsChannel.Domain.Repositories;
using Shared.Application.Ports;
using Shared.Results;

namespace AdsChannel.Application.UseCases.DeleteAdsChannel;

public sealed class DeleteAdsChannelUseCase(
    IAdsChannelRepository repository,
    IUnitOfWorkPort unitOfWork) : IDeleteAdsChannelUseCase
{
    public async Task<Result> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var removeResult = await repository.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
        if (removeResult.IsFailure)
            return removeResult.Error;

        return await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
