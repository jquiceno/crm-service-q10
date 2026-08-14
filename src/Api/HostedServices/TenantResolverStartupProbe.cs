using Infrastructure.MasterAccess.Http.Tenants;
using Microsoft.Extensions.Options;
using Shared.Application.Ports;

namespace Api.HostedServices;

/// <summary>
/// Fails application startup when the tenant-resolver endpoint is not healthy, so the service only
/// comes up if per-request tenant resolution can work. The probe runs in <see cref="StartingAsync"/> —
/// before Kestrel binds its port — so a broken dependency aborts the boot without ever accepting a
/// connection. Registered only in multitenant mode. Startup aborts on either a connection/timeout
/// failure or a non-2xx response from the resolver's <c>/health</c> (the orchestrator then restarts,
/// retrying until the dependency is up). This requires 2xx just like readiness (<c>/health/ready</c>),
/// but runs once at boot rather than continuously.
/// </summary>
public sealed class TenantResolverStartupProbe(
    IHttpClientFactory httpClientFactory,
    IOptions<TenantResolverServiceSettings> options,
    ILoggerPort<TenantResolverStartupProbe> logger) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var healthUrl = settings.HealthUri;

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(healthUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Transport failure. Everything except genuine host cancellation (SIGTERM during boot) maps to a
            // fatal error; HttpClient.Timeout surfaces as a TaskCanceledException whose token is NOT the caller's.
            throw new InvalidOperationException(
                $"Critical Error: the tenant resolver at '{healthUrl}' is not reachable. "
                + "Multitenancy is enabled, so tenant resolution cannot work. Application startup aborted.",
                ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Critical Error: the tenant resolver at '{healthUrl}' returned HTTP {(int)response.StatusCode}. "
                    + "Multitenancy is enabled, so tenant resolution cannot work. Application startup aborted.");
            }
        }

        logger.Info("Tenant resolver reachable at {HealthUrl} (HTTP {StatusCode}).",
                healthUrl, (int)response.StatusCode);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
