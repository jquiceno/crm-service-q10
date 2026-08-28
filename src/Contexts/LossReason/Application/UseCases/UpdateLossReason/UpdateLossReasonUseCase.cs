using LossReason.Domain.Errors;
using LossReason.Domain.Repositories;
using Shared.Application.Ports;
using Shared.Results;

namespace LossReason.Application.UseCases.UpdateLossReason;

public sealed class UpdateLossReasonUseCase(
    ILossReasonRepository repository,
    IUnitOfWorkPort unitOfWork) : IUpdateLossReasonUseCase
{
    private const string Origin = nameof(UpdateLossReasonUseCase);

    public async Task<Result<UpdateLossReasonOutputDto>> ExecuteAsync(
        int id,
        UpdateLossReasonInputDto input,
        CancellationToken cancellationToken = default)
    {
        var aggregateResult = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (aggregateResult.IsFailure)
            return aggregateResult.Error;

        // The aggregate is mutated through its own method, never replaced by a new instance.
        var aggregate = aggregateResult.Value;

        var updateResult = aggregate.Update(input.ToUpdateArgs());
        if (updateResult.IsFailure)
            return updateResult.Error with { Context = LossReasonErrors.Context, Origin = Origin };

        var saveResult = repository.Update(aggregate);
        if (saveResult.IsFailure)
            return saveResult.Error;

        var commitResult = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
            return commitResult.Error;

        return aggregate.ToOutputDto();
    }
}
