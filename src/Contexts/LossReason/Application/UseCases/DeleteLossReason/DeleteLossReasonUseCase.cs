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

    public async Task<Result> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var exists = await repository.ExistsAsync(id, cancellationToken).ConfigureAwait(false);
        if (exists.IsFailure)
            return exists.Error;

        if (!exists.Value)
            return LossReasonErrors.NotFound(id) with { Origin = Origin };

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
