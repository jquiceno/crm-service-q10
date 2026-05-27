namespace Shared.Application.Ports;

public interface IUnitOfWorkPort
{
    Task<global::Shared.Result.Result> CommitAsync(CancellationToken cancellationToken = default);
}
