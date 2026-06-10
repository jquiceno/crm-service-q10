using Shared.Domain.Pagination;
using Shared.Results;

namespace Shared.Domain.Interfaces;

public interface IRootRepository<TAggregate, TId>
    where TAggregate : IAggregateRoot
    where TId : notnull
{
    Task<Result<TAggregate>> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<PagedResult<TAggregate>> GetAllAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<Result> AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    Result Update(TAggregate aggregate);
    Result Remove(TAggregate aggregate);
}
