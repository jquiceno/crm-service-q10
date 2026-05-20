using Shared.Domain.Result;

namespace Shared.Application.Ports;

public interface IUnitOfWorkPort
{
    Task<Result> CommitAsync(CancellationToken cancellationToken = default);
}
