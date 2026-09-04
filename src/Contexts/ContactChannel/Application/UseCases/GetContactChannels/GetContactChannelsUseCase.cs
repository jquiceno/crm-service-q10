using ContactChannel.Domain.Queries;
using ContactChannel.Domain.Repositories;
using Shared.Domain.Pagination;
using Shared.Results;

namespace ContactChannel.Application.UseCases.GetContactChannels;

public sealed class GetContactChannelsUseCase(IContactChannelRepository repository)
    : IGetContactChannelsUseCase
{
    public async Task<PagedResult<GetContactChannelsOutputDto>> ExecuteAsync(
        GetContactChannelsInputDto input,
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        var filter = new ContactChannelFilter(input.IsActive, input.Search);

        var result = await repository
            .GetAsync(filter, page, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
            return PagedResult<GetContactChannelsOutputDto>.Failure(result.Error);

        return PagedResult<GetContactChannelsOutputDto>.Success(
            [.. result.Items.Select(channel => channel.ToOutputDto())],
            result.TotalCount);
    }
}
