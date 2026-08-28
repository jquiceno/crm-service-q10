using Shared.Results;

namespace ContactChannel.Application.UseCases.UpdateContactChannel;

public interface IUpdateContactChannelUseCase
{
    Task<Result<UpdateContactChannelOutputDto>> ExecuteAsync(
        int id,
        UpdateContactChannelInputDto input,
        CancellationToken cancellationToken = default);
}
