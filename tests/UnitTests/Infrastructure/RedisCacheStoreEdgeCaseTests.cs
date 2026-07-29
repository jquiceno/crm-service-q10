using System.Net;
using Infrastructure.Caching;
using NSubstitute;
using Shared.Application.Ports;
using StackExchange.Redis;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure;

public sealed class RedisCacheStoreEdgeCaseTests
{
    private readonly IConnectionMultiplexer _connection = Substitute.For<IConnectionMultiplexer>();
    private readonly ILoggerPort<RedisCacheStore> _logger = Substitute.For<ILoggerPort<RedisCacheStore>>();

    private RedisCacheStore CreateSut() => new(_connection, _logger);

    private sealed record Sample(string Name);

    private static async IAsyncEnumerable<RedisKey> ToAsyncEnumerable(params RedisKey[] keys)
    {
        foreach (var key in keys)
        {
            await Task.Yield();
            yield return key;
        }
    }

    [Fact]
    public async Task GetAsync_WhenValueIsCached_ReturnsDeserializedValue()
    {
        var database = Substitute.For<IDatabase>();
        _connection.GetDatabase().Returns(database);
        database.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)"{\"name\":\"a\"}");

        var value = await CreateSut().GetAsync<Sample>("ctx:t:v1:x:1");

        value.ShouldNotBeNull();
        value.Name.ShouldBe("a");
    }

    [Fact]
    public async Task GetAsync_WhenKeyIsMissing_ReturnsNullWithoutLogging()
    {
        var database = Substitute.For<IDatabase>();
        _connection.GetDatabase().Returns(database);
        database.StringGetAsync(Arg.Any<RedisKey>()).Returns(RedisValue.Null);

        var value = await CreateSut().GetAsync<Sample>("ctx:t:v1:x:1");

        value.ShouldBeNull();
        _logger.DidNotReceive().Warning(Arg.Any<Exception>(), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task GetAsync_WhenTokenAlreadyCancelled_ThrowsWithoutTouchingConnection()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => CreateSut().GetAsync<Sample>("ctx:t:v1:x:1", cts.Token));

        _connection.DidNotReceive().GetDatabase();
    }

    [Fact]
    public async Task SetAsync_WhenBackendSucceeds_WritesWebCamelCaseJsonWithTtl()
    {
        var database = Substitute.For<IDatabase>();
        _connection.GetDatabase().Returns(database);

        await Should.NotThrowAsync(() => CreateSut().SetAsync("ctx:t:v1:x:1", new Sample("a"), TimeSpan.FromMinutes(1)));

        // Pins the write effect AND the serialization contract (JsonSerializerDefaults.Web -> camelCase).
        await database.Received(1).StringSetAsync(
            (RedisKey)"ctx:t:v1:x:1",
            (RedisValue)"""{"name":"a"}""",
            TimeSpan.FromMinutes(1));
        _logger.DidNotReceive().Warning(Arg.Any<Exception>(), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task RemoveAsync_WhenBackendSucceeds_DeletesKey()
    {
        var database = Substitute.For<IDatabase>();
        _connection.GetDatabase().Returns(database);

        await Should.NotThrowAsync(() => CreateSut().RemoveAsync("ctx:t:v1:x:1"));

        await database.Received(1).KeyDeleteAsync((RedisKey)"ctx:t:v1:x:1");
        _logger.DidNotReceive().Warning(Arg.Any<Exception>(), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task RemoveByPrefixAsync_WhenServersHaveMatchingKeys_DeletesKeysFromNonReplicaServersOnly()
    {
        var database = Substitute.For<IDatabase>();
        var primary = Substitute.For<IServer>();
        var replica = Substitute.For<IServer>();
        var primaryEndpoint = new IPEndPoint(IPAddress.Loopback, 6379);
        var replicaEndpoint = new IPEndPoint(IPAddress.Loopback, 6380);
        var key1 = (RedisKey)"ctx:t:v1:x:1";
        var key2 = (RedisKey)"ctx:t:v1:x:2";

        _connection.GetDatabase().Returns(database);
        _connection.GetEndPoints(Arg.Any<bool>()).Returns([primaryEndpoint, replicaEndpoint]);
        _connection.GetServer(primaryEndpoint).Returns(primary);
        _connection.GetServer(replicaEndpoint).Returns(replica);
        primary.IsReplica.Returns(false);
        replica.IsReplica.Returns(true);
        primary.KeysAsync(pattern: Arg.Any<RedisValue>()).Returns(ToAsyncEnumerable(key1, key2));

        await Should.NotThrowAsync(() => CreateSut().RemoveByPrefixAsync("ctx:t:v1:x"));

        await database.Received(1).KeyDeleteAsync(key1);
        await database.Received(1).KeyDeleteAsync(key2);
        _logger.DidNotReceive().Warning(Arg.Any<Exception>(), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task RemoveByPrefixAsync_WhenTokenAlreadyCancelled_ThrowsWithoutTouchingConnection()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => CreateSut().RemoveByPrefixAsync("ctx:t:v1:x", cts.Token));

        _connection.DidNotReceive().GetDatabase();
    }
}
