using System.Text.Json;
using Shared.Application.Ports;
using Shared.Results;
using StackExchange.Redis;

namespace Infrastructure.Caching;

/// <summary>
/// Redis-backed L2 cache (StackExchange.Redis). Serializes only the success payload of a
/// <see cref="Result{T}"/>. Every backend operation degrades gracefully: on failure it logs a
/// warning and falls back (reads run the factory; invalidations no-op).
/// </summary>
public sealed class RedisCacheStore(
    IConnectionMultiplexer connection,
    ILoggerPort<RedisCacheStore> logger) : ICacheStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<T>> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<Task<Result<T>>> factory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cached = await connection.GetDatabase().StringGetAsync(key).ConfigureAwait(false);
            if (cached.HasValue)
            {
                var value = JsonSerializer.Deserialize<T>((string)cached!, JsonOptions);
                if (value is not null)
                    return Result<T>.Success(value);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Warning(ex, "L2 cache read failed for {Key}", key);
        }

        var result = await factory().ConfigureAwait(false);

        if (result.IsSuccess)
        {
            try
            {
                var payload = JsonSerializer.Serialize(result.Value, JsonOptions);
                await connection.GetDatabase().StringSetAsync(key, payload, ttl).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.Warning(ex, "L2 cache write failed for {Key}", key);
            }
        }

        return result;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
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
