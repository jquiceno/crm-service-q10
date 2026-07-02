using Infrastructure.Caching;
using NSubstitute;
using Shared.Application.Caching;
using Shared.Application.Ports;
using Shouldly;
using Xunit;

namespace IntegrationTests.Caching;

public sealed class RedisCacheStoreIntegrationTests : IClassFixture<RedisContainerFixture>, IAsyncLifetime
{
    private readonly RedisContainerFixture _fixture;
    private readonly RedisCacheStore _sut;

    public RedisCacheStoreIntegrationTests(RedisContainerFixture fixture)
    {
        _fixture = fixture;
        _sut = new RedisCacheStore(fixture.Connection, Substitute.For<ILoggerPort<RedisCacheStore>>());
    }

    public Task InitializeAsync() => _fixture.FlushAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record SampleDto(string Name);

    [Fact]
    public async Task GetAsync_OnMiss_ReturnsNull()
    {
        var value = await _sut.GetAsync<SampleDto>(CacheKey.For("orders").Resource("order", 404));

        value.ShouldBeNull();
    }

    [Fact]
    public async Task SetThenGet_ReturnsStoredValue()
    {
        var key = CacheKey.For("orders").Resource("order", 1);

        await _sut.SetAsync(key, new SampleDto("acme"), TimeSpan.FromMinutes(5));
        var value = await _sut.GetAsync<SampleDto>(key);

        value.ShouldNotBeNull();
        value.Name.ShouldBe("acme");
    }

    [Fact]
    public async Task RemoveAsync_EvictsKey()
    {
        var key = CacheKey.For("orders").Resource("order", 2);
        await _sut.SetAsync(key, new SampleDto("acme"), TimeSpan.FromMinutes(5));

        await _sut.RemoveAsync(key);

        (await _sut.GetAsync<SampleDto>(key)).ShouldBeNull();
    }

    [Fact]
    public async Task RemoveByPrefixAsync_InvalidatesWholeFamily()
    {
        var page1 = CacheKey.For("orders").Prefix("order:list:page=1");
        var page2 = CacheKey.For("orders").Prefix("order:list:page=2");
        await _sut.SetAsync(page1, new SampleDto("p1"), TimeSpan.FromMinutes(5));
        await _sut.SetAsync(page2, new SampleDto("p2"), TimeSpan.FromMinutes(5));

        await _sut.RemoveByPrefixAsync(CacheKey.For("orders").Prefix("order:list"));

        (await _sut.GetAsync<SampleDto>(page1)).ShouldBeNull();
        (await _sut.GetAsync<SampleDto>(page2)).ShouldBeNull();
    }

    [Fact]
    public async Task TenantPartition_KeysDoNotCollide()
    {
        var acme = CacheKey.For("orders").Tenant("acme").Resource("order", 1);
        var globex = CacheKey.For("orders").Tenant("globex").Resource("order", 1);

        await _sut.SetAsync(acme, new SampleDto("acme"), TimeSpan.FromMinutes(5));
        await _sut.SetAsync(globex, new SampleDto("globex"), TimeSpan.FromMinutes(5));

        (await _sut.GetAsync<SampleDto>(acme))!.Name.ShouldBe("acme");
        (await _sut.GetAsync<SampleDto>(globex))!.Name.ShouldBe("globex");
    }
}
