using Shared.Results;

namespace BusinessStatus.Application.UseCases.GetBusinessStatusById;

public interface IGetBusinessStatusByIdUseCase
{
    Task<Result<GetBusinessStatusByIdOutputDto>> ExecuteAsync(
        int id, CancellationToken cancellationToken = default);
}
