using Activities.Domain.Aggregates;
using Activities.Domain.Filters;
using Shared.Domain.Interfaces;
using Shared.Domain.Pagination;
using Shared.Results;

namespace Activities.Domain.Repositories;

/// <summary>
/// Persistence contract for the <see cref="Activity"/> aggregate.
/// </summary>
public interface IActivityRepository : IRootRepository<Activity, int>
{
    /// <summary>Excludes activities with no matching deal/opportunity, like the legacy SP. Ordered by id.</summary>
    Task<PagedResult<Activity>> SearchAsync(
        ActivityFilter filter, PageQuery page, CancellationToken cancellationToken = default);
}
