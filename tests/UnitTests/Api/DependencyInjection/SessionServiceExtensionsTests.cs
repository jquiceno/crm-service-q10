using Api.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Ports;
using Shouldly;
using Xunit;

namespace UnitTests.Api.DependencyInjection;

public sealed class SessionServiceExtensionsTests
{
    /// <summary>
    /// With multitenancy off nothing resolves a tenant, but the port must still be bound: a
    /// consumer that partitions a cache key by tenant has to resolve and skip its cache, not fail
    /// to be constructed and take its endpoint down with a 500.
    /// </summary>
    [Fact]
    public void AddSessionServices_WithMultitenancyDisabled_BindsAProviderThatReportsNoTenant()
    {
        var services = new ServiceCollection()
            .AddSessionServices(new ConfigurationBuilder().Build(), multitenancyEnabled: false);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITenantCodeProvider>().Current.ShouldBeNull();
    }
}
