using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Queries;
using Shared.Domain.Interfaces;
using Shared.Domain.Pagination;
using Shared.Results;

namespace ContactChannel.Domain.Repositories;

public interface IContactChannelRepository : IRootRepository<ContactChannelAggregate, int>
{
    Task<PagedResult<ContactChannelAggregate>> GetAsync(
        ContactChannelFilter filter,
        PageQuery page,
        CancellationToken cancellationToken = default);
}
