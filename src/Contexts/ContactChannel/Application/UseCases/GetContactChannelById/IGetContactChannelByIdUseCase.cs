using Shared.Results;

namespace ContactChannel.Application.UseCases.GetContactChannelById;

public interface IGetContactChannelByIdUseCase
{
    Task<Result<GetContactChannelByIdOutputDto>> ExecuteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
