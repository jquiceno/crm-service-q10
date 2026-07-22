using Infrastructure.MasterAccess.Http.Tenants;
using Microsoft.Extensions.Options;
using Shared.Application.Ports;

namespace Api.HostedServices;

/// <summary>
/// Fails application startup when the tenant-resolver endpoint is unreachable, so the service only
/// comes up if per-request tenant resolution can work. The probe runs in <see cref="StartingAsync"/> —
/// before Kestrel binds its port — so an unreachable dependency aborts the boot without ever accepting
/// a connection. Registered only in multitenant mode. Any HTTP response counts as "reachable"; only a
/// connection/timeout failure aborts (the orchestrator then restarts, retrying until the dependency is
/// up). Reachability here is intentionally weaker than readiness (<c>/health/ready</c>), which requires
/// the resolver's own <c>/health</c> to return 2xx.
/// </summary>
public sealed class TenantResolverStartupProbe(
    IHttpClientFactory httpClientFactory,
    IOptions<TenantResolverServiceSettings> options,
    ILoggerPort<TenantResolverStartupProbe> logger) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var healthUrl = settings.BaseUrl.TrimEnd('/') + "/health";

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);

        try
        {
            using var response = await client.GetAsync(healthUrl, cancellationToken).ConfigureAwait(false);
            logger.Info("Tenant resolver reachable at {HealthUrl} (HTTP {StatusCode}).",
                healthUrl, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Everything except genuine host cancellation (SIGTERM during boot) maps to a fatal error;
            // HttpClient.Timeout surfaces as a TaskCanceledException whose token is NOT the caller's.
            throw new InvalidOperationException(
                $"Critical Error: the tenant resolver at '{healthUrl}' is not reachable. "
                + "Multitenancy is enabled, so tenant resolution cannot work. Application startup aborted.",
                ex);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
