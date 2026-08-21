using Shared.Domain.Pagination;
using Shared.Results;

namespace BusinessStatus.Application.UseCases.GetBusinessStatuses;

public interface IGetBusinessStatusesUseCase
{
    Task<PagedResult<GetBusinessStatusesOutputDto>> ExecuteAsync(
        GetBusinessStatusesInputDto input,
        PageQuery page,
        CancellationToken cancellationToken = default);
}
