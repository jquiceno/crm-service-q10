using System.Text.Json;
using Shared.Application.Interfaces;

namespace ServiceTemplate.Tests.Api.Doubles;

/// <summary>
/// In-memory <see cref="ICacheService"/> that records every call for assertion.
/// Use <see cref="Seed{T}"/> to pre-populate cache entries before making requests.
/// </summary>
internal sealed class SpyCacheService : ICacheService
{
    private readonly Dictionary<string, string> _store = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public List<string> GetCalls { get; } = [];
    public List<(string Key, TimeSpan Ttl)> SetCalls { get; } = [];
    public List<string> RemoveCalls { get; } = [];
    public List<string> RemoveByPrefixCalls { get; } = [];

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        GetCalls.Add(key);

        if (_store.TryGetValue(key, out var json))
            return Task.FromResult(JsonSerializer.Deserialize<T>(json, JsonOptions));

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        SetCalls.Add((key, ttl));
        _store[key] = JsonSerializer.Serialize(value, JsonOptions);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        RemoveCalls.Add(key);
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        RemoveByPrefixCalls.Add(prefix);

        foreach (var key in _store.Keys.Where(k => k.StartsWith(prefix)).ToList())
            _store.Remove(key);

        return Task.CompletedTask;
    }

    /// <summary>Writes <paramref name="value"/> at <paramref name="key"/> so the next Get returns a hit.</summary>
    public void Seed<T>(string key, T value)
        => _store[key] = JsonSerializer.Serialize(value, JsonOptions);

    /// <summary>Returns <c>true</c> if <paramref name="key"/> is currently in the store.</summary>
    public bool Contains(string key) => _store.ContainsKey(key);
}
