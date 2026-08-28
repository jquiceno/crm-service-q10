using Shared.Results;

namespace LossReason.Application.UseCases.DeleteLossReason;

public interface IDeleteLossReasonUseCase
{
    Task<Result> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}
