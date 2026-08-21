using ContactChannel.Domain.Repositories;
using Shared.Results;

namespace ContactChannel.Application.UseCases.GetContactChannelById;

public sealed class GetContactChannelByIdUseCase(IContactChannelRepository repository)
    : IGetContactChannelByIdUseCase
{
    public async Task<Result<GetContactChannelByIdOutputDto>> ExecuteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var result = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
            return result.Error;

        return result.Value.ToOutputDto();
    }
}
