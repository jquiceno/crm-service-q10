using Shared.Results;

namespace LossReason.Application.UseCases.CreateLossReason;

public interface ICreateLossReasonUseCase
{
    Task<Result<CreateLossReasonOutputDto>> ExecuteAsync(
        CreateLossReasonInputDto input,
        CancellationToken cancellationToken = default);
}
