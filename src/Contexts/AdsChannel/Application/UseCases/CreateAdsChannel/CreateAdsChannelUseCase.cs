using AdsChannel.Domain.Errors;
using AdsChannel.Domain.Repositories;
using Shared.Results;

namespace AdsChannel.Application.UseCases.CreateAdsChannel;

public sealed class CreateAdsChannelUseCase(IAdsChannelRepository repository) : ICreateAdsChannelUseCase
{
    private const string Origin = nameof(CreateAdsChannelUseCase);

    public async Task<Result<CreateAdsChannelOutputDto>> ExecuteAsync(
        CreateAdsChannelInputDto input, CancellationToken cancellationToken = default)
    {
        // 1. Domain validation first: a malformed body responds 400 without spending a query.
        var aggregateResult = input.ToAggregate();
        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = AdsChannelErrors.Context, Origin = Origin };

        // 2. Business rule that requires a DB query — only reached once the body is well-formed.
        //    Checked against the normalized name (Create() trims it), so a whitespace-only variant
        //    of an existing name can't slip past this check and later collide with what's persisted.
        var normalizedName = aggregateResult.Value.Name;
        var existsResult = await repository
            .ExistsByNameAsync(normalizedName, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (existsResult.IsFailure)
            return existsResult.Error;
        if (existsResult.Value)
            return AdsChannelErrors.NameAlreadyExists(normalizedName) with
            {
                Context = AdsChannelErrors.Context,
                Origin = Origin
            };

        // 3. Persist — CreateAsync inserts and commits internally to recover the SQL IDENTITY.
        var createResult = await repository
            .CreateAsync(aggregateResult.Value, cancellationToken)
            .ConfigureAwait(false);
        if (createResult.IsFailure)
            return createResult.Error;

        return createResult.Value.ToOutputDto();
    }
}
