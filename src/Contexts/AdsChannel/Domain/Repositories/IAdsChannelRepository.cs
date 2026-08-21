using AdsChannel.Domain.Aggregates;
using AdsChannel.Domain.Queries;
using Shared.Domain.Interfaces;
using Shared.Domain.Pagination;
using Shared.Results;

namespace AdsChannel.Domain.Repositories;

public interface IAdsChannelRepository : IRootRepository<AdsChannelAggregate, int>
{
    Task<Result<bool>> ExistsByNameAsync(
        string name, int? excludingId = null, CancellationToken cancellationToken = default);

    Task<PagedResult<AdsChannelAggregate>> GetAsync(
        AdsChannelFilter filter, PageQuery page, CancellationToken cancellationToken = default);

    Task<Result<AdsChannelAggregate>> CreateAsync(
        AdsChannelAggregate aggregate, CancellationToken cancellationToken = default);
}
