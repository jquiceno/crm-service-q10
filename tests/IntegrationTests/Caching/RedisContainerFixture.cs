using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace IntegrationTests.Caching;

public sealed class RedisContainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine")
        .Build();

    public IConnectionMultiplexer Connection { get; private set; } = null!;

    /// <summary>Endpoint of the container, for anything that configures Redis from a string.</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        var options = ConfigurationOptions.Parse(_container.GetConnectionString());
        options.AllowAdmin = true; // required for FLUSHDB in FlushAsync
        Connection = await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);
    }

    public async Task FlushAsync()
    {
        foreach (var endpoint in Connection.GetEndPoints())
            await Connection.GetServer(endpoint).FlushDatabaseAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await Connection.DisposeAsync().ConfigureAwait(false);
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}
