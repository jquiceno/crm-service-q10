using Infrastructure.Persistence.EntityFramework.Common;
using Infrastructure.Persistence.EntityFramework.LossReasons.Entities;
using LossReason.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Results;

namespace Infrastructure.Persistence.EntityFramework.LossReasons;

/// <summary>
/// Checks whether a LossReason is currently assigned to at least one deal.
/// This is a read-only Reader over a foreign table; no Repository is created
/// for it because it is not an Aggregate of this context.
/// </summary>
public sealed class LossReasonUsageReader(
    ApplicationDbContext context,
    ILoggerPort<LossReasonUsageReader> logger) : ILossReasonUsageReader
{
    private const string Origin = nameof(LossReasonUsageReader);

    public async Task<Result<bool>> IsUsedAsync(
        int lossReasonId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isUsed = await context.Set<DealLossReasonUsage>()
                .AsNoTracking()
                .AnyAsync(x => x.LossReasonId == lossReasonId, cancellationToken)
                .ConfigureAwait(false);

            return Result<bool>.Success(isUsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking usage for LossReason with id {LossReasonId}", lossReasonId);
            return PersistenceErrors.Failure(Origin);
        }
    }
}
