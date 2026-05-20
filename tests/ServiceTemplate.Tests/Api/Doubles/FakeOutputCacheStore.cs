namespace ServiceTemplate.Tests.Api.Doubles;

/// <summary>
/// Minimal <see cref="IOutputCacheStore"/> that records <c>EvictByTagAsync</c> calls
/// so the <c>[OutputCacheInvalidate]</c> filter can be unit-tested.
/// Get/Set are not used by the invalidation filter.
/// </summary>
internal sealed class FakeOutputCacheStore : IOutputCacheStore
{
    public List<string> EvictedTags { get; } = [];

    public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
    {
        EvictedTags.Add(tag);
        return ValueTask.CompletedTask;
    }

    public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
        => ValueTask.FromResult<byte[]?>(null);

    public ValueTask SetAsync(
        string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
