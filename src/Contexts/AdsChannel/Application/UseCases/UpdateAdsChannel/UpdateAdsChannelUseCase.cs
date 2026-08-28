using AdsChannel.Domain.Errors;
using AdsChannel.Domain.Repositories;
using Shared.Application.Ports;
using Shared.Results;

namespace AdsChannel.Application.UseCases.UpdateAdsChannel;

public sealed class UpdateAdsChannelUseCase(
    IAdsChannelRepository repository,
    IUnitOfWorkPort unitOfWork) : IUpdateAdsChannelUseCase
{
    private const string Origin = nameof(UpdateAdsChannelUseCase);

    public async Task<Result<UpdateAdsChannelOutputDto>> ExecuteAsync(
        int id, UpdateAdsChannelInputDto input, CancellationToken cancellationToken = default)
    {
        var getResult = await repository                                    // 1. load
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);
        if (getResult.IsFailure)
            return getResult.Error;                                         //    already sealed by the repository

        var aggregate = getResult.Value;

        var updateResult = aggregate.Update(input.ToUpdateArgs());          // 2. apply domain changes
        if (updateResult.IsFailure)
            return updateResult.Error with { Context = AdsChannelErrors.Context, Origin = Origin };

        // aggregate.Update() above already normalized the name (trimmed), so aggregate.Name is what
        // will actually be persisted — check and report that value, not the raw input, so a
        // leading/trailing-whitespace variant can't slip past this check.
        var existsResult = await repository                                 // 3. persistence-level rule
            .ExistsByNameAsync(aggregate.Name, excludingId: id, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (existsResult.IsFailure)
            return existsResult.Error;
        if (existsResult.Value)
            return AdsChannelErrors.NameAlreadyExists(aggregate.Name)
                with { Context = AdsChannelErrors.Context, Origin = Origin };

        var updateRepoResult = repository.Update(aggregate);                // 4. mark modified
        if (updateRepoResult.IsFailure)
            return updateRepoResult.Error;

        var commitResult = await unitOfWork                                 // 5. commit
            .CommitAsync(cancellationToken)
            .ConfigureAwait(false);
        if (commitResult.IsFailure)
            return commitResult.Error;

        return aggregate.ToOutputDto();
    }
}
