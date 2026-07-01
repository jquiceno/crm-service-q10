using Shared.Results;

namespace Shared.Application.Ports;

/// <summary>
/// L2 application cache (cache-aside). Implementations MUST degrade gracefully: any
/// backend failure is logged and swallowed — reads fall back to the factory, invalidations
/// no-op. Only successful results are cached.
/// </summary>
public interface ICacheStore
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or runs <paramref name="factory"/>
    /// on a miss and caches its result only if successful.
    /// </summary>
    Task<Result<T>> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<Task<Result<T>>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a single key (precise invalidation on mutation).</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every key under <paramref name="prefix"/> (collection invalidation).
    /// <paramref name="prefix"/> must be non-empty and start with "ctx:".
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
