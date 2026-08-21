using Shared.Results;

namespace ContactChannel.Application.UseCases.CreateContactChannel;

public interface ICreateContactChannelUseCase
{
    Task<Result<CreateContactChannelOutputDto>> ExecuteAsync(
        CreateContactChannelInputDto input,
        CancellationToken cancellationToken = default);
}
