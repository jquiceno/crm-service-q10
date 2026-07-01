using Infrastructure.Caching;
using NSubstitute;
using Shared.Application.Caching;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;
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
    public async Task GetOrSetAsync_MissThenHit_RunsFactoryOnce()
    {
        var key = CacheKey.For("orders").Resource("order", 1);
        var calls = 0;
        Task<Result<SampleDto>> Factory()
        {
            calls++;
            return Task.FromResult(Result<SampleDto>.Success(new SampleDto("acme")));
        }

        var first = await _sut.GetOrSetAsync(key, TimeSpan.FromMinutes(5), Factory);
        var second = await _sut.GetOrSetAsync(key, TimeSpan.FromMinutes(5), Factory);

        calls.ShouldBe(1);
        first.Value.Name.ShouldBe("acme");
        second.Value.Name.ShouldBe("acme");
    }

    [Fact]
    public async Task GetOrSetAsync_FailedResult_IsNotCached()
    {
        var key = CacheKey.For("orders").Resource("order", 2);
        var calls = 0;
        Task<Result<SampleDto>> Factory()
        {
            calls++;
            return Task.FromResult(Result<SampleDto>.Failure(new DomainError("nope", ErrorType.Internal)));
        }

        await _sut.GetOrSetAsync(key, TimeSpan.FromMinutes(5), Factory);
        await _sut.GetOrSetAsync(key, TimeSpan.FromMinutes(5), Factory);

        calls.ShouldBe(2); // failure never stored, so factory runs again
    }

    [Fact]
    public async Task RemoveByPrefixAsync_InvalidatesWholeFamily()
    {
        var calls = 0;
        Task<Result<SampleDto>> Factory()
        {
            calls++;
            return Task.FromResult(Result<SampleDto>.Success(new SampleDto("x")));
        }

        var page1 = CacheKey.For("orders").Prefix("order:list:page=1");
        var page2 = CacheKey.For("orders").Prefix("order:list:page=2");
        await _sut.GetOrSetAsync(page1, TimeSpan.FromMinutes(5), Factory);
        await _sut.GetOrSetAsync(page2, TimeSpan.FromMinutes(5), Factory);
        calls.ShouldBe(2);

        await _sut.RemoveByPrefixAsync(CacheKey.For("orders").Prefix("order:list"));

        await _sut.GetOrSetAsync(page1, TimeSpan.FromMinutes(5), Factory);
        await _sut.GetOrSetAsync(page2, TimeSpan.FromMinutes(5), Factory);
        calls.ShouldBe(4); // both re-fetched after invalidation
    }

    [Fact]
    public async Task TenantPartition_KeysDoNotCollide()
    {
        Task<Result<SampleDto>> Factory(string name) =>
            Task.FromResult(Result<SampleDto>.Success(new SampleDto(name)));

        var acme = CacheKey.For("orders").Tenant("acme").Resource("order", 1);
        var globex = CacheKey.For("orders").Tenant("globex").Resource("order", 1);

        var a = await _sut.GetOrSetAsync(acme, TimeSpan.FromMinutes(5), () => Factory("acme"));
        var g = await _sut.GetOrSetAsync(globex, TimeSpan.FromMinutes(5), () => Factory("globex"));

        a.Value.Name.ShouldBe("acme");
        g.Value.Name.ShouldBe("globex");
    }
}
