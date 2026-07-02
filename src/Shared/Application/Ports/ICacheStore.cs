namespace Shared.Application.Ports;

/// <summary>
/// L2 application cache. The caller orchestrates cache-aside explicitly: read with
/// <see cref="GetAsync{T}"/>, and on a miss populate with <see cref="SetAsync{T}"/>.
/// Implementations MUST degrade gracefully: any backend failure is logged and swallowed —
/// <see cref="GetAsync{T}"/> returns <c>null</c> (treated as a miss) and the write/invalidation
/// operations no-op.
/// </summary>
public interface ICacheStore
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or <c>null</c> on a miss
    /// (or when the backend is unavailable).
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/> for <paramref name="ttl"/>.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Removes a single key (precise invalidation on mutation).</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every key under <paramref name="prefix"/> (collection invalidation).
    /// <paramref name="prefix"/> must be non-empty and start with "ctx:".
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
