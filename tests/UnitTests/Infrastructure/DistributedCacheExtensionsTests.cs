using Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared.Application.Ports;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure;

public sealed class DistributedCacheExtensionsTests
{
    private static IConfiguration Config(bool l2Enabled, string connectionString) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cache:L2Enabled"] = l2Enabled.ToString(),
            ["Cache:ConnectionString"] = connectionString,
        }).Build();

    [Fact]
    public void AddDistributedCache_WhenDisabled_RegistersNoOpStore()
    {
        var services = new ServiceCollection();
        services.AddDistributedCache(Config(l2Enabled: false, connectionString: "localhost:6379"));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ICacheStore>().ShouldBeOfType<NoOpCacheStore>();
    }

    [Fact]
    public void AddDistributedCache_WhenEnabledWithoutConnectionString_RegistersNoOpStore()
    {
        var services = new ServiceCollection();
        services.AddDistributedCache(Config(l2Enabled: true, connectionString: ""));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ICacheStore>().ShouldBeOfType<NoOpCacheStore>();
    }

    [Fact]
    public void AddDistributedCache_WhenEnabledWithConnectionString_RegistersRedisStore()
    {
        var services = new ServiceCollection();
        // logger dependency of RedisCacheStore (avoid pulling Serilog into the test)
        services.AddSingleton(Substitute.For<ILoggerPort<RedisCacheStore>>());
        services.AddDistributedCache(Config(l2Enabled: true, connectionString: "localhost:6379"));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ICacheStore>().ShouldBeOfType<RedisCacheStore>();
    }
}
