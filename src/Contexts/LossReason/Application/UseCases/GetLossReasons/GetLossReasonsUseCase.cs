using LossReason.Domain.Repositories;
using Shared.Domain.Pagination;
using Shared.Results;

namespace LossReason.Application.UseCases.GetLossReasons;

public sealed class GetLossReasonsUseCase(ILossReasonRepository repository) : IGetLossReasonsUseCase
{
    public async Task<PagedResult<GetLossReasonsOutputDto>> ExecuteAsync(
        GetLossReasonsInputDto input,
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        var result = await repository
            .GetAsync(input.ToFilter(), page, cancellationToken)
            .ConfigureAwait(false);

        // An empty catalogue is a successful empty page, never an error (D9).
        if (result.IsFailure)
            return PagedResult<GetLossReasonsOutputDto>.Failure(result.Error);

        return PagedResult<GetLossReasonsOutputDto>.Success(
            [.. result.Items.Select(lossReason => lossReason.ToOutputDto())],
            result.TotalCount);
    }
}
