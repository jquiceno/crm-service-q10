using Shared.Domain.Pagination;
using Shared.Results;

namespace ContactChannel.Application.UseCases.GetContactChannels;

public interface IGetContactChannelsUseCase
{
    Task<PagedResult<GetContactChannelsOutputDto>> ExecuteAsync(
        GetContactChannelsInputDto input,
        PageQuery page,
        CancellationToken cancellationToken = default);
}
