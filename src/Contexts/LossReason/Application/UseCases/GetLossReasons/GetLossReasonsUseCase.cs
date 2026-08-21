using LossReason.Domain.Queries;
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
        var filter = new LossReasonFilter(input.Name, input.IsActive);

        var result = await repository
            .GetAsync(filter, page, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
            return PagedResult<GetLossReasonsOutputDto>.Failure(result.Error);

        return PagedResult<GetLossReasonsOutputDto>.Success(
            [.. result.Items.Select(lossReason => lossReason.ToOutputDto())],
            result.TotalCount);
    }
}
