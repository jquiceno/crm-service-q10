using Shared.Application.Ports;

namespace Infrastructure.Caching;

/// <summary>No-op L2 cache used when the distributed cache is disabled. Every read is a miss.</summary>
public sealed class NoOpCacheStore : ICacheStore
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
        Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class =>
        Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
