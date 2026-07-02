using Infrastructure.Caching;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shared.Application.Ports;
using StackExchange.Redis;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure;

public sealed class RedisCacheStoreTests
{
    private readonly IConnectionMultiplexer _connection = Substitute.For<IConnectionMultiplexer>();
    private readonly ILoggerPort<RedisCacheStore> _logger = Substitute.For<ILoggerPort<RedisCacheStore>>();

    private RedisCacheStore CreateSut() => new(_connection, _logger);

    private sealed record Sample(string Name);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("orders:list")]        // not namespaced with ctx:
    public async Task RemoveByPrefixAsync_RejectsInvalidPrefix(string prefix)
    {
        await Should.ThrowAsync<ArgumentException>(() => CreateSut().RemoveByPrefixAsync(prefix));
        _connection.DidNotReceive().GetEndPoints(Arg.Any<bool>());
    }

    [Fact]
    public async Task GetAsync_WhenBackendThrows_ReturnsNullAndLogs()
    {
        _connection.GetDatabase().Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var value = await CreateSut().GetAsync<Sample>("ctx:t:v1:x:1");

        value.ShouldBeNull();
        _logger.Received().Warning(Arg.Any<Exception>(), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task SetAsync_WhenBackendThrows_DoesNotThrowAndLogs()
    {
        _connection.GetDatabase().Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        await Should.NotThrowAsync(() => CreateSut().SetAsync("ctx:t:v1:x:1", new Sample("a"), TimeSpan.FromMinutes(1)));
        _logger.Received().Warning(Arg.Any<Exception>(), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task RemoveAsync_WhenBackendThrows_DoesNotThrow()
    {
        _connection.GetDatabase().Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        await Should.NotThrowAsync(() => CreateSut().RemoveAsync("ctx:t:v1:x:1"));
        _logger.Received().Warning(Arg.Any<Exception>(), Arg.Any<string>(), Arg.Any<object[]>());
    }
}
