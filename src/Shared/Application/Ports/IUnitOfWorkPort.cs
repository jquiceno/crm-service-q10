namespace Shared.Application.Ports;

public interface IUnitOfWorkPort
{
    Task<Results.Result> CommitAsync(CancellationToken cancellationToken = default);
}
