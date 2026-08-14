using Api.HostedServices;
using Infrastructure.MasterAccess.Http.Tenants;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Application.Ports;
using Shouldly;
using Xunit;

namespace UnitTests.Api.HostedServices;

/// <summary>
/// Covers the no-op <see cref="IHostedLifecycleService"/> members that
/// <see cref="TenantResolverStartupProbeTests"/> does not exercise.
/// </summary>
public sealed class TenantResolverStartupProbeLifecycleTests
{
    private static TenantResolverStartupProbe CreateSut() =>
        new(
            Substitute.For<IHttpClientFactory>(),
            Options.Create(new TenantResolverServiceSettings { BaseUrl = "https://resolver.local/", TimeoutSeconds = 5 }),
            Substitute.For<ILoggerPort<TenantResolverStartupProbe>>());

    [Fact]
    public void StartAsync_CompletesImmediately()
    {
        var task = CreateSut().StartAsync(CancellationToken.None);

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void StartedAsync_CompletesImmediately()
    {
        var task = CreateSut().StartedAsync(CancellationToken.None);

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void StoppingAsync_CompletesImmediately()
    {
        var task = CreateSut().StoppingAsync(CancellationToken.None);

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void StopAsync_CompletesImmediately()
    {
        var task = CreateSut().StopAsync(CancellationToken.None);

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void StoppedAsync_CompletesImmediately()
    {
        var task = CreateSut().StoppedAsync(CancellationToken.None);

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }
}
