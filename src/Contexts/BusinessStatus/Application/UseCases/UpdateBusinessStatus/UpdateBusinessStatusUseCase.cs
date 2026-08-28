using BusinessStatus.Domain.Errors;
using BusinessStatus.Domain.Repositories;
using Shared.Application.Ports;
using Shared.Results;

namespace BusinessStatus.Application.UseCases.UpdateBusinessStatus;

public sealed class UpdateBusinessStatusUseCase(
    IBusinessStatusRepository repository,
    IUnitOfWorkPort unitOfWork) : IUpdateBusinessStatusUseCase
{
    private const string Origin = nameof(UpdateBusinessStatusUseCase);

    public async Task<Result<UpdateBusinessStatusOutputDto>> ExecuteAsync(
        int id, UpdateBusinessStatusInputDto input, CancellationToken cancellationToken = default)
    {
        var getResult = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (getResult.IsFailure)
            return getResult.Error;

        var aggregate = getResult.Value;

        var updateResult = aggregate.Update(input.ToUpdateArgs());
        if (updateResult.IsFailure)
            return updateResult.Error with { Context = BusinessStatusErrors.Context, Origin = Origin };

        var persistResult = repository.Update(aggregate);
        if (persistResult.IsFailure)
            return persistResult.Error;

        var commitResult = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
            return commitResult.Error;

        return aggregate.ToOutputDto();
    }
}
