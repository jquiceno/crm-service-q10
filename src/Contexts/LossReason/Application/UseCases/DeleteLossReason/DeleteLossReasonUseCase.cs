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
        // Existence first, the usage check second. Inverting this makes every 404 pay a full scan
        // of ~300.000 rows, because neg_cau_consecutivo is not indexed (D7, risk R2).
        var exists = await repository.ExistsAsync(id, cancellationToken).ConfigureAwait(false);
        if (exists.IsFailure)
            return exists.Error;

        if (!exists.Value)
            return LossReasonErrors.NotFound(id) with { Origin = Origin };

        var isUsed = await usageReader.IsUsedAsync(id, cancellationToken).ConfigureAwait(false);
        if (isUsed.IsFailure)
            return isUsed.Error;

        // The FK is NO_ACTION, so deleting a reason still assigned to a deal would fail with SQL
        // Server error 547. This turns that into a Conflict before the delete is ever staged (D7).
        if (isUsed.Value)
            return LossReasonErrors.InUse(id) with { Origin = Origin };

        var removed = await repository.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
        if (removed.IsFailure)
            return removed.Error;

        return await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
