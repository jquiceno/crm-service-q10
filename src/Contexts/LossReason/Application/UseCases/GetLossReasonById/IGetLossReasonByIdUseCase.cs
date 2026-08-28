using Shared.Results;

namespace LossReason.Application.UseCases.GetLossReasonById;

public interface IGetLossReasonByIdUseCase
{
    Task<Result<GetLossReasonByIdOutputDto>> ExecuteAsync(
        int id, CancellationToken cancellationToken = default);
}
