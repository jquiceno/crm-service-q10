using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Enums;
using BusinessStatus.Domain.Queries;
using Shared.Domain.Interfaces;
using Shared.Domain.Pagination;
using Shared.Results;

namespace BusinessStatus.Domain.Repositories;

public interface IBusinessStatusRepository : IRootRepository<BusinessStatusAggregate, int>
{
    Task<PagedResult<BusinessStatusAggregate>> GetAsync(
        BusinessStatusFilter filter, PageQuery page, CancellationToken cancellationToken = default);

    Task<Result<BusinessStatusAggregate>> CreateAsync(
        BusinessStatusAggregate aggregate, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BusinessStatusAggregate>>> GetActiveTerminalsAsync(
        TerminalKind kind, CancellationToken cancellationToken = default);
}
