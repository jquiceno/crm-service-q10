using Shared.Results;

namespace ContactChannel.Application.Ports;

public interface IContactChannelUsageReader
{
    Task<Result<bool>> IsReferencedAsync(int id, CancellationToken cancellationToken = default);
}
