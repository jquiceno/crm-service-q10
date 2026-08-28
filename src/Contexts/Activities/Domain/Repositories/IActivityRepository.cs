using Activities.Domain.Aggregates;
using Activities.Domain.Queries;
using Activities.Domain.Models;
using Shared.Domain.Interfaces;
using Shared.Domain.Pagination;
using Shared.Results;

namespace Activities.Domain.Repositories;

/// <summary>
/// Persistence contract for the <see cref="ActivityAggregate"/> aggregate.
/// </summary>
public interface IActivityRepository : IRootRepository<ActivityAggregate, int>
{
    /// <summary>
    /// Inserts the activity and returns it with the identity the database generated.
    /// </summary>
    /// <remarks>
    /// Separate from <c>AddAsync</c>, which only queues the insert: the creation flow needs the
    /// consecutive back in the same call, and it does not exist until the row is written. A caller
    /// that persists through here does not commit a unit of work afterwards.
    /// </remarks>
    Task<Result<ActivityAggregate>> CreateAsync(
        ActivityAggregate aggregate, CancellationToken cancellationToken = default);

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
