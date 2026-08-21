using BusinessStatus.Domain.Errors;
using BusinessStatus.Domain.Repositories;
using Shared.Results;

namespace BusinessStatus.Application.UseCases.CreateBusinessStatus;

public sealed class CreateBusinessStatusUseCase(IBusinessStatusRepository repository)
    : ICreateBusinessStatusUseCase
{
    private const string Origin = nameof(CreateBusinessStatusUseCase);

    public async Task<Result<CreateBusinessStatusOutputDto>> ExecuteAsync(
        CreateBusinessStatusInputDto input, CancellationToken cancellationToken = default)
    {
        var aggregateResult = input.ToAggregate();
        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = BusinessStatusErrors.Context, Origin = Origin };

        var persistResult = await repository
            .CreateAsync(aggregateResult.Value, cancellationToken)
            .ConfigureAwait(false);
        if (persistResult.IsFailure)
            return persistResult.Error;

        return persistResult.Value.ToOutputDto();
    }
}
