using Infrastructure.Caching;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shared.Application.Ports;
using Shared.Results;
using StackExchange.Redis;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure;

public sealed class RedisCacheStoreTests
{
    private readonly IConnectionMultiplexer _connection = Substitute.For<IConnectionMultiplexer>();
    private readonly ILoggerPort<RedisCacheStore> _logger = Substitute.For<ILoggerPort<RedisCacheStore>>();

    private RedisCacheStore CreateSut() => new(_connection, _logger);

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
    public async Task GetOrSetAsync_WhenBackendThrows_RunsFactoryAndLogs()
    {
        _connection.GetDatabase().Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var result = await CreateSut().GetOrSetAsync(
            "ctx:t:v1:x:1", TimeSpan.FromMinutes(1),
            () => Task.FromResult(Result<int>.Success(7)));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(7);
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
