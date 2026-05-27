using Shared.Domain.Pagination;
using Shared.Result;

namespace Shared.Domain.Interfaces;

public interface IRepositoryBase<TAggregate, TId>
    where TAggregate : IAggregateRoot
    where TId : notnull
{
    Task<Result<TAggregate>> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<PagedResult<TAggregate>> GetAllAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<global::Shared.Result.Result> AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    global::Shared.Result.Result Update(TAggregate aggregate);
    global::Shared.Result.Result Remove(TAggregate aggregate);
}
