using System.Text.Json;
using Shared.Application.Ports;

namespace UnitTests.Infrastructure;

/// <summary>
/// In-memory <see cref="ICacheStore"/> that serializes with System.Text.Json exactly like
/// RedisCacheStore, so tests catch cached types that cannot be (de)serialized. Unlike
/// RedisCacheStore it does NOT swallow serialization errors — a non-deserializable cached
/// type surfaces as a test failure instead of a silent fallback.
/// </summary>
public sealed class JsonRoundTripCacheStore : ICacheStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, string> _store = new();

    public IReadOnlyCollection<string> Keys => _store.Keys.ToList();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
        Task.FromResult(_store.TryGetValue(key, out var json)
            ? JsonSerializer.Deserialize<T>(json, Options)
            : null);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        _store[key] = JsonSerializer.Serialize(value, Options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var k in _store.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            _store.Remove(k);
        return Task.CompletedTask;
    }
}
