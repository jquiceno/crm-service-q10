using Shared.Results;

namespace BusinessStatus.Application.UseCases.DeleteBusinessStatus;

public interface IDeleteBusinessStatusUseCase
{
    Task<Result> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}
