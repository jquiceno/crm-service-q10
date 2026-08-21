using Shared.Results;

namespace BusinessStatus.Application.UseCases.CreateBusinessStatus;

public interface ICreateBusinessStatusUseCase
{
    Task<Result<CreateBusinessStatusOutputDto>> ExecuteAsync(
        CreateBusinessStatusInputDto input, CancellationToken cancellationToken = default);
}
