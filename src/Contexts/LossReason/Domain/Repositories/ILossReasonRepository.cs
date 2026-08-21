using LossReason.Domain.Aggregates;
using LossReason.Domain.Queries;
using Shared.Domain.Interfaces;
using Shared.Domain.Pagination;
using Shared.Results;

namespace LossReason.Domain.Repositories;

public interface ILossReasonRepository : IRootRepository<LossReasonAggregate, int>
{
    Task<PagedResult<LossReasonAggregate>> GetAsync(
        LossReasonFilter filter,
        PageQuery page,
        CancellationToken cancellationToken = default);

    Task<Result<LossReasonAggregate>> CreateAsync(
        LossReasonAggregate aggregate,
        CancellationToken cancellationToken = default);
}
