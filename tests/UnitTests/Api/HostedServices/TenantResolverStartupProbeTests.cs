using System.Net;
using Api.HostedServices;
using Infrastructure.MasterAccess.Http.Tenants;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Application.Ports;
using Shouldly;
using Xunit;

namespace UnitTests.Api.HostedServices;

public sealed class TenantResolverStartupProbeTests
{
    private sealed class StubHandler(Func<HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder());
    }

    private sealed class FaultingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class TokenAwareHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static TenantResolverStartupProbe Build(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));

        return new TenantResolverStartupProbe(
            factory,
            Options.Create(new TenantResolverServiceSettings { BaseUrl = "https://resolver.local/", TimeoutSeconds = 5 }),
            Substitute.For<ILoggerPort<TenantResolverStartupProbe>>());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound)]           // any HTTP response means the endpoint is reachable
    [InlineData(HttpStatusCode.ServiceUnavailable)] // even an unhealthy resolver is still "reachable"
    public async Task StartingAsync_WhenEndpointResponds_CompletesWithoutThrowing(HttpStatusCode status)
    {
        var probe = Build(new StubHandler(() => new HttpResponseMessage(status)));

        await Should.NotThrowAsync(() => probe.StartingAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartingAsync_WhenEndpointUnreachable_ThrowsAndAbortsStartup()
    {
        var probe = Build(new FaultingHandler(new HttpRequestException("connection refused")));

        await Should.ThrowAsync<InvalidOperationException>(() => probe.StartingAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartingAsync_WhenRequestTimesOut_ThrowsInvalidOperationException()
    {
        // HttpClient.Timeout surfaces as a TaskCanceledException whose token is NOT the caller's.
        var probe = Build(new FaultingHandler(new TaskCanceledException("timeout")));

        await Should.ThrowAsync<InvalidOperationException>(() => probe.StartingAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartingAsync_WhenHostCancels_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var probe = Build(new TokenAwareHandler());

        // Genuine host cancellation must propagate, never be masked as a fatal "not reachable" error.
        await Should.ThrowAsync<OperationCanceledException>(() => probe.StartingAsync(cts.Token));
    }
}
