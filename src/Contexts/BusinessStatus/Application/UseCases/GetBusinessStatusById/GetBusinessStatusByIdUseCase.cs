using BusinessStatus.Domain.Repositories;
using Shared.Results;

namespace BusinessStatus.Application.UseCases.GetBusinessStatusById;

public sealed class GetBusinessStatusByIdUseCase(IBusinessStatusRepository repository)
    : IGetBusinessStatusByIdUseCase
{
    public async Task<Result<GetBusinessStatusByIdOutputDto>> ExecuteAsync(
        int id, CancellationToken cancellationToken = default)
    {
        var result = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        // The repository produced the NotFound with its own Origin: propagating it untouched keeps
        // the trace, and an unknown id answers 404 instead of dereferencing a null as the legacy
        // detail screen did.
        if (result.IsFailure)
            return result.Error;

        return result.Value.ToOutputDto();
    }
}
