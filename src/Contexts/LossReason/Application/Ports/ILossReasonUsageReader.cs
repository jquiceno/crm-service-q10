using Shared.Results;

namespace LossReason.Application.Ports;

public interface ILossReasonUsageReader
{
    Task<Result<bool>> IsUsedAsync(int lossReasonId, CancellationToken cancellationToken = default);
}
