using BusinessStatus.Domain.Repositories;
using Shared.Domain.Pagination;
using Shared.Results;

namespace BusinessStatus.Application.UseCases.GetBusinessStatuses;

public sealed class GetBusinessStatusesUseCase(IBusinessStatusRepository repository)
    : IGetBusinessStatusesUseCase
{
    public async Task<PagedResult<GetBusinessStatusesOutputDto>> ExecuteAsync(
        GetBusinessStatusesInputDto input,
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        var result = await repository
            .GetAsync(input.ToFilter(), page, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
            return PagedResult<GetBusinessStatusesOutputDto>.Failure(result.Error);

        return PagedResult<GetBusinessStatusesOutputDto>.Success(
            [.. result.Items.Select(aggregate => aggregate.ToOutputDto())],
            result.TotalCount);
    }
}
