using Shared.Results;

namespace Activities.Application.UseCases.CreateActivity;

/// <summary>Entry point of <c>POST /activities</c>, invoked by the controller.</summary>
public interface ICreateActivityUseCase
{
    Task<Result<CreateActivityOutputDto>> ExecuteAsync(
        CreateActivityInputDto input, CancellationToken cancellationToken = default);
}
