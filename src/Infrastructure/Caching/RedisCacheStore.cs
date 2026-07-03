using System.Text.Json;
using Shared.Application.Ports;
using StackExchange.Redis;

namespace Infrastructure.Caching;

/// <summary>
/// Redis-backed L2 cache (StackExchange.Redis) using System.Text.Json. Every backend
/// operation degrades gracefully: on failure it logs a warning and falls back
/// (<see cref="GetAsync{T}"/> returns <c>null</c>; writes and invalidations no-op).
/// </summary>
public sealed class RedisCacheStore(
    IConnectionMultiplexer connection,
    ILoggerPort<RedisCacheStore> logger) : ICacheStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var cached = await connection.GetDatabase().StringGetAsync(key).ConfigureAwait(false);
            if (cached.HasValue)
                return JsonSerializer.Deserialize<T>((string)cached!, JsonOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Warning(ex, "L2 cache read failed for {Key}", key);
        }

        return null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await connection.GetDatabase().StringSetAsync(key, payload, ttl).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Warning(ex, "L2 cache write failed for {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await connection.GetDatabase().KeyDeleteAsync(key).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Warning(ex, "L2 cache remove failed for {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix) || !prefix.StartsWith("ctx:", StringComparison.Ordinal))
            throw new ArgumentException("Prefix must be non-empty and start with 'ctx:'.", nameof(prefix));

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var database = connection.GetDatabase();
            var pattern = new RedisValue($"{prefix}*");

            foreach (var endpoint in connection.GetEndPoints())
            {
                var server = connection.GetServer(endpoint);
                if (server.IsReplica)
                    continue;

                await foreach (var key in server.KeysAsync(pattern: pattern).WithCancellation(cancellationToken).ConfigureAwait(false))
                    await database.KeyDeleteAsync(key).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Warning(ex, "L2 cache prefix invalidation failed for {Prefix}", prefix);
        }
    }
}
