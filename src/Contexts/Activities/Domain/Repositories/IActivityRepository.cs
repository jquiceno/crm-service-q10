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
    /// <summary>
    /// Lists activities joined to their deal and opportunity, the same way the legacy API stored
    /// procedure does: an activity whose deal is missing, or whose deal's opportunity is missing,
    /// is excluded rather than returned with a null relation. Ordered by identity ascending, like
    /// the legacy procedure orders by <c>negact_consecutivoP ASC</c>.
    /// </summary>
    Task<PagedResult<Activity>> SearchAsync(
        ActivityFilter filter, PageQuery page, CancellationToken cancellationToken = default);
}
