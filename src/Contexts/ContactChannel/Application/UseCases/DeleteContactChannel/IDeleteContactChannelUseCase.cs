using Shared.Results;

namespace ContactChannel.Application.UseCases.DeleteContactChannel;

public interface IDeleteContactChannelUseCase
{
    Task<Result> ExecuteAsync(int id, CancellationToken cancellationToken = default);
}
