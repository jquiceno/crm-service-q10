using Shared.Domain.Pagination;
using Shared.Results;

namespace Activities.Application.UseCases.GetActivities;

/// <summary>Entry point of <c>GET /activities</c>, invoked by the controller.</summary>
public interface IGetActivitiesUseCase
{
    Task<PagedResult<GetActivitiesOutputDto>> ExecuteAsync(
        GetActivitiesInputDto input, PageQuery page, CancellationToken cancellationToken = default);
}
