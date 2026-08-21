using ContactChannel.Domain.Errors;
using ContactChannel.Domain.Repositories;
using Shared.Results;

namespace ContactChannel.Application.UseCases.CreateContactChannel;

public sealed class CreateContactChannelUseCase(IContactChannelRepository repository)
    : ICreateContactChannelUseCase
{
    private const string Origin = nameof(CreateContactChannelUseCase);

    public async Task<Result<CreateContactChannelOutputDto>> ExecuteAsync(
        CreateContactChannelInputDto input,
        CancellationToken cancellationToken = default)
    {
        var aggregateResult = input.ToAggregate();

        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = ContactChannelErrors.Context, Origin = Origin };

        var persistResult = await repository
            .CreateAsync(aggregateResult.Value, cancellationToken)
            .ConfigureAwait(false);

        if (persistResult.IsFailure)
            return persistResult.Error;

        return persistResult.Value.ToOutputDto();
    }
}
