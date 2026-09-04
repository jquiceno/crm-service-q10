using LossReason.Application.Ports;
using LossReason.Domain.Errors;
using LossReason.Domain.Repositories;
using Shared.Application.Ports;
using Shared.Results;

namespace LossReason.Application.UseCases.DeleteLossReason;

public sealed class DeleteLossReasonUseCase(
    ILossReasonRepository repository,
    ILossReasonUsageReader usageReader,
    IUnitOfWorkPort unitOfWork) : IDeleteLossReasonUseCase
{
    private const string Origin = nameof(DeleteLossReasonUseCase);

    // The delete is idempotent on purpose: an id that is not there deletes nothing and still
    // answers 204, so existence is never checked — neither here nor in the repository.
    public async Task<Result> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var isUsed = await usageReader.IsUsedAsync(id, cancellationToken).ConfigureAwait(false);
        if (isUsed.IsFailure)
            return isUsed.Error;

        if (isUsed.Value)
            return LossReasonErrors.InUse(id) with { Origin = Origin };

        var removed = await repository.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
        if (removed.IsFailure)
            return removed.Error;

        return await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
