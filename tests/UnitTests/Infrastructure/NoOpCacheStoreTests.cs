using Infrastructure.Caching;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure;

public sealed class NoOpCacheStoreTests
{
    private readonly NoOpCacheStore _sut = new();

    private sealed record Sample(string Name);

    [Fact]
    public async Task GetAsync_AlwaysReturnsNull()
    {
        var value = await _sut.GetAsync<Sample>("ctx:t:v1:x:1");

        value.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_AfterSet_StillReturnsNull()
    {
        await _sut.SetAsync("ctx:t:v1:x:1", new Sample("acme"), TimeSpan.FromMinutes(1));

        var value = await _sut.GetAsync<Sample>("ctx:t:v1:x:1");

        value.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_RemoveAsync_RemoveByPrefixAsync_DoNotThrow()
    {
        await Should.NotThrowAsync(() => _sut.SetAsync("ctx:t:v1:x:1", new Sample("a"), TimeSpan.FromMinutes(1)));
        await Should.NotThrowAsync(() => _sut.RemoveAsync("ctx:t:v1:x:1"));
        await Should.NotThrowAsync(() => _sut.RemoveByPrefixAsync("ctx:t:v1:x:list"));
    }
}
