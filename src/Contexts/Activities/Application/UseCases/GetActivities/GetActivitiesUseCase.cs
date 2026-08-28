using Activities.Domain.Queries;
using Activities.Domain.Repositories;
using Shared.Domain.Pagination;
using Shared.Results;

namespace Activities.Application.UseCases.GetActivities;

/// <summary>
/// Lists the activities of a deal, an opportunity or a deal state, paged.
/// </summary>
/// <remarks>
/// Read-only: no <c>IUnitOfWorkPort</c>, no aggregate is mutated. It returns
/// <see cref="PagedResult{T}"/> straight from the repository, so a persistence failure propagates
/// with the origin the adapter already set instead of being re-wrapped here.
/// </remarks>
public sealed class GetActivitiesUseCase(IActivityRepository repository) : IGetActivitiesUseCase
{
    public async Task<PagedResult<GetActivitiesOutputDto>> ExecuteAsync(
        GetActivitiesInputDto input, PageQuery page, CancellationToken cancellationToken = default)
    {
        var filter = new ActivityFilter(input.DealId, input.OpportunityId, input.DealStateId);

        var result = await repository.SearchAsync(filter, page, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return PagedResult<GetActivitiesOutputDto>.Failure(result.Error);

        return PagedResult<GetActivitiesOutputDto>.Success(
            [.. result.Items.Select(item => item.ToOutputDto())],
            result.TotalCount);
    }
}
