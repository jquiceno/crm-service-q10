using Shared.Results;

namespace LossReason.Application.UseCases.UpdateLossReason;

public interface IUpdateLossReasonUseCase
{
    Task<Result<UpdateLossReasonOutputDto>> ExecuteAsync(
        int id,
        UpdateLossReasonInputDto input,
        CancellationToken cancellationToken = default);
}
