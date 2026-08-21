using Shared.Domain.Pagination;
using Shared.Results;

namespace LossReason.Application.UseCases.GetLossReasons;

public interface IGetLossReasonsUseCase
{
    Task<PagedResult<GetLossReasonsOutputDto>> ExecuteAsync(
        GetLossReasonsInputDto input,
        PageQuery page,
        CancellationToken cancellationToken = default);
}
