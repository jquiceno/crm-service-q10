using Activities.Domain.Aggregates;
using Activities.Domain.Filters;
using Activities.Domain.Models;
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
    /// Excludes activities with no matching deal/opportunity, like the legacy SP. Ordered by id.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="ActivityListItem"/> rather than the bare aggregate because the listing
    /// contract also carries the deal, opportunity and advisor names — foreign data the aggregate
    /// does not own. Resolving them here keeps the listing to a single query, instead of the use
    /// case looking each name up per row.
    /// </remarks>
    Task<PagedResult<ActivityListItem>> SearchAsync(
        ActivityFilter filter, PageQuery page, CancellationToken cancellationToken = default);
}
