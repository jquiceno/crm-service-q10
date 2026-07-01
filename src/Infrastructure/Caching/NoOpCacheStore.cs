using Shared.Application.Ports;
using Shared.Results;

namespace Infrastructure.Caching;

/// <summary>No-op L2 cache used when the distributed cache is disabled. Always runs the factory.</summary>
public sealed class NoOpCacheStore : ICacheStore
{
    public Task<Result<T>> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<Task<Result<T>>> factory,
        CancellationToken cancellationToken = default) => factory();

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
