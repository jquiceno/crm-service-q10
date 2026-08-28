using LossReason.Domain.Repositories;
using Shared.Results;

namespace LossReason.Application.UseCases.GetLossReasonById;

public sealed class GetLossReasonByIdUseCase(ILossReasonRepository repository) : IGetLossReasonByIdUseCase
{
    public async Task<Result<GetLossReasonByIdOutputDto>> ExecuteAsync(
        int id, CancellationToken cancellationToken = default)
    {
        var result = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return result.Error;

        return result.Value.ToOutputDto();
    }
}
