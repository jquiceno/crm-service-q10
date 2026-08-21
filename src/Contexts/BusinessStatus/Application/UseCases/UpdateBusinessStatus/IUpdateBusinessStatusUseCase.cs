using Shared.Results;

namespace BusinessStatus.Application.UseCases.UpdateBusinessStatus;

public interface IUpdateBusinessStatusUseCase
{
    Task<Result<UpdateBusinessStatusOutputDto>> ExecuteAsync(
        int id, UpdateBusinessStatusInputDto input, CancellationToken cancellationToken = default);
}
