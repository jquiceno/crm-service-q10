using LossReason.Domain.Errors;
using LossReason.Domain.Repositories;
using Shared.Results;

namespace LossReason.Application.UseCases.CreateLossReason;

// No IUnitOfWorkPort here: the key is an IDENTITY, so CreateAsync commits the insert
// inside the repository and hands back the aggregate with its assigned id.
public sealed class CreateLossReasonUseCase(ILossReasonRepository repository) : ICreateLossReasonUseCase
{
    private const string Origin = nameof(CreateLossReasonUseCase);

    public async Task<Result<CreateLossReasonOutputDto>> ExecuteAsync(
        CreateLossReasonInputDto input,
        CancellationToken cancellationToken = default)
    {
        var aggregateResult = input.ToAggregate();
        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = LossReasonErrors.Context, Origin = Origin };

        var persistResult = await repository
            .CreateAsync(aggregateResult.Value, cancellationToken)
            .ConfigureAwait(false);
        if (persistResult.IsFailure)
            return persistResult.Error;

        return persistResult.Value.ToOutputDto();
    }
}
